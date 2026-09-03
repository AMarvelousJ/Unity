using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ZCJ.Shiploader.Tests
{
    public sealed class SL15PlayModeTests
    {
        [UnityTest]
        public IEnumerator RigPoseAndCameraPresetsWorkInPlayMode()
        {
            SL15ModelConfig config = ScriptableObject.CreateInstance<SL15ModelConfig>();
            config.ResetToWebDefaults();

            GameObject root = new("TestRig");
            Transform upper = Child(root.transform, "Upper", new Vector3(0f, config.baseHeight, 0f));
            Transform luff = Child(upper, "Luff", new Vector3(0f, config.boomBaseHeightOffset, 0f));
            Transform telescope = Child(luff, "Telescope", new Vector3(0f, 0f, config.fixedBoomLength - config.telescopicOverlap));
            Transform head = Child(luff, "Head", new Vector3(0f, 0f, config.fixedBoomLength));
            Transform chute = Child(root.transform, "Chute", Vector3.zero);
            Transform yaw = Child(chute, "Yaw", Vector3.zero);
            Transform rope = Child(upper, "Rope", Vector3.zero);
            Transform mast = Child(upper, "Mast", new Vector3(0f, 8f, -1.2f));
            Transform slewCenter = Child(root.transform, "SlewCenter", new Vector3(0f, config.baseHeight, 0f));
            Transform boomRoot = Child(luff, "BoomRoot", Vector3.zero);
            Transform fixedTip = Child(luff, "FixedTip", new Vector3(0f, 0f, config.fixedBoomLength));
            Transform boomTip = Child(head, "BoomTip", Vector3.zero);
            Transform chuteTop = Child(chute, "ChuteTop", Vector3.zero);
            Transform chuteBottom = Child(chute, "ChuteBottom", new Vector3(0f, -config.chuteLength, 0f));
            Transform discharge = Child(chute, "Discharge", new Vector3(0f, -config.chuteLength - config.dischargeDrop, 0f));

            ShiploaderRigController rig = root.AddComponent<ShiploaderRigController>();
            rig.Configure(config, upper, luff, telescope, head, chute, yaw, rope, mast,
                slewCenter, boomRoot, fixedTip, boomTip, chuteTop, chuteBottom, discharge);
            rig.ApplyPose(new ShiploaderPose(25f, 90f, 24f, 21.25f, 120f));
            yield return null;

            Assert.That(rig.GetMaximumKeyPointError(), Is.LessThanOrEqualTo(0.01f));
            Assert.That(Vector3.Dot(chute.up, Vector3.up), Is.GreaterThan(0.9999f));

            GameObject targetObject = new("CameraTarget");
            GameObject cameraObject = new("Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            ShiploaderOrbitCamera orbit = cameraObject.AddComponent<ShiploaderOrbitCamera>();
            orbit.Configure(targetObject.transform);
            orbit.SetPreset(ShiploaderCameraPreset.Side);
            yield return null;
            Assert.That(camera.orthographic, Is.True);
            orbit.SetPreset(ShiploaderCameraPreset.Perspective);
            yield return null;
            Assert.That(camera.orthographic, Is.False);

            Object.Destroy(root);
            Object.Destroy(targetObject);
            Object.Destroy(cameraObject);
            Object.Destroy(config);
        }

        [UnityTest]
        public IEnumerator HatchAndEffectsControllersApplyRequestedState()
        {
            GameObject coverRoot = new("CoverRoot");
            Transform left = Child(coverRoot.transform, "Left", Vector3.zero);
            Transform right = Child(coverRoot.transform, "Right", Vector3.zero);
            HatchCoverController hatch = coverRoot.AddComponent<HatchCoverController>();
            Vector3[] closed = { new(0f, 0f, -1f), new(0f, 0f, 1f) };
            Vector3[] opened = { new(0f, 0f, -3f), new(0f, 0f, 3f) };
            Vector3[] rotations = { Vector3.zero, Vector3.zero };
            hatch.Configure("Vessel-T", "T1", new[] { left, right }, closed, rotations, opened, rotations, true);
            Assert.That(hatch.IsOpen, Is.True);
            Assert.That(left.localPosition.z, Is.EqualTo(-3f).Within(0.0001f));
            hatch.SetOpen(false, true);
            Assert.That(right.localPosition.z, Is.EqualTo(1f).Within(0.0001f));

            GameObject effectsObject = new("Effects");
            GameObject belt = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ParticleSystem particles = new GameObject("Particles").AddComponent<ParticleSystem>();
            ShiploaderEffectsController effects = effectsObject.AddComponent<ShiploaderEffectsController>();
            effects.Configure(new[] { belt.GetComponent<Renderer>() }, particles);
            effects.SetBeltRunning(true);
            effects.SetCoalFlow(true);
            yield return null;
            Assert.That(effects.BeltRunning, Is.True);
            Assert.That(effects.CoalFlowEnabled, Is.True);
            Assert.That(particles.isPlaying, Is.True);

            effects.SetCoalFlow(false);
            yield return null;
            Assert.That(particles.isPlaying, Is.False);

            Object.Destroy(coverRoot);
            Object.Destroy(effectsObject);
            Object.Destroy(belt);
            Object.Destroy(particles.gameObject);
        }

        [UnityTest]
        public IEnumerator RemoteCoordinatorReconnectsAndReusesStartRequestId()
        {
            SL15BackendConfig backend = ScriptableObject.CreateInstance<SL15BackendConfig>();
            backend.ResetToDefaults();
            backend.reconnectInterval = 0.02f;
            backend.statePollInterval = 100f;
            backend.heartbeatInterval = 100f;
            backend.staleSnapshotThreshold = 100f;

            FakeShiploaderTransport transport = new() { FailFirstHealth = true, FailFirstStart = true };
            GameObject bridgeObject = new("RemoteCoordinatorTest");
            ShiploaderRemoteCoordinator coordinator = bridgeObject.AddComponent<ShiploaderRemoteCoordinator>();
            coordinator.Configure(backend, null, null, null, null);
            coordinator.SetTransportForTests(transport);

            float deadline = Time.realtimeSinceStartup + 2f;
            while (!coordinator.IsConnected && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.That(coordinator.IsConnected, Is.True, coordinator.ErrorMessage);
            Assert.That(coordinator.LoadingPlan.validation.valid, Is.True);

            Task start = coordinator.StartLoadingTaskAsync();
            while (!start.IsCompleted && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.That(start.IsCompletedSuccessfully, Is.True);
            Assert.That(transport.StartBodies, Has.Count.EqualTo(2));
            string firstRequestId = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                transport.StartBodies[0])["request_id"];
            string retriedRequestId = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                transport.StartBodies[1])["request_id"];
            Assert.That(retriedRequestId, Is.EqualTo(firstRequestId));
            Assert.That(coordinator.CurrentTask.status, Is.EqualTo("positioning"));
            Assert.That(coordinator.ManualControlLocked, Is.True);

            Task pause = coordinator.PauseLoadingTaskAsync();
            while (!pause.IsCompleted) yield return null;
            Assert.That(coordinator.CurrentTask.status, Is.EqualTo("paused"));
            Task resume = coordinator.ResumeLoadingTaskAsync();
            while (!resume.IsCompleted) yield return null;
            Assert.That(coordinator.CurrentTask.status, Is.EqualTo("positioning"));
            Task cancel = coordinator.CancelLoadingTaskAsync();
            while (!cancel.IsCompleted) yield return null;
            Assert.That(coordinator.CurrentTask.status, Is.EqualTo("cancelled"));
            Assert.That(coordinator.ManualControlLocked, Is.False);
            Task reset = coordinator.ResetLoadingTaskAsync();
            while (!reset.IsCompleted) yield return null;
            Assert.That(coordinator.CurrentTask, Is.Null);

            Object.Destroy(bridgeObject);
            Object.Destroy(backend);
        }

        private sealed class FakeShiploaderTransport : IShiploaderApiTransport
        {
            public bool FailFirstHealth;
            public bool FailFirstStart;
            public readonly List<string> StartBodies = new();
            private string taskStatus;

            public Task<ShiploaderHttpResponse> SendAsync(
                string method,
                string url,
                string jsonBody,
                int timeoutSeconds,
                CancellationToken cancellationToken)
            {
                if (url.EndsWith("/health"))
                {
                    if (FailFirstHealth)
                    {
                        FailFirstHealth = false;
                        return Reply(0, string.Empty, "server unavailable");
                    }
                    return Json(new { status = "ok" });
                }
                if (url.Contains("/api/simulation/clock/clients/"))
                {
                    return method == "DELETE"
                        ? Reply(204, string.Empty)
                        : Json(new SimulationClockStatusDto { running = true, activeClients = 1, stepS = 0.1f });
                }
                if (url.EndsWith("/api/loading-plans/current"))
                {
                    return Json(new LoadingPlanEnvelopeDto
                    {
                        plan = new LoadingPlanDto
                        {
                            planId = "default-plan",
                            vesselId = "Vessel-R",
                            vesselName = "华元503",
                            name = "默认演示计划",
                            totalTargetKg = 52_100_000,
                        },
                        validation = new LoadingPlanValidationDto
                        {
                            valid = true,
                            calculatedTotalKg = 52_100_000,
                        },
                    });
                }
                if (url.EndsWith("/api/loading-tasks/current"))
                {
                    return Reply(200, "null");
                }
                if (url.EndsWith("/api/scene"))
                {
                    return Json(new SceneConfigDto());
                }
                if (url.EndsWith("/api/state"))
                {
                    return Json(Snapshot(null));
                }
                if (url.EndsWith("/api/loading-tasks") && method == "POST")
                {
                    StartBodies.Add(jsonBody);
                    if (FailFirstStart)
                    {
                        FailFirstStart = false;
                        return Reply(0, string.Empty, "connection lost after send");
                    }
                    taskStatus = "positioning";
                    return Json(TaskDto(taskStatus));
                }
                if (url.EndsWith("/pause"))
                {
                    taskStatus = "paused";
                    return Json(TaskDto(taskStatus));
                }
                if (url.EndsWith("/resume"))
                {
                    taskStatus = "positioning";
                    return Json(TaskDto(taskStatus));
                }
                if (url.EndsWith("/cancel"))
                {
                    taskStatus = "cancelled";
                    return Json(TaskDto(taskStatus));
                }
                if (url.EndsWith("/reset"))
                {
                    return Json(new LoadingResetResponseDto
                    {
                        task = TaskDto("cancelled"),
                        snapshot = Snapshot(null),
                    });
                }
                return Reply(404, "{\"detail\":{\"code\":\"NOT_FOUND\",\"message\":\"missing fake route\"}}");
            }

            private static LoadingTaskDto TaskDto(string status)
            {
                return new LoadingTaskDto
                {
                    taskId = "task-1",
                    planId = "default-plan",
                    mode = "demo",
                    status = status,
                    currentRoundNo = 1,
                    currentBatchNo = 1,
                    currentPhase = status,
                    targetTotalKg = 52_100_000,
                    batchProgress = new List<LoadingBatchProgressDto>(),
                    holdProgress = new List<LoadingHoldProgressDto>(),
                };
            }

            private static SimulationSnapshotDto Snapshot(LoadingTaskDto task)
            {
                Dictionary<string, ShiploaderAxisStateDto> axes = new();
                foreach (string axis in new[] { "travel", "slew", "boom_luff", "boom_extension", "chute_rotate" })
                {
                    axes[axis] = new ShiploaderAxisStateDto();
                }
                return new SimulationSnapshotDto
                {
                    axisStates = axes,
                    beltState = new ShiploaderBeltStateDto(),
                    loadingTask = task,
                };
            }

            private static Task<ShiploaderHttpResponse> Json(object value) =>
                Reply(200, JsonConvert.SerializeObject(value));

            private static Task<ShiploaderHttpResponse> Reply(
                long status,
                string body,
                string error = null) =>
                Task.FromResult(new ShiploaderHttpResponse(status, body, error));
        }

        private static Transform Child(Transform parent, string name, Vector3 localPosition)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            return child.transform;
        }
    }
}
