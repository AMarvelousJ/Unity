using UnityEditor;
using UnityEngine;

namespace ZCJ.Shiploader.Editor
{
    [InitializeOnLoad]
    internal static class SL15AutoBootstrap
    {
        static SL15AutoBootstrap()
        {
            EditorApplication.delayCall += ValidateInitialScene;
        }

        private static void ValidateInitialScene()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            // Never rebuild a committed scene during project import or a script reload.
            // A fresh checkout normally opens SampleScene first, so inspecting the active
            // scene for SL15 components would incorrectly treat a healthy project as
            // incomplete and overwrite the authored 19 MB demo with the base generator.
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SL15SceneBuilder.ScenePath) == null)
            {
                Debug.LogWarning(
                    "SL15_Demo.unity is missing. Automatic rebuilding is disabled to protect " +
                    "authored scene content. Use Tools/SL15/Rebuild Demo Scene explicitly if needed.");
            }
        }
    }
}
