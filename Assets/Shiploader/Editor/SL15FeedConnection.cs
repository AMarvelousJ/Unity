using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ZCJ.Shiploader.Editor
{
    public static partial class SL15ReferenceModel
    {
        static void BuildFeedConnection(Transform feed,float baseHeight,float end)
        {
            var beltMaterial=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Rubber.mat");
            // Coordinates are relative to the horizontal receiving conveyor.
            // Lower belt surface is 5.98 m above the quay; upper belt is baseHeight+1.
            Vector3 high=new Vector3(0,0,end);
            Vector3 low=new Vector3(0,5.98f-(baseHeight+1),end+14);
            Vector3 direction=(low-high).normalized;
            Vector3 normal=new Vector3(0,direction.z,-direction.y);
            foreach(float side in new[]{-1f,1f}) {
                Vector3 offset=Vector3.right*(side*1.6f);
                BoxBeam(feed,"TransferUpperChord",high+offset,low+offset,.23f,.28f,red);
                BoxBeam(feed,"TransferLowerChord",high+offset-normal*.85f,low+offset-normal*.85f,.22f,.24f,edge);
                BoxBeam(feed,"TransferBlueSkirt",high+offset*.83f+normal*.2f,low+offset*.83f+normal*.2f,.09f,.62f,blue);
                for(int i=0;i<9;i++) {
                    Vector3 a=Vector3.Lerp(high,low,i/9f)+offset;
                    Vector3 b=Vector3.Lerp(high,low,(i+1)/9f)+offset;
                    BoxBeam(feed,"TransferTrussDiagonal",a-normal*.85f,b,.1f,.13f,red);
                }
                Vector3 walk=Vector3.right*(side*2.12f)-normal*.55f;
                BoxBeam(feed,"TransferWalkway",high+walk,low+walk,.8f,.12f,deck);
                Rail(feed,high+walk+Vector3.right*side*.4f,low+walk+Vector3.right*side*.4f);
            }
            BoxBeam(feed,"TransferBelt",high,low,2.65f,.1f,beltMaterial);
            BoxBeam(feed,"TransferWeatherCover",high+normal*.8f,low+normal*.8f,2.8f,.12f,blue);
            for(int i=0;i<=18;i++) {
                Vector3 at=Vector3.Lerp(high,low,i/18f);
                Cylinder(feed,"TransferIdler",at-normal*.18f,.23f,2.9f,steel).localRotation=Quaternion.Euler(0,0,90);
                BoxBeam(feed,"TransferRoofRib",at+normal*.89f-Vector3.right*1.43f,at+normal*.89f+Vector3.right*1.43f,.05f,.05f,steel);
            }
            Box(feed,"UpperTransferHood",high+new Vector3(0,.55f,-.15f),new Vector3(3.25f,1.4f,.9f),blue);
            Box(feed,"LowerReceivingHood",low+new Vector3(0,.45f,.15f),new Vector3(3.05f,1.1f,1.4f),blue);
            foreach(float side in new[]{-1f,1f}) {
                Vector3 foot=low+new Vector3(side*3.3f,-5.48f,0);
                BoxBeam(feed,"TransferSupport",foot,low+new Vector3(side*1.65f,-.75f,0),.2f,.24f,steel);
                Box(feed,"TransferSupportFoot",foot,new Vector3(.65f,.14f,.65f),edge);
            }
        }

        [MenuItem("Tools/SL15/Connect Feed Conveyors")]
        public static void ConnectFeedConveyors()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode first");
            InitQuayMaterials();
            string original=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            string prefab=SL15SceneBuilder.PrefabFolder+"/SL15_Shiploader.prefab";
            var contents=PrefabUtility.LoadPrefabContents(prefab);
            try{AlignFeed(contents);PrefabUtility.SaveAsPrefabAsset(contents,prefab);}
            finally{PrefabUtility.UnloadPrefabContents(contents);}
            foreach(string path in new[]{SL15SceneBuilder.ScenePath,"Assets/Shiploader/Scenes/SL15_VideoPreview.unity"}) {
                var scene=EditorSceneManager.OpenScene(path);
                AlignFeed(GameObject.Find("SL_ShipLoaderRoot"));
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(original);
        }
    }
}
