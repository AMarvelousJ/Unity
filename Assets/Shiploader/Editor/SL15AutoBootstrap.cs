using UnityEditor;
using UnityEngine;

namespace ZCJ.Shiploader.Editor
{
    [InitializeOnLoad]
    internal static class SL15AutoBootstrap
    {
        private const string SessionKey = "ZCJ.SL15.AutoBootstrap.VesselGeneralArrangementV3";

        static SL15AutoBootstrap()
        {
            EditorApplication.delayCall += TryBuildInitialScene;
        }

        private static void TryBuildInitialScene()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            SessionState.SetBool(SessionKey, true);
            try
            {
                bool sceneMissing = AssetDatabase.LoadAssetAtPath<SceneAsset>(SL15SceneBuilder.ScenePath) == null;
                bool bridgeMissing = Object.FindFirstObjectByType<ShiploaderRemoteCoordinator>() == null;
                GameObject vesselPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    SL15SceneBuilder.PrefabFolder + "/Vessel-R.prefab");
                bool vesselVisualsMissing = vesselPrefab == null ||
                                            vesselPrefab.transform.Find(
                                                "VesselVisualDetails/VIS_GeneralArrangementV3") == null;
                if (sceneMissing || bridgeMissing || vesselVisualsMissing)
                {
                    SL15SceneBuilder.RebuildDemoScene();
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
