using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;

namespace ZCJ.Shiploader.Editor
{
    public static class SL15SceneViewClarity
    {
        private const string SnapshotKey = "SL15.SceneViewClarity.Previous";
        [Serializable] private class Previous
        {
            public string pipeline;
            public int msaa, sceneMsaa = 1;
            public Vector3 pivot;
            public Quaternion rotation;
            public float size, near, far;
            public bool orthographic, dynamicClip, grid, fog;
        }

        [MenuItem("Tools/SL15/Improve Scene View Clarity")]
        public static void Apply()
        {
            var view = SceneView.lastActiveSceneView;
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (view == null || pipeline == null) throw new InvalidOperationException("Open the harbor Scene view first.");
            if (string.IsNullOrEmpty(SessionState.GetString(SnapshotKey, "")))
                SessionState.SetString(SnapshotKey, JsonUtility.ToJson(new Previous {
                    pipeline = AssetDatabase.GetAssetPath(pipeline), msaa = pipeline.msaaSampleCount,
                    sceneMsaa = (int)(SceneAA?.GetValue(view) ?? 1),
                    pivot = view.pivot, rotation = view.rotation, size = view.size,
                    orthographic = view.orthographic, near = view.cameraSettings.nearClip,
                    far = view.cameraSettings.farClip, dynamicClip = view.cameraSettings.dynamicClip,
                    grid = view.showGrid, fog = view.sceneViewState.showFog
                }));
            Undo.RecordObject(pipeline, "Improve harbor antialiasing");
            pipeline.msaaSampleCount = 8;
            // Unity 6000.3 keeps a separate Scene-view sample count (observed default: 1).
            // Changing only the URP asset leaves that editor render target un-antialiased.
            SceneAA?.SetValue(view, 8);
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssetIfDirty(pipeline);
            // Preserve the area the user is looking at, but put the navigation pivot on land/water.
            var plane = new Plane(Vector3.up, Vector3.zero);
            Ray ray = view.camera.ViewportPointToRay(new Vector3(.5f, .5f, 0));
            Vector3 focus = plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : new Vector3(130, 0, 0);
            focus.x = Mathf.Clamp(focus.x, -680, 340);
            focus.z = Mathf.Clamp(focus.z, -390, 130);
            focus.y = 8;
            view.cameraSettings.dynamicClip = false;
            view.cameraSettings.nearClip = .3f;
            view.cameraSettings.farClip = 6000;
            view.showGrid = false;
            view.sceneViewState.showFog = false;
            // Low-angle orthographic navigation can put the lower viewport below the sea plane.
            // Return to an upright perspective inspection angle instead of clipping half the view.
            float elevation = Mathf.DeltaAngle(0, view.rotation.eulerAngles.x);
            Quaternion inspection = Quaternion.Euler(Mathf.Clamp(elevation, 30, 75), view.rotation.eulerAngles.y, 0);
            view.LookAt(focus, inspection, Mathf.Clamp(view.size, 65, 260), false, true);
            view.Repaint();
            Debug.Log("Scene clarity: 8x MSAA requested, ground navigation pivot, fixed clipping 0.3–6000, grid/fog hidden. Actual support depends on the rendering target.");
        }

        [MenuItem("Tools/SL15/Restore Previous Scene View Clarity")]
        public static void Restore()
        {
            string json = SessionState.GetString(SnapshotKey, "");
            if (string.IsNullOrEmpty(json)) return;
            var previous = JsonUtility.FromJson<Previous>(json);
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(previous.pipeline);
            if (pipeline != null) { pipeline.msaaSampleCount = previous.msaa; EditorUtility.SetDirty(pipeline); AssetDatabase.SaveAssetIfDirty(pipeline); }
            var view = SceneView.lastActiveSceneView;
            if (view != null) {
                SceneAA?.SetValue(view, Mathf.Max(1, previous.sceneMsaa));
                view.cameraSettings.dynamicClip = previous.dynamicClip;
                view.cameraSettings.nearClip = previous.near;
                view.cameraSettings.farClip = previous.far;
                view.showGrid = previous.grid; view.sceneViewState.showFog = previous.fog;
                view.LookAt(previous.pivot, previous.rotation, previous.size, previous.orthographic, true);
            }
            SessionState.EraseString(SnapshotKey);
        }

        private static PropertyInfo SceneAA => typeof(SceneView).GetProperty("antiAliasing",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    }
}
