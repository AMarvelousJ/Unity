using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ZCJ.Shiploader.Editor
{
    public static class SL15GlareFix
    {
        [MenuItem("Tools/SL15/Fix Camera Glare Permanently")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("Exit Play mode before changing scene cameras.");
            }

            string originalScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            string[] scenePaths =
            {
                SL15SceneBuilder.ScenePath,
                "Assets/Shiploader/Scenes/SL15_VideoPreview.unity",
            };

            foreach (string scenePath in scenePaths)
            {
                var scene = EditorSceneManager.OpenScene(scenePath);
                Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                if (cameras.Length == 0)
                {
                    throw new InvalidOperationException($"No camera found in {scenePath}.");
                }

                foreach (Camera camera in cameras)
                {
                    UniversalAdditionalCameraData data =
                        camera.GetUniversalAdditionalCameraData();
                    data.renderPostProcessing = false;
                    EditorUtility.SetDirty(data);
                }

                foreach (GameObject temporary in scene.GetRootGameObjects()
                             .Where(root => root.name == "TEMP_BloomIsolation").ToArray())
                {
                    UnityEngine.Object.DestroyImmediate(temporary);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(originalScene))
            {
                EditorSceneManager.OpenScene(originalScene);
            }

            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.sceneViewState.showImageEffects = false;
                SceneView.RepaintAll();
            }

            Debug.Log("SL15 glare fix applied: post-processing disabled on all simulation cameras.");
        }
    }
}
