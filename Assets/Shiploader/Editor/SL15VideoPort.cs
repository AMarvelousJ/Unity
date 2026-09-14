using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ZCJ.Shiploader.Editor
{
    public static partial class SL15ReferenceModel
    {
        [MenuItem("Tools/SL15/Apply Approved Video Port")]
        public static void ApplyVideoPort()
        {
            Apply();
            foreach(string name in new[]{"Vessel-L","Vessel-R"}) {
                string path=SL15SceneBuilder.PrefabFolder+"/"+name+".prefab";
                var vessel=PrefabUtility.LoadPrefabContents(path);
                try { VideoVessel(vessel);PrefabUtility.SaveAsPrefabAsset(vessel,path); }
                finally { PrefabUtility.UnloadPrefabContents(vessel); }
                VideoVessel(GameObject.Find(name));
            }
            var env=GameObject.Find("PortEnvironment");VideoEnvironment(env);
            PrefabUtility.SaveAsPrefabAsset(env,SL15SceneBuilder.PrefabFolder+"/PortEnvironment.prefab");
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(env.scene);EditorSceneManager.SaveScene(env.scene);
            Debug.Log("Approved video port authored: circular chute, rigid nested boom, open holds, side stowed covers and waterfront.");
        }

        static Transform Owned(Transform parent,string name)
        {
            foreach(var old in parent.Cast<Transform>().Where(t=>t.name==name).ToArray()) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var g=new GameObject(name);g.transform.SetParent(parent,false);return g.transform;
        }

        static Mesh PersistMesh(Mesh mesh,string name)
        {
            mesh.name=name;
            string path=Folder+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            UpdateMesh(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);return old;
        }

        static void UpdateMesh(Mesh source,Mesh target)
        {
            target.Clear();target.indexFormat=source.indexFormat;target.name=source.name;
            target.vertices=source.vertices;target.normals=source.normals;target.uv=source.uv;
            target.subMeshCount=source.subMeshCount;
            for(int i=0;i<source.subMeshCount;i++)target.SetTriangles(source.GetTriangles(i),i);
            target.RecalculateBounds();target.UploadMeshData(false);
        }

        static void VideoVessel(GameObject root)
        {
            if(root==null)throw new InvalidOperationException("Missing vessel");
            var hullPaint=Mat("VideoHullGreen",new Color32(22,48, 40,255),.24f,.38f);
            var hullLower=Mat("VideoAntifouling",new Color32(76, 30,27,255),.12f,.25f);
            var hatchPaint=Mat("VideoHatchOxide",new Color32(117, 40, 30,255),.25f,.32f);
            var deckPaint=Mat("VideoDeckGreen",new Color32(40,76, 60,255),.2f,.32f);
            var inside=Mat("VideoHoldInterior",new Color32( 60,48, 40,255),.1f,.25f);
            var hull=root.transform.Find("Hull");
            var source=hull.GetComponent<MeshFilter>().sharedMesh;
            var mesh=UnityEngine.Object.Instantiate(source);
            // Remove the old sealed upper hull face, keeping side and bottom surfaces.
            var vertices=mesh.vertices;
            for(int sub=0;sub<mesh.subMeshCount;sub++) {
                var old=mesh.GetTriangles(sub);var tris=new List<int>();
                for(int i=0;i<old.Length;i+=3) {
                    Vector3 a=vertices[old[i]],b=vertices[old[i+1]],c=vertices[old[i+2]];
                    bool top=a.y>=1.59f&&b.y>=1.59f&&c.y>=1.59f&&Mathf.Max(a.z,Mathf.Max(b.z,c.z))-Mathf.Min(a.z,Mathf.Min(b.z,c.z))>2;
                    if(!top)tris.AddRange(new[]{old[i],old[i+1],old[i+2]});
                }mesh.SetTriangles(tris,sub);
            }
            hull.GetComponent<MeshFilter>().sharedMesh=PersistMesh(mesh,root.name+"_OpenHull");
            hull.GetComponent<MeshRenderer>().sharedMaterials=new[]{hullLower,hullPaint};
            root.transform.Find("MainDeck").GetComponent<MeshRenderer>().enabled=false;
            foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true)) {
                if(r.name.Contains("PanelPlate")||r.name.Contains("PanelRib"))r.sharedMaterial=hatchPaint;
                if(r.name.Contains("Deck")&&!r.name.Contains("Accommodation")&&r.name!="MainDeck"&&r.name!="PoopDeck")r.sharedMaterial=deckPaint;
            }
            Transform detail=Owned(root.transform,"VIS_VideoVessel");
            bool left=root.name=="Vessel-L";float length=left?58:64,width=left?8:8.5f;
            var holds=root.GetComponentsInChildren<HoldCargoVisualController>().OrderBy(c=>c.transform.localPosition.x).ToArray();
            float last=-length*.38f;
            foreach(var cargo in holds) {
                Transform h=cargo.transform;
                float l=h.Find("Coaming_Port").localScale.x,w=Mathf.Abs(h.Find("Coaming_Port").localPosition.z)*2;
                float cx=h.localPosition.x;
                if(cx-l/2>last)Box(detail,"CrossDeck",new Vector3((last+cx-l/2)/2,1.64f,0),new Vector3(cx-l/2-last,.16f,width*.95f),deckPaint);
                last=cx+l/2;
                foreach(float sign in new[]{-1f,1f}) {
                    Box(detail,"SideDeck",new Vector3(cx,1.64f,sign*(w/2+(width-w)/4)),new Vector3(l,.16f,(width-w)/2),deckPaint);
                    Box(detail,"HoldInnerSide",new Vector3(cx,.4f,sign*w/2),new Vector3(l,2.45f,.12f),inside);
                    for(float x=-l/2+.3f;x<l/2;x+=.65f)Box(detail,"HoldFrame",new Vector3(cx+x,.4f,sign*(w/2-.12f)),new Vector3(.085f,2.45f,.13f),edge);
                }
                foreach(float sign in new[]{-1f,1f})Box(detail,"HoldBulkhead",new Vector3(cx+sign*l/2,.4f,0),new Vector3(.14f,2.45f,w),inside);
                Box(detail,"HoldTankTop",new Vector3(cx,-.83f,0),new Vector3(l,.12f,w),inside);
                var coal=h.Find("CoalSurface");coal.GetComponent<MeshFilter>().sharedMesh=PersistMesh(CoalMound(),root.name+"_"+cargo.HoldId+"_CoalMound");
                coal.GetComponent<MeshRenderer>().sharedMaterial=Mat("VideoCoal",new Color32(23,25,26,255),.03f,.16f);
                coal.GetComponent<MeshRenderer>().sharedMaterial.SetTexture("_BaseMap",SurfaceTexture("CoalGrain",true));
                var so=new SerializedObject(cargo);so.FindProperty("coalBottomY").floatValue=-2.45f;so.FindProperty("fullHeight").floatValue=2.6f;so.ApplyModifiedPropertiesWithoutUndo();cargo.SetFillRatio(cargo.FillRatio);
                var controller=h.GetComponentInChildren<HatchCoverController>();bool open=controller.IsOpen;
                foreach(Transform old in controller.transform.Cast<Transform>().ToArray()) {
                    if(old.name.StartsWith("VideoSliding"))UnityEngine.Object.DestroyImmediate(old.gameObject);else old.gameObject.SetActive(false);
                }
                var panels=new Transform[2];var closed=new Vector3[2];var opened=new Vector3[2];var rot=new[]{Vector3.zero,Vector3.zero};
                for(int i=0;i<2;i++) {
                    float sign=i==0?-1:1;var panel=Owned(controller.transform,"VideoSliding"+i);panels[i]=panel;
                    Box(panel,"CoverPlate",Vector3.zero,new Vector3(l*.98f,.15f,w*.49f),hatchPaint);
                    foreach(float z in new[]{-w*.23f,w*.23f})Box(panel,"CoverEdgeBeam",new Vector3(0,-.12f,z),new Vector3(l*.98f,.2f,.08f),edge);
                    for(float x=-l*.46f;x<l*.47f;x+=.65f)Box(panel,"CoverUnderRib",new Vector3(x,-.15f,0),new Vector3(.09f,.2f,w*.46f),edge);
                    Batch(panel,root.name+"_"+cargo.HoldId+"_Cover"+i);
                    closed[i]=new Vector3(0,0,sign*w*.245f);opened[i]=new Vector3(0,.08f,sign*w*.76f);
                }
                controller.Configure(controller.VesselId,controller.HoldId,panels,closed,rot,opened,rot,open);
            }
            if(last<length*.39f)Box(detail,"ForwardDeck",new Vector3((last+length*.39f)/2,1.64f,0),new Vector3(length*.39f-last,.16f,width*.86f),deckPaint);
            Batch(detail,root.name+"_VideoDeck");
            SoftenVessel(root);
        }

        static Mesh CoalMound()
        {
            var v=new List<Vector3>();var tris=new List<int>();int n=24;
            for(int z=0;z<=n;z++)for(int x=0;x<=n;x++) {
                float u=x/(float)n,w=z/(float)n;
                float dome=Mathf.Pow(Mathf.Sin(u*Mathf.PI)*Mathf.Sin(w*Mathf.PI),.65f);
                float noise=Mathf.PerlinNoise(u*24,w*24)*.055f;
                v.Add(new Vector3(u-.5f,-.45f+.9f*dome+noise,w-.5f));
            }
            for(int z=0;z<n;z++)for(int x=0;x<n;x++){int a=z*(n+1)+x;tris.AddRange(new[]{a,a+n+1,a+1,a+1,a+n+1,a+n+2});}
            var m=new Mesh{name="CoalMound"};m.SetVertices(v);m.SetUVs(0,v.ConvertAll(p=>new Vector2(p.x*5,p.z*5)));m.SetTriangles(tris,0);m.RecalculateNormals();m.RecalculateBounds();return m;
        }

        static void VideoEnvironment(GameObject env,float extension=0,bool buildBelt=true,bool legacyShore=true)
        {
            Transform apron=env.transform.Find("ConcreteApron");apron.localPosition=new Vector3(10+extension/2,-.8f,0);apron.localScale=new Vector3(145+extension,1.6f,27);
            foreach(var t in env.GetComponentsInChildren<Transform>())if(t.name=="RailSleeper")t.gameObject.SetActive(false);
            var p=Owned(env.transform,"VIS_VideoPort");
            Material concrete=Mat("VideoConcrete",new Color32(123,129,124,255),.06f,.2f);
            apron.GetComponent<MeshRenderer>().sharedMaterial=concrete;
            foreach(float z in new[]{-13.5f,13.5f}) {
                Box(p,"QuayCap",new Vector3(10+extension/2,-.06f,z),new Vector3(145+extension,.2f,.5f),concrete);
                for(float x=-57;x<80+extension;x+=6) {
                    Box(p,"QuayFender",new Vector3(x,-.45f,z+Mathf.Sign(z)*.25f),new Vector3(1,.95f,.6f),black);
                    Cylinder(p,"QuayBollard",new Vector3(x,.35f,z-Mathf.Sign(z)*.65f),.22f,.55f,steel);
                }
            }
            if(buildBelt)BuildQuayConveyor(p,extension);
            // A quay-side cable reel, with actual annular rim and radial spokes.
            var reel=Owned(p,"CableReel");reel.localPosition=new Vector3(-8,4,5);reel.localRotation=Quaternion.Euler(90,0,0);
            Ring(reel,"ReelRim",0,2,1.9f,.25f,edge);Cylinder(reel,"ReelHub",Vector3.zero,.45f,.7f,steel);
            for(int i=0;i<40;i++){float a=i*Mathf.PI*2/40;BoxBeam(reel,"ReelSpoke",Vector3.zero,new Vector3(Mathf.Cos(a)*1.95f,0,Mathf.Sin(a)*1.95f),.035f,.035f,steel);}
            for(int i=0;i<(legacyShore?14:0);i++) {
                float z=-90+i*14;Box(p,"DistantWarehouse",new Vector3(-111,3.5f,z),new Vector3(22,7,11),i%2==0?white:blue);
                foreach(float sign in new[]{-1f,1f}) {
                    var roof=Box(p,"WarehouseRoof",new Vector3(-111+sign*5.5f,8.7f,z),new Vector3(11.6f,.16f,11.6f),steel);roof.localRotation=Quaternion.Euler(0,0,-sign*16);
                }
                for(int j=0;j<4;j++)Box(p,"WarehouseDoor",new Vector3(-99.94f,2.2f,z-3.8f+j*2.5f),new Vector3(.08f,4.3f,1.8f),deck);
            }
            if(legacyShore)Box(p,"FarShore",new Vector3(-150,-1.3f,0),new Vector3(140,2,600),concrete);
            Box(p,"QuayApproach",new Vector3(-73,-.8f,0),new Vector3(22,1.6f,27),concrete);
            Batch(p,extension>0?"TripleVideoPort":"VideoPort");
            var sea=Owned(env.transform,"VIS_VideoSea");
            var ocean=new Material(Shader.Find("Shiploader/HarborWater")){name="HarborWater"};
            string path=Folder+"/HarborWater.mat";var saved=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(saved==null){AssetDatabase.CreateAsset(ocean,path);saved=ocean;}else{EditorUtility.CopySerialized(ocean,saved);UnityEngine.Object.DestroyImmediate(ocean);}
            var plane=GameObject.CreatePrimitive(PrimitiveType.Plane);plane.name="HarborWater";UnityEngine.Object.DestroyImmediate(plane.GetComponent<Collider>());
            plane.transform.SetParent(sea,false);plane.transform.localPosition=new Vector3(0,-.6f,0);plane.transform.localScale=new Vector3(180,1,180);plane.GetComponent<MeshRenderer>().sharedMaterial=saved;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.48f,.62f,.73f);RenderSettings.ambientEquatorColor=new Color(.39f,.46f,.5f);RenderSettings.ambientGroundColor=new Color(.2f,.22f,.24f);
            RenderSettings.fog=true;RenderSettings.fogColor=new Color(.57f,.7f,.77f);RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.0015f;
            var sun=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l=>l.type==LightType.Directional);
            if(sun!=null){sun.transform.rotation=Quaternion.Euler(38,-40,0);sun.color=new Color(1,.91f,.78f);sun.intensity=1.65f;sun.shadows=LightShadows.Soft;}
        }
    }
}
