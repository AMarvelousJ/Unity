using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering.Universal;

namespace ZCJ.Shiploader.Editor
{
    public static partial class SL15ReferenceModel
    {
        // 130 m spacing also accommodates the enlarged reference vessels.
        const float ModuleSpacing=130f;
        static readonly float[] TripleLanes={0f,-5.4f,5.4f};

        [MenuItem("Tools/SL15/Create Three Complete Berths")]
        public static void CreateThreeCompleteBerths()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode first");
            InitQuayMaterials();
            string original=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            foreach(string path in new[]{SL15SceneBuilder.ScenePath,"Assets/Shiploader/Scenes/SL15_VideoPreview.unity"})
            {
                var scene=EditorSceneManager.OpenScene(path);
                var env=GameObject.Find("PortEnvironment");
                if(PrefabUtility.IsPartOfPrefabInstance(env))PrefabUtility.UnpackPrefabInstance(env,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                var old=GameObject.Find("AdditionalBerthModules");if(old!=null)UnityEngine.Object.DestroyImmediate(old);
                VideoEnvironment(env,2*ModuleSpacing,false);
                BuildSharedQuayConveyors(env.transform);
                var tracks=Owned(env.transform,"ExtendedTravelRails");
                foreach(var t in env.GetComponentsInChildren<Transform>(true))
                    if(t!=tracks && (t.name=="CraneRail"||t.name=="SafetyRail") && t.GetComponent<Renderer>()!=null)t.gameObject.SetActive(false);
                foreach(float z in new[]{-11.5f,11.5f}) {
                    Box(tracks,"RailHead",new Vector3(140,.18f,z),new Vector3(395,.36f,.32f),steel);
                    Box(tracks,"RailGuard",new Vector3(140,.11f,z+Mathf.Sign(z)*.5f),new Vector3(395,.2f,.18f),edge);
                }
                Batch(tracks,"TripleTravelRails");
                var source=GameObject.Find("SL_ShipLoaderRoot");
                var vessels=new[]{GameObject.Find("Vessel-L"),GameObject.Find("Vessel-R")};
                if(source==null||vessels.Any(v=>v==null))throw new InvalidOperationException("Original berth incomplete");
                AlignFeed(source);
                var modules=new GameObject("AdditionalBerthModules").transform;
                for(int i=1;i<3;i++) {
                    var module=Owned(modules,"Berth_0"+(i+1));
                    var loader=CloneDisplay(source,module,"Shiploader_0"+(i+1));
                    OffsetReceivingConveyor(loader,TripleLanes[i],i+1);
                    foreach(var vessel in vessels)CloneDisplay(vessel,module,vessel.name+"_0"+(i+1));
                    module.position=Vector3.right*(ModuleSpacing*i);
                }
                var focus=GameObject.Find("TriplePortCameraFocus");if(focus==null)focus=new GameObject("TriplePortCameraFocus");
                focus.transform.position=new Vector3(130,8,0);
                var camera=Camera.main;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
                var orbit=camera.GetComponent<ShiploaderOrbitCamera>();
                var so=new SerializedObject(orbit);so.FindProperty("target").objectReferenceValue=focus.transform;
                so.FindProperty("distance").floatValue=330;so.FindProperty("yaw").floatValue=-25;so.FindProperty("pitch").floatValue=37;
                so.ApplyModifiedPropertiesWithoutUndo();orbit.SetPreset(ShiploaderCameraPreset.Perspective);
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                Debug.Log("Three complete berths saved: "+path+"; 3 loaders, 6 vessels, 3 independent feed lanes.");
            }
            AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(original);
            var view=SceneView.lastActiveSceneView;
            if(view!=null){view.sceneViewState.showImageEffects=false;view.LookAt(new Vector3(130,8,0),Quaternion.Euler(37,-25,0),230,false,true);}
        }

        static GameObject CloneDisplay(GameObject source,Transform parent,string name)
        {
            var clone=UnityEngine.Object.Instantiate(source,parent);
            clone.name=name;
            // Snapshot geometry only: duplicated vessel IDs must never consume the original backend task.
            foreach(var behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))UnityEngine.Object.DestroyImmediate(behaviour);
            return clone;
        }

        static void OffsetReceivingConveyor(GameObject loader,float lane,int index)
        {
            var feed=loader.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="LandsideConveyor");
            var visual=feed.parent;visual.localPosition+=Vector3.forward*lane;
            var bridge=Owned(visual.parent,"LaneTransfer_0"+index);
            // Short transverse belt bridges the offset receiver to the central loader inlet.
            Vector3 a=new Vector3(-3,13,0),b=new Vector3(-3,13,lane);
            BoxBeam(bridge,"CrossTransferBelt",a,b,2.65f,.1f,black);
            BoxBeam(bridge,"CrossTransferCover",a+Vector3.up*.85f,b+Vector3.up*.85f,2.8f,.12f,blue);
            foreach(float x in new[]{-1.6f,1.6f}) {
                BoxBeam(bridge,"CrossTransferGirder",a+new Vector3(x,-.6f,0),b+new Vector3(x,-.6f,0),.22f,.35f,red);
                BoxBeam(bridge,"CrossTransferSkirt",a+new Vector3(x,.25f,0),b+new Vector3(x,.25f,0),.1f,.7f,blue);
            }
            Batch(bridge,"LaneTransfer_0"+index);
        }
    }
}
