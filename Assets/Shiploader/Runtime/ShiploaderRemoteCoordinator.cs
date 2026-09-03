using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ZCJ.Shiploader
{
    [DisallowMultipleComponent]
    public sealed class ShiploaderRemoteCoordinator : MonoBehaviour
    {
        [SerializeField] private SL15BackendConfig backendConfig;
        [SerializeField] private ShiploaderRigController rig;
        [SerializeField] private ShiploaderEffectsController effects;
        [SerializeField] private HatchCoverController[] hatchCovers = Array.Empty<HatchCoverController>();
        [SerializeField] private HoldCargoVisualController[] cargoVisuals = Array.Empty<HoldCargoVisualController>();

        private readonly string clientId = $"unity-{Guid.NewGuid():N}";
        private IShiploaderApiTransport transport;
        private ShiploaderApiClient api;
        private CancellationTokenSource lifetime;
        private bool connecting;
        private bool polling;
        private bool heartbeating;
        private bool commandBusy;
        private bool connected;
        private bool hasSnapshot;
        private bool snapNextPose = true;
        private float nextPollAt;
        private float nextHeartbeatAt;
        private float nextReconnectAt;
        private float lastSnapshotAt;
        private ShiploaderPose renderedPose;
        private ShiploaderPose targetPose;
        private string pendingStartRequestId;
        private string errorCode;
        private string errorMessage;

        public bool IsConnected => connected;
        public bool IsBusy => connecting || commandBusy;
        public bool ManualControlLocked => connected && CurrentTask != null && CurrentTask.LocksManualControl;
        public string ConnectionText => connected ? "已连接" : connecting ? "正在连接" : "连接中断";
        public string ErrorCode => errorCode;
        public string ErrorMessage => errorMessage;
        public LoadingPlanEnvelopeDto LoadingPlan { get; private set; }
        public LoadingTaskDto CurrentTask { get; private set; }
        public SimulationSnapshotDto LatestSnapshot { get; private set; }

        public void Configure(
            SL15BackendConfig config,
            ShiploaderRigController rigController,
            ShiploaderEffectsController effectsController,
            HatchCoverController[] covers,
            HoldCargoVisualController[] cargoControllers)
        {
            backendConfig = config;
            rig = rigController;
            effects = effectsController;
            hatchCovers = covers ?? Array.Empty<HatchCoverController>();
            cargoVisuals = cargoControllers ?? Array.Empty<HoldCargoVisualController>();
            RebuildClient();
        }

        public void SetTransportForTests(IShiploaderApiTransport injectedTransport)
        {
            transport = injectedTransport;
            RebuildClient();
        }

        private void Awake()
        {
            if (hatchCovers == null || hatchCovers.Length == 0)
            {
                hatchCovers = FindObjectsByType<HatchCoverController>(FindObjectsSortMode.None);
            }
            if (cargoVisuals == null || cargoVisuals.Length == 0)
            {
                cargoVisuals = FindObjectsByType<HoldCargoVisualController>(FindObjectsSortMode.None);
            }
            RebuildClient();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }
            lifetime = new CancellationTokenSource();
            nextReconnectAt = Time.realtimeSinceStartup;
        }

        private void Update()
        {
            if (!Application.isPlaying || backendConfig == null || api == null)
            {
                return;
            }

            UpdateSmoothedPose();
            float now = Time.realtimeSinceStartup;
            if (!connected)
            {
                if (!connecting && now >= nextReconnectAt)
                {
                    _ = ConnectAsync();
                }
                return;
            }

            if (hasSnapshot && now - lastSnapshotAt > backendConfig.staleSnapshotThreshold)
            {
                HandleDisconnect("SNAPSHOT_STALE", "快照超过 1 秒未更新");
                return;
            }
            if (!polling && now >= nextPollAt)
            {
                _ = PollSnapshotAsync();
            }
            if (!heartbeating && now >= nextHeartbeatAt)
            {
                _ = HeartbeatAsync();
            }
        }

        private void OnDisable()
        {
            if (api != null && connected)
            {
                _ = ReleaseClockSafelyAsync();
            }
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = null;
            connected = false;
            ApplyEffects(false, false);
            if (rig != null)
            {
                rig.SetRemoteControlLocked(false);
            }
        }

        private void RebuildClient()
        {
            if (backendConfig == null)
            {
                return;
            }
            transport ??= new UnityWebRequestShiploaderTransport();
            api = new ShiploaderApiClient(backendConfig, transport);
        }

        private async Task ConnectAsync()
        {
            if (connecting || api == null || lifetime == null)
            {
                return;
            }
            connecting = true;
            try
            {
                CancellationToken token = lifetime.Token;
                await api.HealthAsync(token);
                await api.RegisterClockAsync(clientId, token);
                LoadingPlan = await api.GetLoadingPlanAsync(token);
                CurrentTask = await api.GetCurrentTaskAsync(token);
                SceneConfigDto scene = await api.GetSceneAsync(token);
                ApplySceneCovers(scene);
                SimulationSnapshotDto snapshot = await api.GetSnapshotAsync(token);
                connected = true;
                errorCode = null;
                errorMessage = null;
                snapNextPose = true;
                ApplySnapshot(snapshot);
                float now = Time.realtimeSinceStartup;
                nextPollAt = now + backendConfig.statePollInterval;
                nextHeartbeatAt = now + backendConfig.heartbeatInterval;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                SetFailure(exception);
            }
            finally
            {
                connecting = false;
            }
        }

        private async Task PollSnapshotAsync()
        {
            polling = true;
            nextPollAt = Time.realtimeSinceStartup + backendConfig.statePollInterval;
            try
            {
                SimulationSnapshotDto snapshot = await api.GetSnapshotAsync(lifetime.Token);
                ApplySnapshot(snapshot);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                SetFailure(exception);
            }
            finally
            {
                polling = false;
            }
        }

        private async Task HeartbeatAsync()
        {
            heartbeating = true;
            nextHeartbeatAt = Time.realtimeSinceStartup + backendConfig.heartbeatInterval;
            try
            {
                await api.RegisterClockAsync(clientId, lifetime.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                SetFailure(exception);
            }
            finally
            {
                heartbeating = false;
            }
        }

        private void ApplySnapshot(SimulationSnapshotDto snapshot)
        {
            if (snapshot == null)
            {
                return;
            }
            bool timeReset = LatestSnapshot != null && snapshot.simTime < LatestSnapshot.simTime;
            LatestSnapshot = snapshot;
            CurrentTask = snapshot.loadingTask;
            lastSnapshotAt = Time.realtimeSinceStartup;
            hasSnapshot = true;

            if (snapshot.TryGetPose(out ShiploaderPose pose))
            {
                targetPose = pose;
                if (snapNextPose || timeReset || rig == null)
                {
                    renderedPose = pose;
                    rig?.ApplyPose(pose);
                    snapNextPose = false;
                }
            }

            bool loading = connected && CurrentTask?.status == "loading";
            bool belt = loading && snapshot.beltState != null && snapshot.beltState.running;
            ApplyEffects(belt, belt);
            UpdateCargoVisuals(CurrentTask);
            rig?.SetRemoteControlLocked(ManualControlLocked);
        }

        private void UpdateSmoothedPose()
        {
            if (!connected || !hasSnapshot || rig == null || snapNextPose)
            {
                return;
            }
            float blend = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
            renderedPose = new ShiploaderPose(
                Mathf.Lerp(renderedPose.travel, targetPose.travel, blend),
                Mathf.Lerp(renderedPose.slew, targetPose.slew, blend),
                Mathf.Lerp(renderedPose.boomLuff, targetPose.boomLuff, blend),
                Mathf.Lerp(renderedPose.boomExtension, targetPose.boomExtension, blend),
                Mathf.Lerp(renderedPose.chuteRotate, targetPose.chuteRotate, blend));
            rig.ApplyPose(renderedPose);
        }

        private void UpdateCargoVisuals(LoadingTaskDto task)
        {
            Dictionary<string, LoadingHoldProgressDto> progress = task?.holdProgress?
                .Where(item => !string.IsNullOrEmpty(item.holdId))
                .GroupBy(item => item.holdId)
                .ToDictionary(group => group.Key, group => group.Last())
                ?? new Dictionary<string, LoadingHoldProgressDto>();
            string activeHold = task?.batchProgress?
                .FirstOrDefault(item => item.batchNo == task.currentBatchNo)?.holdId;

            foreach (HoldCargoVisualController visual in cargoVisuals)
            {
                if (visual == null || visual.VesselId != "Vessel-R")
                {
                    visual?.SetActive(false);
                    continue;
                }
                visual.SetFillRatio(
                    progress.TryGetValue(visual.HoldId, out LoadingHoldProgressDto hold)
                        ? hold.Progress
                        : 0f);
                visual.SetActive(visual.HoldId == activeHold && task != null && !task.IsTerminal);
            }
        }

        private void ApplySceneCovers(SceneConfigDto scene)
        {
            if (scene?.vessels == null)
            {
                return;
            }
            Dictionary<string, string> states = scene.vessels
                .SelectMany(vessel => vessel.holds.Select(hold => new
                {
                    Key = $"{vessel.id}/{hold.id}",
                    hold.coverState,
                }))
                .ToDictionary(item => item.Key, item => item.coverState);
            foreach (HatchCoverController hatch in hatchCovers)
            {
                string key = hatch == null ? string.Empty : $"{hatch.VesselId}/{hatch.HoldId}";
                if (hatch != null && states.TryGetValue(key, out string state))
                {
                    hatch.SetOpen(state != "closed");
                }
            }
        }

        private void ApplyEffects(bool belt, bool coal)
        {
            effects?.SetBeltRunning(belt);
            effects?.SetCoalFlow(coal);
        }

        private void HandleDisconnect(string code, string message)
        {
            connected = false;
            errorCode = code;
            errorMessage = message;
            snapNextPose = true;
            nextReconnectAt = Time.realtimeSinceStartup + backendConfig.reconnectInterval;
            ApplyEffects(false, false);
            rig?.SetRemoteControlLocked(false);
        }

        private void SetFailure(Exception exception)
        {
            if (exception is ShiploaderApiException apiException)
            {
                HandleDisconnect(apiException.ErrorCode, apiException.Message);
            }
            else
            {
                HandleDisconnect("CONNECTION_ERROR", exception.Message);
            }
        }

        public async Task StartLoadingTaskAsync()
        {
            if (commandBusy || !connected || LoadingPlan?.plan == null ||
                LoadingPlan.validation?.valid != true || CurrentTask != null)
            {
                return;
            }
            commandBusy = true;
            pendingStartRequestId ??= Guid.NewGuid().ToString();
            try
            {
                CurrentTask = await ExecuteNetworkRetryAsync(
                    token => api.StartTaskAsync(
                        pendingStartRequestId,
                        LoadingPlan.plan.planId,
                        token));
                UpdateCargoVisuals(CurrentTask);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                SetCommandError(exception);
            }
            finally
            {
                commandBusy = false;
            }
        }

        public Task PauseLoadingTaskAsync() => RunTaskCommandAsync(api.PauseTaskAsync);
        public Task ResumeLoadingTaskAsync() => RunTaskCommandAsync(api.ResumeTaskAsync);
        public Task CancelLoadingTaskAsync() => RunTaskCommandAsync(api.CancelTaskAsync);

        public async Task ResetLoadingTaskAsync()
        {
            if (commandBusy || !connected || CurrentTask == null || !CurrentTask.IsTerminal)
            {
                return;
            }
            commandBusy = true;
            try
            {
                LoadingResetResponseDto response = await api.ResetTaskAsync(
                    CurrentTask.taskId,
                    lifetime.Token);
                CurrentTask = null;
                pendingStartRequestId = null;
                snapNextPose = true;
                if (response?.snapshot != null)
                {
                    ApplySnapshot(response.snapshot);
                }
                UpdateCargoVisuals(null);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                SetCommandError(exception);
            }
            finally
            {
                commandBusy = false;
            }
        }

        public async Task SetHatchOpenAsync(HatchCoverController hatch, bool open)
        {
            if (hatch == null || ManualControlLocked || commandBusy)
            {
                return;
            }
            if (!connected)
            {
                hatch.SetOpen(open);
                return;
            }
            commandBusy = true;
            try
            {
                SceneConfigDto scene = await api.SetHatchCoverAsync(
                    hatch.VesselId,
                    hatch.HoldId,
                    open,
                    lifetime.Token);
                ApplySceneCovers(scene);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                SetCommandError(exception);
            }
            finally
            {
                commandBusy = false;
            }
        }

        private async Task RunTaskCommandAsync(
            Func<string, CancellationToken, Task<LoadingTaskDto>> command)
        {
            if (commandBusy || !connected || CurrentTask == null)
            {
                return;
            }
            commandBusy = true;
            try
            {
                CurrentTask = await command(CurrentTask.taskId, lifetime.Token);
                UpdateCargoVisuals(CurrentTask);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                SetCommandError(exception);
            }
            finally
            {
                commandBusy = false;
            }
        }

        private async Task<T> ExecuteNetworkRetryAsync<T>(
            Func<CancellationToken, Task<T>> operation)
        {
            try
            {
                return await operation(lifetime.Token);
            }
            catch (ShiploaderApiException exception) when (exception.ErrorCode == "CONNECTION_ERROR")
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(backendConfig.reconnectInterval),
                    lifetime.Token);
                return await operation(lifetime.Token);
            }
        }

        private void SetCommandError(Exception exception)
        {
            errorCode = exception is ShiploaderApiException apiException
                ? apiException.ErrorCode
                : "COMMAND_ERROR";
            errorMessage = exception.Message;
            if (errorCode == "CONNECTION_ERROR")
            {
                HandleDisconnect(errorCode, errorMessage);
            }
        }

        private async Task ReleaseClockSafelyAsync()
        {
            try
            {
                await api.ReleaseClockAsync(clientId, CancellationToken.None);
            }
            catch
            {
                // The five-second server lease is the fallback when shutdown cannot send DELETE.
            }
        }
    }
}
