using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ZCJ.Shiploader.Editor
{
    public static partial class SL15ReferenceModel
    {
        [MenuItem("Tools/SL15/Enable Seven Machine Fleet")]
        public static void EnableSevenMachineFleet()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first");
            string original = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            string backup = "Temp/fleet/backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(backup);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            foreach (string path in new[] { SL15SceneBuilder.ScenePath, "Assets/Shiploader/Scenes/SL15_VideoPreview.unity" })
                File.Copy(path, backup + "/" + Path.GetFileName(path));
            try {
                foreach (string path in new[] { SL15SceneBuilder.ScenePath, "Assets/Shiploader/Scenes/SL15_VideoPreview.unity" }) {
                    var scene = EditorSceneManager.OpenScene(path);
                    BuildFleetInScene();
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                }
                AssetDatabase.SaveAssets();
            } finally { EditorSceneManager.OpenScene(original); }
            Debug.Log("Seven-machine fleet saved in both scenes. Backup: " + backup);
        }

        static void BuildFleetInScene()
        {
            var source = GameObject.Find("SL_ShipLoaderRoot");
            var rig = source.GetComponent<ShiploaderRigController>();
            var originalPose = rig.Pose;
            rig.ApplyPose(new ShiploaderPose(0, 0, 10, 0, 0));
            var originals = new[] { GameObject.Find("Vessel-R"), GameObject.Find("Vessel-L") };
            var oldCoordinator = UnityEngine.Object.FindFirstObjectByType<ShiploaderRemoteCoordinator>();
            if (oldCoordinator == null) throw new InvalidOperationException("Missing backend configuration");
            var backend = (SL15BackendConfig)new SerializedObject(oldCoordinator).FindProperty("backendConfig").objectReferenceValue;
            var ui = UnityEngine.Object.FindFirstObjectByType<ShiploaderDemoUI>();
            var orbit = UnityEngine.Object.FindFirstObjectByType<ShiploaderOrbitCamera>();
            // Dedicated coordinators contain only the assigned vessel controllers.
            foreach (var c in UnityEngine.Object.FindObjectsByType<ShiploaderRemoteCoordinator>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(c);
            var district = GameObject.Find("PortEnvironment").transform.Find("AerialReferencePort");
            var rigs = new List<ShiploaderRigController> { rig };
            var vessels = new List<GameObject[]> { originals };
            for (int n = 2; n <= 3; n++) {
                var parent = GameObject.Find("AdditionalBerthModules").transform.Find("Berth_0" + n);
                var copy = ReplaceFleetObject(source, parent.Find("Shiploader_0" + n));
                InitQuayMaterials(); OffsetReceivingConveyor(copy, TripleLanes[n-1], n);
                rigs.Add(copy.GetComponent<ShiploaderRigController>());
                vessels.Add(originals.Select(v => ReplaceFleetObject(v, parent.Find(v.name + "_0" + n))).ToArray());
            }
            for (int pier = 2; pier <= 3; pier++) for (int slot = 0; slot < 2; slot++) {
                var parent = district.Find("DisplayPier_0" + pier);
                var copy = ReplaceFleetObject(source, parent.Find("DisplayShiploader_" + slot));
                foreach (var t in copy.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.name == FixedTurntableName || t.name == MovingTurntableName).ToArray())
                    UnityEngine.Object.DestroyImmediate(t.gameObject);
                rigs.Add(copy.GetComponent<ShiploaderRigController>());
                var vessel = originals[pier == 2 ? 0 : 1];
                vessels.Add(new[] { ReplaceFleetObject(vessel, parent.Find(vessel.name + "_Display_" + slot)) });
            }
            var coordinators = new List<ShiploaderRemoteCoordinator>();
            var configurations = new JArray();
            for (int index = 0; index < 7; index++) {
                var item = rigs[index];
                // Constrain each machine to its own rail section, maintaining clearance
                // between the three upper machines even during parallel manual travel.
                float min = index < 3 ? -45 : -75, max = index < 3 ? 50 : 75;
                string assetPath = "Assets/Shiploader/Generated/FleetModel_" + (index+1).ToString("00") + "_" +
                    Path.GetFileNameWithoutExtension(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path) + ".asset";
                var config = AssetDatabase.LoadAssetAtPath<SL15ModelConfig>(assetPath);
                if (config == null) { config = UnityEngine.Object.Instantiate(item.Config); AssetDatabase.CreateAsset(config, assetPath); }
                else EditorUtility.CopySerialized(item.Config, config);
                config.travelRange = new AxisRange(min, max); EditorUtility.SetDirty(config);
                var so = new SerializedObject(item);
                so.FindProperty("config").objectReferenceValue = config;
                so.FindProperty("rootBasePosition").vector3Value = item.transform.localPosition - Vector3.right * item.Pose.travel;
                so.ApplyModifiedPropertiesWithoutUndo();
                var coordinator = item.gameObject.AddComponent<ShiploaderRemoteCoordinator>();
                coordinator.Configure(backend, item, item.GetComponent<ShiploaderEffectsController>(),
                    vessels[index].SelectMany(v => v.GetComponentsInChildren<HatchCoverController>(true)).ToArray(),
                    vessels[index].SelectMany(v => v.GetComponentsInChildren<HoldCargoVisualController>(true)).ToArray());
                var configuration = FleetSceneConfig(item, vessels[index], min, max);
                coordinator.ConfigureFleet((index + 1).ToString("00"), configuration.ToString());
                coordinators.Add(coordinator); configurations.Add(configuration);
                item.ApplyPose(index >= 5 ? new ShiploaderPose(0, 180, 10, 0, 0) : new ShiploaderPose(0, 0, 10, 0, 0));
                EditorUtility.SetDirty(item); EditorUtility.SetDirty(coordinator);
            }
            rig.ApplyPose(originalPose);
            var fleet = ui.GetComponent<ShiploaderFleetController>() ?? ui.gameObject.AddComponent<ShiploaderFleetController>();
            fleet.Configure(coordinators.ToArray(), ui, orbit);
            ui.Configure(rig, rig.GetComponent<ShiploaderEffectsController>(), orbit, coordinators[0]);
            EditorUtility.SetDirty(fleet); EditorUtility.SetDirty(ui);
            Directory.CreateDirectory("../outputs");
            File.WriteAllText("../outputs/fleet-" + Path.GetFileNameWithoutExtension(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path) + ".json", configurations.ToString());
        }

        static GameObject ReplaceFleetObject(GameObject source, Transform old)
        {
            if (old == null) throw new InvalidOperationException("Missing fleet object for " + source.name);
            var parent = old.parent; var position = old.localPosition; var rotation = old.localRotation;
            var scale = old.localScale; string name = old.name;
            // All articulation scripts and serialized references are cloned together.
            var copy = UnityEngine.Object.Instantiate(source, parent);
            UnityEngine.Object.DestroyImmediate(old.gameObject);
            copy.name = name; copy.transform.localPosition = position; copy.transform.localRotation = rotation; copy.transform.localScale = scale;
            var controller = copy.GetComponent<ShiploaderRigController>();
            if (controller != null) {
                var so = new SerializedObject(controller);
                so.FindProperty("rootBasePosition").vector3Value = position;
                so.ApplyModifiedPropertiesWithoutUndo(); controller.ApplyPose(new ShiploaderPose(0, 0, 10, 0, 0));
            }
            return copy;
        }

        static JObject FleetSceneConfig(ShiploaderRigController rig, GameObject[] vessels, float min, float max)
        {
            var baseline = JObject.Parse(File.ReadAllText("../Python/jianmo/app/config/default_scene.json"));
            var loader = JObject.Parse(File.ReadAllText("../Python/jianmo/app/config/default_loader.json"));
            var output = new JArray();
            Vector3 origin = rig.transform.position - Vector3.right * rig.Pose.travel;
            foreach (var vessel in vessels) {
                string id = vessel.GetComponentsInChildren<HatchCoverController>(true).First().VesselId;
                var value = (JObject)baseline["vessels"].First(v => (string)v["id"] == id).DeepClone();
                Vector3 p = vessel.transform.position - origin, s = vessel.transform.lossyScale;
                value["position"] = new JArray(p.x, p.z, p.y);
                value["length_m"] = (float)value["length_m"] * s.x;
                value["width_m"] = (float)value["width_m"] * s.z;
                value["height_m"] = (float)value["height_m"] * s.y;
                foreach (JObject hold in value["holds"]) {
                    hold["center_offset_m"] = (float)hold["center_offset_m"] * s.x;
                    hold["length_m"] = (float)hold["length_m"] * s.x;
                    hold["width_m"] = (float)hold["width_m"] * s.z;
                    hold["hatch_plane_offset_z_m"] = (float)hold["hatch_plane_offset_z_m"] * s.y;
                    hold["cover_state"] = "open";
                }
                output.Add(value);
            }
            var geometry = (JObject)loader["geometry"].DeepClone();
            geometry["chute_length_m"] = rig.Config.PresentationChuteLength;
            return new JObject { ["vessels"] = output, ["geometry"] = geometry, ["travel_min"] = min, ["travel_max"] = max };
        }
    }
}
