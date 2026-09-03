using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZCJ.Shiploader.Editor;
using Newtonsoft.Json;
using Object = UnityEngine.Object;

namespace ZCJ.Shiploader.Tests
{
    public sealed class SL15RigTests
    {
        private ShiploaderRigController rig;
        private SL15ModelConfig config;

        [SetUp]
        public void SetUp()
        {
            if (SceneManager.GetActiveScene().path != SL15SceneBuilder.ScenePath)
            {
                EditorSceneManager.OpenScene(SL15SceneBuilder.ScenePath, OpenSceneMode.Single);
            }

            config = AssetDatabase.LoadAssetAtPath<SL15ModelConfig>(SL15SceneBuilder.ConfigPath);
            rig = Object.FindFirstObjectByType<ShiploaderRigController>();
            Assert.That(config, Is.Not.Null);
            Assert.That(rig, Is.Not.Null);
            rig.ResetPose();
        }

        [Test]
        public void ConfigurationMatchesWebEngineeringDimensions()
        {
            Assert.That(config.railGauge, Is.EqualTo(23f).Within(0.0001f));
            Assert.That(config.wheelBase, Is.EqualTo(22f).Within(0.0001f));
            Assert.That(config.baseHeight, Is.EqualTo(12f).Within(0.0001f));
            Assert.That(config.fixedBoomLength, Is.EqualTo(16.5f).Within(0.0001f));
            Assert.That(config.boomExtensionTravel, Is.EqualTo(21.25f).Within(0.0001f));
            Assert.That(config.chuteLength, Is.EqualTo(12.6f).Within(0.0001f));
            Assert.That(config.slewRange.min, Is.EqualTo(-35f).Within(0.0001f));
            Assert.That(config.slewRange.max, Is.EqualTo(215f).Within(0.0001f));
            Assert.That(config.luffRange.min, Is.EqualTo(-10f).Within(0.0001f));
            Assert.That(config.luffRange.max, Is.EqualTo(24f).Within(0.0001f));
        }

        [Test]
        public void SceneContainsUniqueMotionContractAndExpectedHolds()
        {
            Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            string[] requiredNames =
            {
                "SL_ShipLoaderRoot",
                "SL_TravelAssembly",
                "SL_UpperSlewAssembly",
                "SL_BoomLuffPivot",
                "SL_FixedBoomAssembly",
                "SL_TelescopicBoomAssembly",
                "SL_BoomHeadAttachment",
                "SL_ChuteAssembly",
                "SL_ChuteYawAssembly",
                "MK_SLEW_CENTER",
                "MK_BOOM_ROOT",
                "MK_FIXED_BOOM_TIP",
                "MK_BOOM_TIP",
                "MK_CHUTE_TOP",
                "MK_CHUTE_BOTTOM",
                "MK_DISCHARGE",
            };

            foreach (string requiredName in requiredNames)
            {
                Assert.That(transforms.Count(item => item.name == requiredName), Is.EqualTo(1), requiredName);
            }

            HatchCoverController[] hatches = Object.FindObjectsByType<HatchCoverController>(FindObjectsSortMode.None);
            Assert.That(hatches.Count(item => item.VesselId == "Vessel-L"), Is.EqualTo(4));
            Assert.That(hatches.Count(item => item.VesselId == "Vessel-R"), Is.EqualTo(5));

            ShiploaderRemoteCoordinator[] coordinators =
                Object.FindObjectsByType<ShiploaderRemoteCoordinator>(FindObjectsSortMode.None);
            HoldCargoVisualController[] cargo =
                Object.FindObjectsByType<HoldCargoVisualController>(FindObjectsSortMode.None);
            Assert.That(coordinators, Has.Length.EqualTo(1));
            Assert.That(cargo.Count(item => item.VesselId == "Vessel-L"), Is.EqualTo(4));
            Assert.That(cargo.Count(item => item.VesselId == "Vessel-R"), Is.EqualTo(5));
        }

