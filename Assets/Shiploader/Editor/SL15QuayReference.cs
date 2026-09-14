using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ZCJ.Shiploader.Editor
{
    public static partial class SL15ReferenceModel
    {
        // Image-calibrated proportions. Dimensions are visual estimates, not surveyed dimensions.
        static void BuildQuayConveyor(Transform p, float extension=0, bool includeSupports=true)
        {
            var casing=Mat("QuayConveyorLightBlue",new Color32(85,154,188,255),.08f,.19f);
            var supports=Mat("QuayConveyorSupport",new Color32(86,103,92,255),.18f,.22f);
            const float first=-57.5f,top=6.2f;
            float last=77.5f+extension, length=135+extension, center=10+extension/2;
            foreach(float z in new[]{-1.65f,1.65f}) {
                Box(p,"BlueConveyorSide",new Vector3(center,top-.68f,z),new Vector3(length,1.35f,.1f),casing);
                Box(p,"ConveyorTopLip",new Vector3(center,top+.03f,z),new Vector3(length,.12f,.18f),steel);
                Box(p,"ConveyorBottomChord",new Vector3(center,top-1.45f,z),new Vector3(length,.22f,.2f),supports);
                for(float x=first;x<last;x+=1.5f)Box(p,"BluePanelSeam",new Vector3(x,top-.68f,z+Mathf.Sign(z)*.065f),new Vector3(.035f,1.32f,.035f),blue);
            }
            Box(p,"QuayTransportBelt",new Vector3(center,top-.22f,0),new Vector3(length,.1f,2.65f),black);
            Box(p,"ReturnBelt",new Vector3(center,top-1.2f,0),new Vector3(length,.07f,2.4f),black);
            for(float x=first+.5f;x<last;x+=1.5f) {
                Cylinder(p,"ConveyorIdler",new Vector3(x,top-.4f,0),.24f,2.9f,steel).localRotation=Quaternion.Euler(90,0,0);
                Box(p,"IdlerCrossBearer",new Vector3(x,top-.58f,0),new Vector3(.14f,.14f,3.35f),supports);
            }
            for(float x=first+1;includeSupports&&x<last;x+=4.5f) {
                foreach(float z in new[]{-1.8f,1.8f}) {
                    Box(p,"ConveyorColumn",new Vector3(x,2.43f,z),new Vector3(.19f,4.7f,.2f),supports);
                    Box(p,"ConveyorBasePlate",new Vector3(x,.1f,z),new Vector3(.55f,.12f,.55f),supports);
                    foreach(float dx in new[]{-.19f,.19f})Cylinder(p,"FootBolt",new Vector3(x+dx,.2f,z),.065f,.12f,steel);
                    BoxBeam(p,"ColumnKnee",new Vector3(x,3.5f,z),new Vector3(x+.8f,4.8f,z),.1f,.13f,supports);
                    if(x+4.5f<last)BoxBeam(p,"LongitudinalBracing",new Vector3(x,.6f,z),new Vector3(x+4.5f,4.7f,z),.085f,.1f,supports);
                }
                Box(p,"ConveyorSupportCrosshead",new Vector3(x,4.75f,0),new Vector3(.22f,.25f,3.9f),supports);
            }
            foreach(float z in new[]{-2.2f,2.2f}) {
                Box(p,"MaintenanceWalkway",new Vector3(center,4.85f,z),new Vector3(length,.12f,.8f),deck);
                Rail(p,new Vector3(first,4.95f,z+Mathf.Sign(z)*.38f),new Vector3(last,4.95f,z+Mathf.Sign(z)*.38f));
            }
        }

        static void BuildPortal(Transform p,SL15ModelConfig c)
        {
            float h=c.baseHeight-.75f,g=c.railGauge*.5f,w=c.wheelBase*.5f;
            foreach(float z in new[]{-g,g}) {
                foreach(float x in new[]{-w,w}) {
                    Box(p,"VerticalPortalColumn",new Vector3(x,(h+1.65f)/2,z),new Vector3(1.1f,h-1.65f,1.15f),red);
                    Box(p,"ColumnOuterFlange",new Vector3(x,(h+1.65f)/2,z+Mathf.Sign(z)*.6f),new Vector3(1.25f,h-1.65f,.1f),edge);
                    Box(p,"BogieEqualizer",new Vector3(x,1.1f,z),new Vector3(5.5f,.7f,1.2f),red);
                    Box(p,"PivotSaddle",new Vector3(x,1.6f,z),new Vector3(1.75f,.4f,1.5f),edge);
                    for(int i=0;i<6;i++) {
                        float bx=x-2.15f+i*.86f;
                        Cylinder(p,"RailWheel",new Vector3(bx,.46f,z),.84f,.48f,steel).localRotation=Quaternion.Euler(90,0,0);
                        foreach(float side in new[]{-1f,1f}) {
                            Cylinder(p,"WheelFlange",new Vector3(bx,.46f,z+side*.26f),.91f,.06f,edge).localRotation=Quaternion.Euler(90,0,0);
                            Cylinder(p,"BearingCap",new Vector3(bx,.46f,z+side*.32f),.27f,.12f,red).localRotation=Quaternion.Euler(90,0,0);
                        }
                    }
                    Box(p,"TravelGearbox",new Vector3(x+2.75f,1.15f,z),new Vector3(.7f,.8f,.9f),steel);
                    foreach(float side in new[]{-1f,1f})Box(p,"RailSweeper",new Vector3(x+side*2.95f,.35f,z),new Vector3(.12f,.4f,.5f),edge);
                    BoxBeam(p,"UpperKneeBrace",new Vector3(x,h-3.5f,z),new Vector3(x-Mathf.Sign(x)*3.6f,h-.5f,z),.45f,.5f,red);
                }
                Box(p,"PortalSideHeader",new Vector3(0,h,z),new Vector3(c.wheelBase+1.2f,1.4f,1.1f),red);
                Box(p,"LowerTieBeam",new Vector3(0,2.35f,z),new Vector3(c.wheelBase, .55f,.6f),edge);
                foreach(float sign in new[]{-1f,1f}) {
                    BoxBeam(p,"SideLattice",new Vector3(sign*w,2.65f,z),new Vector3(sign*3.8f,h-.7f,z),.3f,.36f,red);
                    BoxBeam(p,"SideLatticeReturn",new Vector3(sign*w,h-.7f,z),new Vector3(sign*3.8f,2.65f,z),.22f,.26f,edge);
                }
                Box(p,"PortalSideWalkway",new Vector3(0,h+.8f,z),new Vector3(c.wheelBase+2,.14f,1.9f),deck);
                Rail(p,new Vector3(-w-1,h+.9f,z+Mathf.Sign(z)),new Vector3(w+1,h+.9f,z+Mathf.Sign(z)));
            }
            foreach(float x in new[]{-w,w,-6.1f,6.1f})Box(p,"TransversePortalGirder",new Vector3(x,h,0),new Vector3(1.1f,1.5f,c.railGauge+1.1f),red);
            foreach(float z in new[]{-6.7f,6.7f})Box(p,"BearingSupportGirder",new Vector3(0,h,z),new Vector3(c.wheelBase,1.5f,1.1f),red);
            PlatformRing(p,h+.85f,13.5f,15.2f,7.1f,8.2f);
            Stairs(p,new Vector3(-w,1.5f,g-1.3f),new Vector3(-w+8,h+.9f,g-1.3f),.95f);
            Cylinder(p,"SlewRaceBase",new Vector3(0,h+1.1f,0),6.8f,.48f,edge);
            Cylinder(p,"SlewRaceSteel",new Vector3(0,h+1.38f,0),6.2f,.18f,steel);
            Batch(p,"Portal");
        }

        static void InitQuayMaterials()
        {
            red=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/OxideRed.mat");edge=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/RedEdge.mat");
            steel=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Steel.mat");deck=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Walkway.mat");
            yellow=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Handrail.mat");blue=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/ConveyorBlue.mat");
            black=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Rubber.mat");white=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/CabIvory.mat");
        }
        static void ReplacePortal(GameObject root)
        {
            var parents=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Portal_OxideRed").Select(t=>t.parent).Distinct().ToArray();
            foreach(var p in parents)UnityEngine.Object.DestroyImmediate(p.gameObject);
            BuildPortal(Part(Find(root,"SL_TravelAssembly")),root.GetComponent<ShiploaderRigController>().Config);
        }
        [MenuItem("Tools/SL15/Apply Quay Reference Structure")]
        public static void ApplyQuayReference()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit play mode first");
            InitQuayMaterials();
            var temp=GameObject.Find("TEMP_BloomIsolation");if(temp!=null)UnityEngine.Object.DestroyImmediate(temp);
            string original=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            foreach(string name in new[]{"SL15_Shiploader","PortEnvironment"}) {
                string path=SL15SceneBuilder.PrefabFolder+"/"+name+".prefab";
                var root=PrefabUtility.LoadPrefabContents(path);
                try{if(name=="SL15_Shiploader"){ReplacePortal(root);AlignFeed(root);}else VideoEnvironment(root);PrefabUtility.SaveAsPrefabAsset(root,path);}
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            foreach(string path in new[]{SL15SceneBuilder.ScenePath,"Assets/Shiploader/Scenes/SL15_VideoPreview.unity"}) {
                var scene=EditorSceneManager.OpenScene(path);
                var env=GameObject.Find("PortEnvironment");
                if(PrefabUtility.IsPartOfPrefabInstance(env))PrefabUtility.UnpackPrefabInstance(env,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                VideoEnvironment(env);ReplacePortal(GameObject.Find("SL_ShipLoaderRoot"));AlignFeed(GameObject.Find("SL_ShipLoaderRoot"));
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(original);
        }
    }
}
