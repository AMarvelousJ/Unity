using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ZCJ.Shiploader.Editor
{
    public static partial class SL15ReferenceModel
    {
        [MenuItem("Tools/SL15/Fix Glare and Conveyor Alignment")]
        public static void ApplySceneCorrections()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before authoring.");
            red=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/OxideRed.mat");
            edge=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/RedEdge.mat");
            steel=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Steel.mat");
            deck=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Walkway.mat");
            yellow=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Handrail.mat");
            blue=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/ConveyorBlue.mat");
            foreach(string name in new[]{"SL15_Shiploader","Vessel-L","Vessel-R"}) {
                string path=SL15SceneBuilder.PrefabFolder+"/"+name+".prefab";
                var root=PrefabUtility.LoadPrefabContents(path);
                try{if(name=="SL15_Shiploader")AlignFeed(root);else SoftenVessel(root);PrefabUtility.SaveAsPrefabAsset(root,path);}
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            var active=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(active);
            string original=active.path;
            foreach(string path in new[]{SL15SceneBuilder.ScenePath,"Assets/Shiploader/Scenes/SL15_VideoPreview.unity"}) {
                var scene=EditorSceneManager.OpenScene(path);
                var background=GameObject.Find("VIS_BackgroundLoaders");
                if(background!=null)UnityEngine.Object.DestroyImmediate(background);
                AlignFeed(GameObject.Find("SL_ShipLoaderRoot"));
                SoftenVessel(GameObject.Find("Vessel-L"));SoftenVessel(GameObject.Find("Vessel-R"));
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=original)EditorSceneManager.OpenScene(original);
        }

        static void AlignFeed(GameObject root)
        {
            if(root==null)throw new InvalidOperationException("Missing shiploader");
            var parts=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Feed_OxideRed").Select(t=>t.parent).Distinct().ToArray();
            foreach(var part in parts) {
                if(part.name!=VisualRoot)throw new InvalidOperationException("Unexpected feed parent; refusing to replace unrelated geometry");
                UnityEngine.Object.DestroyImmediate(part.gameObject);
            }
            BuildFeed(Part(Find(root,"SL_TravelAssembly")),root.GetComponent<ShiploaderRigController>().Config.baseHeight);
        }

        static void SoftenVessel(GameObject root)
        {
            if(root==null)return;
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true)) {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++) {
                    var source=materials[i];if(source==null)continue;
                    // Glazing keeps modest reflectivity; painted hull/deck/fittings are matte.
                    bool glazing=source.name.Contains("Glaz")||source.name.Contains("Glass");
                    string name=source.name.EndsWith("_MarineMatte")?source.name:source.name+"_MarineMatte";
                    string path=Folder+"/"+name+".mat";
                    var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(material==null){material=new Material(source){name=name};AssetDatabase.CreateAsset(material,path);}
                    material.SetFloat("_Metallic",glazing?.08f:.03f);
                    material.SetFloat("_Smoothness",glazing?.28f:.12f);
                    material.SetFloat("_SpecularHighlights",0);material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                    material.SetFloat("_EnvironmentReflections",0);material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                    EditorUtility.SetDirty(material);materials[i]=material;
                }
                renderer.sharedMaterials=materials;
            }
        }
    }
}