        [Test]
        public void VesselsUseGeneralArrangementVisualLanguageWithoutMovingSimulationAnchors()
        {
            GameObject left = GameObject.Find("Vessel-L");
            GameObject right = GameObject.Find("Vessel-R");
            Assert.That(left, Is.Not.Null);
            Assert.That(right, Is.Not.Null);
            Assert.That(left.transform.position, Is.EqualTo(new Vector3(10f, 1.6f, -18f)));
            Assert.That(right.transform.position, Is.EqualTo(new Vector3(12f, 1.6f, 24f)));

            foreach (GameObject vessel in new[] { left, right })
            {
                Assert.That(vessel.transform.Find("VesselVisualDetails/ForecastleAssembly"), Is.Not.Null);
                Assert.That(vessel.transform.Find("VesselVisualDetails/AftAccommodationAssembly"), Is.Not.Null);
                Assert.That(vessel.transform.Find("VesselVisualDetails/DeckEquipment"), Is.Not.Null);
                Assert.That(vessel.transform.Find("VesselVisualDetails/SideWalkwaysAndMarkings"), Is.Not.Null);
                Assert.That(vessel.GetComponentsInChildren<Transform>(true).Count(item => item.name == "Lifeboat"),
                    Is.EqualTo(2));

                MeshRenderer hull = vessel.transform.Find("Hull").GetComponent<MeshRenderer>();
                Assert.That(hull.sharedMaterials, Has.Length.EqualTo(2));
                Assert.That(hull.sharedMaterials[0].name, Is.EqualTo("Ship_Antifouling"));
                Assert.That(hull.sharedMaterials[1].name, Is.EqualTo("Ship_Hull"));
            }

            Transform leftCranes = left.transform.Find("VesselVisualDetails/DeckCranes");
            Transform rightCranes = right.transform.Find("VesselVisualDetails/DeckCranes");
            Assert.That(leftCranes.childCount, Is.EqualTo(3));
            Assert.That(rightCranes.childCount, Is.EqualTo(4));

            float firstHatchLength = right.transform.Find("Hold_R1/Coaming_Port").localScale.x;
            float standardHatchLength = right.transform.Find("Hold_R2/Coaming_Port").localScale.x;
            Assert.That(firstHatchLength, Is.LessThan(standardHatchLength));
        }

