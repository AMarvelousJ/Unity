using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ZCJ.Shiploader.Editor
{
    public static partial class SL15ReferenceModel
    {
        const string FixedTurntableName = "VIS_DualSideTurntable_Fixed";
        const string MovingTurntableName = "VIS_DualSideTurntable_Rotating";

        [MenuItem("Tools/SL15/Add Dual Side Circular Turntables")]
        public static void ApplyDualSideTurntables()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode first");
            InitQuayMaterials();
            string original=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            string backup="Temp/slew-turntable/backup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
            System.IO.Directory.CreateDirectory(backup);
            var current=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(current,backup+"/ActiveScene.unity",true);
            EditorSceneManager.SaveScene(current);
            string prefab=SL15SceneBuilder.PrefabFolder+"/SL15_Shiploader.prefab";
            System.IO.File.Copy(prefab,backup+"/SL15_Shiploader.prefab");
            foreach(string path in new[]{SL15SceneBuilder.ScenePath,"Assets/Shiploader/Scenes/SL15_VideoPreview.unity"})
                System.IO.File.Copy(path,backup+"/"+System.IO.Path.GetFileName(path));
            try
            {
                var contents=PrefabUtility.LoadPrefabContents(prefab);
                try{BuildDualSideTurntable(contents);PrefabUtility.SaveAsPrefabAsset(contents,prefab);}
                finally{PrefabUtility.UnloadPrefabContents(contents);}
                foreach(string path in new[]{SL15SceneBuilder.ScenePath,"Assets/Shiploader/Scenes/SL15_VideoPreview.unity"})
                {
                    var scene=EditorSceneManager.OpenScene(path);
                    BuildDualSideTurntable(GameObject.Find("SL_ShipLoaderRoot"));
                    var modules=GameObject.Find("AdditionalBerthModules");
                    if(modules==null)throw new InvalidOperationException("Missing original berth modules");
                    var loaders=modules.GetComponentsInChildren<Transform>(true)
                        .Where(t=>t.name=="Shiploader_02"||t.name=="Shiploader_03").ToArray();
                    if(loaders.Length!=2)throw new InvalidOperationException("Expected two display loaders on original pier");
                    foreach(var loader in loaders)BuildDualSideTurntable(loader.gameObject);
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                }
                AssetDatabase.SaveAssets();
            }
            finally{EditorSceneManager.OpenScene(original);}
            Debug.Log("Circular turntables saved for all 3 original-pier loaders in both scenes and original loader prefab. Backup: "+backup);
        }

        // The bearing center is the existing slew pivot. No rig dimensions or poses change.
        static void BuildDualSideTurntable(GameObject loader)
        {
            if(loader==null)throw new InvalidOperationException("Missing loader");
            Transform travel=Find(loader,"SL_TravelAssembly");
            Transform upper=Find(loader,"SL_UpperSlewAssembly");
            var fixedRoot=Owned(travel,FixedTurntableName);
            fixedRoot.localPosition=travel.InverseTransformPoint(upper.position);
            var movingRoot=Owned(upper,MovingTurntableName);
            var bearing=Mat("LargeSlewBearingSteel",new Color32(82,91,98,255),.45f,.24f);
            var seal=Mat("LargeSlewBearingSeal",new Color32(29,33,36,255),.05f,.1f);
            var plate=Mat("LargeSlewDeck",new Color32(86,91,91,255),.12f,.22f);
            // Fixed bearing flange on the portal crossheads, then a visible gear/seal band.
            Ring(fixedRoot,"LowerSupportFlange",.06f,9.65f,7.9f,.3f,edge);
            Ring(fixedRoot,"FixedBearingRace",.31f,9.55f,8.15f,.2f,bearing);
            Ring(fixedRoot,"BearingSeparation",.465f,9.6f,8.2f,.09f,seal);
            for(int i=0;i<12;i++)
            {
                float angle=i*Mathf.PI*2/12;
                Vector3 d=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                BoxBeam(fixedRoot,"RadialSupport",d*3.8f-Vector3.up*.12f,d*9.2f-Vector3.up*.12f,.42f,.35f,red);
            }
            for(int i=0;i<128;i++)
            {
                float angle=i*Mathf.PI*2/128;
                Vector3 d=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                var tooth=Box(fixedRoot,"GearTooth",d*9.61f+Vector3.up*.31f,new Vector3(.18f,.18f,.24f),bearing);
                tooth.localRotation=Quaternion.Euler(0,-angle*Mathf.Rad2Deg,0);
                if(i%2==0)Cylinder(fixedRoot,"FlangeBolt",d*9.25f+Vector3.up*.24f,.11f,.14f,bearing);
            }
            // Rotating plate conceals the old rectangular bed beneath it, without editing
            // shared tower meshes or removing their stair, tower and cabin attachments.
            Ring(movingRoot,"RotatingDrum",.72f,9.7f,8.05f,.39f,red);
            Ring(movingRoot,"TopCircularDeck",1.035f,9.85f,1.6f,.24f,plate);
            Ring(movingRoot,"RedDeckRim",1.03f,9.88f,9.64f,.3f,red);
            Ring(movingRoot,"YellowToeBoard",1.23f,9.7f,9.63f,.16f,yellow);
            // Front opening follows the boom, so railings do not run across its passage.
            const int segments=96;
            for(int i=0;i<segments;i++)
            {
                float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;
                float mid=(a+b)*.5f;
                if(Mathf.Sin(mid)>.92f)continue;
                Vector3 d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                Vector3 next=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                foreach(float y in new[]{1.72f,2.25f})
                    BoxBeam(movingRoot,"CircularHandrail",d*9.65f+Vector3.up*y,next*9.65f+Vector3.up*y,.055f,.055f,yellow);
                if(i%3==0)Cylinder(movingRoot,"RailingPost",d*9.65f+Vector3.up*1.7f,.065f,1.15f,yellow);
            }
            for(int i=0;i<48;i++)
            {
                float angle=i*Mathf.PI*2/48;
                Vector3 d=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                Cylinder(movingRoot,"TopDeckBolt",d*9.35f+Vector3.up*1.19f,.1f,.07f,bearing);
            }
            Batch(fixedRoot,"DualSideFixedTurntable");
            Batch(movingRoot,"DualSideRotatingTurntable");
        }
    }
}
