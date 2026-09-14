using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ZCJ.Shiploader.Editor
{
    public static partial class SL15ReferenceModel
    {
        [MenuItem("Tools/SL15/Create Video Showcase")]
        public static void CreateVideoShowcase()
        {
            // Separate visual calibration scene: preserves the business layout in SL15_Demo.
            const string path="Assets/Shiploader/Scenes/SL15_VideoPreview.unity";
            var current=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(current.path!=path){EditorSceneManager.SaveScene(current);EditorSceneManager.SaveScene(current,path,true);EditorSceneManager.OpenScene(path);}
            var rig=UnityEngine.Object.FindFirstObjectByType<ShiploaderRigController>();
            var previous=rig.Pose;
            rig.ApplyPose(new ShiploaderPose(0,0,3,8,0));
            foreach(var stay in rig.GetComponentsInChildren<ShiploaderVisualStay>())stay.Refresh();
            foreach(string name in new[]{"Vessel-L","Vessel-R"}) {
                var vessel=GameObject.Find(name);vessel.transform.localScale=new Vector3(1.7f,1.35f,2.4f);
                vessel.transform.position=new Vector3(10,1.35f,name=="Vessel-L"?-26f:26f);
                // The bulb sits below the waterline in the reference presentation.
                var bulb=vessel.transform.Find("BulbousBow");if(bulb!=null)bulb.localPosition=new Vector3(bulb.localPosition.x,-2.3f,0);
            }
            var old=GameObject.Find("VIS_BackgroundLoaders");if(old!=null)UnityEngine.Object.DestroyImmediate(old);
            // One working shiploader serves the two berthed vessels.
            rig.ApplyPose(new ShiploaderPose(0,0,3,8,0));
            foreach(var stay in rig.GetComponentsInChildren<ShiploaderVisualStay>())stay.Refresh();
            var qualityObject=GameObject.Find("VideoPreviewQuality");if(qualityObject!=null)UnityEngine.Object.DestroyImmediate(qualityObject);
            string pipelinePath=Folder+"/VideoPreviewPipeline.asset";
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if(pipeline==null){pipeline=UnityEngine.Object.Instantiate((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline);pipeline.name="VideoPreviewPipeline";AssetDatabase.CreateAsset(pipeline,pipelinePath);}
            var pipelineSettings=new SerializedObject(pipeline);
            pipelineSettings.FindProperty("m_RenderScale").floatValue=1;
            pipelineSettings.FindProperty("m_MSAA").intValue=4;
            pipelineSettings.FindProperty("m_MainLightShadowmapResolution").intValue=4096;
            pipelineSettings.FindProperty("m_ShadowDistance").floatValue=240;
            pipelineSettings.FindProperty("m_ShadowCascadeCount").intValue=4;
            pipelineSettings.FindProperty("m_SoftShadowsSupported").boolValue=true;
            pipelineSettings.ApplyModifiedPropertiesWithoutUndo();
            qualityObject=new GameObject("VideoPreviewQuality");qualityObject.SetActive(false);
            qualityObject.AddComponent<ShiploaderPreviewQuality>().previewPipeline=pipeline;qualityObject.SetActive(true);
            if(RenderSettings.skybox!=null) {
                var sky=new Material(RenderSettings.skybox);sky.name="VideoSky";
                if(sky.HasProperty("_SkyTint"))sky.SetColor("_SkyTint",new Color(.42f,.58f,.74f));
                if(sky.HasProperty("_Exposure"))sky.SetFloat("_Exposure",1.1f);
                string skyPath=Folder+"/VideoSky.mat";var asset=AssetDatabase.LoadAssetAtPath<Material>(skyPath);
                if(asset==null){AssetDatabase.CreateAsset(sky,skyPath);asset=sky;}else{EditorUtility.CopySerialized(sky,asset);UnityEngine.Object.DestroyImmediate(sky);}
                RenderSettings.skybox=asset;
            }
            var camera=Camera.main;camera.clearFlags=CameraClearFlags.Skybox;camera.farClipPlane=1800;
            var orbit=camera.GetComponent<ShiploaderOrbitCamera>();var so=new SerializedObject(orbit);
            so.FindProperty("distance").floatValue=113;so.FindProperty("yaw").floatValue=-110;so.FindProperty("pitch").floatValue=22;so.ApplyModifiedPropertiesWithoutUndo();orbit.SetPreset(ShiploaderCameraPreset.Perspective);
            camera.GetUniversalAdditionalCameraData().antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);EditorSceneManager.SaveScene(rig.gameObject.scene);
        }
    }
}