        [Test]
        public void BackendConfigurationUsesApprovedLocalDefaults()
        {
            SL15BackendConfig backend =
                AssetDatabase.LoadAssetAtPath<SL15BackendConfig>(SL15SceneBuilder.BackendConfigPath);
            Assert.That(backend, Is.Not.Null);
            Assert.That(backend.baseUrl, Is.EqualTo("http://127.0.0.1:8000"));
            Assert.That(backend.statePollInterval, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(backend.heartbeatInterval, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(backend.requestTimeout, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(backend.reconnectInterval, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(backend.staleSnapshotThreshold, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void SnapshotJsonMapsFiveBackendAxesToUnityPose()
        {
            const string json = "{\"sim_time\":1.2,\"sim_paused\":false," +
                                "\"axis_states\":{" +
                                "\"travel\":{\"current\":12.5}," +
                                "\"slew\":{\"current\":45.0}," +
                                "\"boom_luff\":{\"current\":18.0}," +
                                "\"boom_extension\":{\"current\":9.25}," +
                                "\"chute_rotate\":{\"current\":-30.0}}," +
                                "\"belt_state\":{\"running\":false}}";
            SimulationSnapshotDto snapshot = JsonConvert.DeserializeObject<SimulationSnapshotDto>(json);
            Assert.That(snapshot.TryGetPose(out ShiploaderPose pose), Is.True);
            Assert.That(pose.travel, Is.EqualTo(12.5f));
            Assert.That(pose.slew, Is.EqualTo(45f));
            Assert.That(pose.boomLuff, Is.EqualTo(18f));
            Assert.That(pose.boomExtension, Is.EqualTo(9.25f));
            Assert.That(pose.chuteRotate, Is.EqualTo(-30f));
        }

        [Test]
        public void TwelveBatchContractAndHoldProgressDeserialize()
        {
            LoadingTaskDto task = new()
            {
                taskId = "task-test",
                status = "loading",
                targetTotalKg = 52_100_000,
                loadedTotalKg = 26_050_000,
                batchProgress = Enumerable.Range(1, 12)
                    .Select(index => new LoadingBatchProgressDto
                    {
                        batchNo = index,
                        roundNo = index <= 5 ? 1 : index <= 10 ? 2 : 3,
                        holdId = $"R{(index - 1) % 5 + 1}",
                        targetKg = 100,
                        loadedKg = index == 1 ? 100 : 0,
                        status = index == 1 ? "completed" : "pending",
                    })
                    .ToList(),
            };
            string json = JsonConvert.SerializeObject(task);
            LoadingTaskDto restored = JsonConvert.DeserializeObject<LoadingTaskDto>(json);
            Assert.That(restored.batchProgress, Has.Count.EqualTo(12));
            Assert.That(restored.targetTotalKg, Is.EqualTo(52_100_000));
            Assert.That(restored.Progress, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void CargoSurfaceScalesFromBottomAndKeepsPartialFill()
        {
            GameObject hold = new("CargoTestHold");
            GameObject coal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            coal.transform.SetParent(hold.transform, false);
            coal.transform.localScale = new Vector3(8f, 0.2f, 5f);
            coal.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            HoldCargoVisualController cargo = hold.AddComponent<HoldCargoVisualController>();
            cargo.Configure("Vessel-R", "R1", coal.transform, Array.Empty<Renderer>(), 1.4f, 0f);
            cargo.SetFillRatio(0.25f);
            Assert.That(cargo.FillRatio, Is.EqualTo(0.25f));
            Assert.That(coal.transform.localScale.y, Is.EqualTo(0.35f).Within(0.0001f));
            Assert.That(coal.transform.localPosition.y, Is.EqualTo(0.195f).Within(0.0001f));
            Object.DestroyImmediate(hold);
        }

        [TestCase(0f, 0f, 10f, 0f, 0f)]
        [TestCase(-100f, -35f, -10f, 0f, -180f)]
        [TestCase(100f, 215f, 24f, 21.25f, 180f)]
        [TestCase(37f, 90f, 0f, 10f, 45f)]
        public void KeyPointsMatchAnalyticKinematics(
            float travel,
            float slew,
            float luff,
            float extension,
            float chuteRotate)
        {
            rig.ApplyPose(new ShiploaderPose(travel, slew, luff, extension, chuteRotate));
            Assert.That(rig.GetMaximumKeyPointError(), Is.LessThanOrEqualTo(0.01f));

            Transform chute = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Single(item => item.name == "SL_ChuteAssembly");
            Assert.That(Vector3.Dot(chute.up, Vector3.up), Is.GreaterThan(0.9999f));
        }

        [Test]
        public void PoseIsClampedToEngineeringRanges()
        {
            rig.ApplyPose(new ShiploaderPose(-999f, 999f, -999f, 999f, -999f));
            ShiploaderPose pose = rig.Pose;
            Assert.That(pose.travel, Is.EqualTo(config.travelRange.min));
            Assert.That(pose.slew, Is.EqualTo(config.slewRange.max));
            Assert.That(pose.boomLuff, Is.EqualTo(config.luffRange.min));
            Assert.That(pose.boomExtension, Is.EqualTo(config.extensionRange.max));
            Assert.That(pose.chuteRotate, Is.EqualTo(config.chuteRotateRange.min));
        }

        [Test]
        public void PoseUpdatesDoNotRebuildGeometryOrMaterials()
        {
            int meshCount = rig.GetComponentsInChildren<MeshFilter>(true).Length;
            int rendererCount = rig.GetComponentsInChildren<Renderer>(true).Length;
            int distinctMaterials = rig.GetComponentsInChildren<Renderer>(true)
                .Select(item => item.sharedMaterial)
                .Where(item => item != null)
                .Distinct()
                .Count();

            rig.ApplyPose(new ShiploaderPose(50f, 120f, 24f, 21.25f, 180f));
            rig.ApplyPose(new ShiploaderPose(-50f, -35f, -10f, 0f, -180f));
            rig.ResetPose();

            Assert.That(rig.GetComponentsInChildren<MeshFilter>(true).Length, Is.EqualTo(meshCount));
            Assert.That(rig.GetComponentsInChildren<Renderer>(true).Length, Is.EqualTo(rendererCount));
            Assert.That(rig.GetComponentsInChildren<Renderer>(true)
                .Select(item => item.sharedMaterial)
                .Where(item => item != null)
                .Distinct()
                .Count(), Is.EqualTo(distinctMaterials));
        }

        [Test]
        public void SceneGeneratorIsIdempotentForRemoteBridgeAndRequiredNodes()
        {
            SL15SceneBuilder.RebuildDemoScene();
            SL15SceneBuilder.RebuildDemoScene();

            Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            Assert.That(transforms.Count(item => item.name == "SL_ShipLoaderRoot"), Is.EqualTo(1));
            Assert.That(transforms.Count(item => item.name == "SL15_BackendBridge"), Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<ShiploaderRemoteCoordinator>(FindObjectsSortMode.None),
                Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<HoldCargoVisualController>(FindObjectsSortMode.None),
                Has.Length.EqualTo(9));
        }
    }
}
