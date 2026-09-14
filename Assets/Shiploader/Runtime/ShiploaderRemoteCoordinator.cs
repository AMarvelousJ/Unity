using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json.Linq;

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
        [SerializeField] private string machineId;
        [SerializeField, TextArea] private string fleetSceneJson;
        private string manualSession;
        private int manualSequence;
        private bool jogging;
        public string MachineId => machineId;
        public ShiploaderRigController Rig => rig;
        public ShiploaderEffectsController Effects => effects;
        public HatchCoverController[] Covers => hatchCovers;
        public string SelectedVessel { get; private set; }
        public bool IsFleet => !string.IsNullOrEmpty(machineId);
        public bool IsManual => connected && manualSession != null;
        public bool NeedsSide => IsFleet && int.Parse(machineId) <= 3 && string.IsNullOrEmpty(SelectedVessel);
        public void ConfigureFleet(string id, string sceneJson) { machineId = id; fleetSceneJson = sceneJson; RebuildClient(); }

        private readonly string clientId = $"unity-{Guid.NewGuid():N}";
        private IShiploaderApiTransport transport;
        private ShiploaderApiClient api;
        private CancellationTokenSource lifetime;
        private bool connecting;
        private bool polling;
        private bool heartbeating;
        private bool commandBusy;
        private int stateVersion;
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
        public bool ManualControlLocked => IsFleet ? !IsManual : connected && CurrentTask != null && CurrentTask.LocksManualControl;
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
            if (!polling && !commandBusy && now >= nextPollAt)
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
            api = new ShiploaderApiClient(backendConfig, transport, IsFleet ? "/api/fleet/" + machineId : "");
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
                if (IsFleet)
                {
                    await api.SendAsync<JObject>("PUT", "/api/fleet-configuration", JObject.Parse(fleetSceneJson), token);
                    var selection = await api.SendAsync<JObject>("GET", "/api/selection", null, token);
                    SelectedVessel = selection.Value<string>("vessel_id");
                }
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
            int version = stateVersion;
            nextPollAt = Time.realtimeSinceStartup + backendConfig.statePollInterval;
            try
            {
                SimulationSnapshotDto snapshot = await api.GetSnapshotAsync(lifetime.Token);
                if (!commandBusy && version == stateVersion) ApplySnapshot(snapshot);
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
            if (IsFleet && snapshot.fleetConfigured == false)
            {
                HandleDisconnect("BACKEND_RESTARTED", "仿真服务已重启，正在重新登记设备");
                return;
            }
            if (IsFleet && snapshot.fleetConfigured == true) SelectedVessel = snapshot.selectedVessel;
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
                if (visual == null || visual.VesselId != (IsFleet ? SelectedVessel : "Vessel-R"))
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
            manualSession = null;
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
                LoadingPlan.validation?.valid != true || CurrentTask != null || NeedsSide)
            {
                return;
            }
            commandBusy = true; ++stateVersion;
            manualSession = null;
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
        public Task ResumeLoadingTaskAsync() { manualSession = null; return RunTaskCommandAsync(api.ResumeTaskAsync); }
        public Task CancelLoadingTaskAsync() => RunTaskCommandAsync(api.CancelTaskAsync);

        public async Task ResetLoadingTaskAsync()
        {
            if (commandBusy || !connected || CurrentTask == null || !CurrentTask.IsTerminal)
            {
                return;
            }
            commandBusy = true; ++stateVersion;
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
            commandBusy = true; ++stateVersion;
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
            commandBusy = true; ++stateVersion;
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

        public async Task SelectVesselAsync(string vessel)
        {
            if (!IsFleet || !connected || IsBusy || CurrentTask != null) return;
            commandBusy = true; ++stateVersion;
            try {
                await api.SendAsync<JObject>("POST", "/api/selection", new { vessel_id = vessel }, lifetime.Token);
                SelectedVessel = vessel;
                LoadingPlan = await api.GetLoadingPlanAsync(lifetime.Token);
                errorCode = errorMessage = null;
            } catch (Exception e) when (e is not OperationCanceledException) { SetCommandError(e); }
            finally { commandBusy = false; }
        }

        private int manualGeneration;
        public async Task EnterManualAsync()
        {
            if (!IsFleet || !connected || IsBusy) return;
            int generation = ++manualGeneration;
            commandBusy = true; ++stateVersion;
            try {
                var result = await api.SendAsync<JObject>("POST", "/api/manual/enter", null, lifetime.Token);
                if (generation != manualGeneration) {
                    await api.SendAsync<JObject>("POST", "/api/manual/stop", new { session = result.Value<string>("session") }, lifetime.Token);
                    return;
                }
                manualSession = result.Value<string>("session"); manualSequence = 0;
                CurrentTask = await api.GetCurrentTaskAsync(lifetime.Token);
                errorCode = errorMessage = null;
            } catch (Exception e) when (e is not OperationCanceledException) { SetCommandError(e); }
            finally { commandBusy = false; }
        }

        public async Task StopManualAsync()
        {
            ++manualGeneration;
            string session = manualSession;
            manualSession = null;
            if (!connected || !IsFleet || session == null) return;
            try { await api.SendAsync<JObject>("POST", "/api/manual/stop", new { session }, lifetime.Token); }
            catch (Exception e) when (e is not OperationCanceledException) { SetCommandError(e); }
        }

        public async Task JogAsync(float travel, float slew, float luff, float extension, float chute)
        {
            if (!IsManual || jogging || IsBusy) return;
            jogging = true;
            string session = manualSession;
            try {
                await api.SendAsync<SimulationSnapshotDto>("POST", "/api/manual/jog", new {
                    session, sequence = ++manualSequence,
                    axes = new { travel, slew, boom_luff = luff, boom_extension = extension, chute_rotate = chute }
                }, lifetime.Token);
            } catch (Exception e) when (e is not OperationCanceledException) {
                if (session == manualSession) { manualSession = null; SetCommandError(e); }
            } finally { jogging = false; }
        }
    }
}
