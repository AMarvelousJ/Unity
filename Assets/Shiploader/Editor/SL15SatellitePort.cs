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
        // Relative layout traced from the supplied aerial image. Existing berth remains at Z=0.
        // These are presentation dimensions, not surveyed engineering coordinates.
        [MenuItem("Tools/SL15/Build Aerial Reference Port")]
        public static void BuildAerialReferencePort()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first");
            InitQuayMaterials();
            var original = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            string originalPath = original.path;
            string backup = "Temp/reference-port/backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            System.IO.Directory.CreateDirectory(backup);
            // Preserve both disk scenes plus any unsaved active-scene edits before authoring.
            foreach (string path in new[] { SL15SceneBuilder.ScenePath, "Assets/Shiploader/Scenes/SL15_VideoPreview.unity" })
                System.IO.File.Copy(path, backup + "/" + System.IO.Path.GetFileName(path));
            EditorSceneManager.SaveScene(original, backup + "/ActiveScene.unity", true);
            EditorSceneManager.SaveScene(original);
            try
            {
                foreach (string path in new[] { SL15SceneBuilder.ScenePath, "Assets/Shiploader/Scenes/SL15_VideoPreview.unity" })
                {
                    var scene = EditorSceneManager.OpenScene(path);
                    BuildAerialLayout(GameObject.Find("PortEnvironment").transform);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.OpenScene(originalPath); }
            var view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                view.sceneViewState.showImageEffects = false;
                view.LookAt(new Vector3(-180, 0, -130), Quaternion.Euler(90, 0, 0), 330, true, true);
            }
            Debug.Log("Aerial port saved to both scenes. Original berth retained; 2 display piers; 9 stockyard rows; 4 covered sheds. Backup: " + backup);
        }

        static void BuildAerialLayout(Transform env)
        {
            var formerShore = env.Find("ShoreOperationsDistrict");
            if (formerShore != null) formerShore.gameObject.SetActive(false);
            Transform root = Owned(env, "AerialReferencePort");
            var concrete = Mat("AerialConcrete", new Color32(125,133,132,255), 0, .16f);
            var asphalt = Mat("AerialAsphalt", new Color32(50,58,60,255), 0, .12f);
            var marking = Mat("AerialMarking", new Color32(206,205,184,255), 0, .12f);
            var coal = Mat("AerialCoal", new Color32(26,30,31,255), 0, .08f);
            coal.SetTexture("_BaseMap", SurfaceTexture("CoalGrain", true));
            var roof = Mat("AerialShedRoof", new Color32(154,169,178,255), .1f, .18f);
            var wall = Mat("AerialBuilding", new Color32(177,184,179,255), 0, .15f);
            var pond = Mat("AerialPond", new Color32(30,64,63,255), .05f, .22f);
            var grass = Mat("AerialPlanting", new Color32(66,86,61,255), 0, .05f);
            var ground = Owned(root, "LandRoadsAndSeawall");
            Box(ground, "MainLand", new Vector3(-387,-1.25f,-130), new Vector3(614,2.5f,520), concrete);
            Box(ground, "SouthShoreExtension", new Vector3(-66,-1.25f,-330), new Vector3(28,2.5f,120), concrete);
            foreach (float x in new[] {-677f,-148f,-95f}) AerialRoad(ground, new Vector3(x,.06f,-130), 12, 508, false, asphalt, marking);
            foreach (float z in new[] {118f,-43f,-195f,-245f,-375f}) AerialRoad(ground,new Vector3(-386,.08f,z),590,10,true,asphalt,marking);
            Box(ground,"NorthSeawall",new Vector3(-80,-.45f,-35),new Vector3(1.2f,2.4f,330),steel);
            Box(ground,"SouthSeawall",new Vector3(-52,-.45f,-330),new Vector3(1.2f,2.4f,120),steel);
            BoxBeam(ground,"SeawallTransition",new Vector3(-80,-.45f,-200),new Vector3(-52,-.45f,-270),1.2f,2.4f,steel);
            for (int i=0;i<27;i++)
            {
                float z=115-i*19;
                Cylinder(ground,"WaterfrontLight",new Vector3(-87,7,z),.2f,14,steel);
                Box(ground,"LampHead",new Vector3(-87,14,z),new Vector3(1.7f,.3f,.6f),white);
            }
            Batch(ground,"AerialGround");

            // Image shows long E-W stockpiles, separated into rows by equipment corridors.
            Mesh mound = ShoreCoalMesh();
            for (int row=0;row<9;row++)
            {
                float z=-65-row*34;
                var yard=Owned(root,"StockyardRow_"+(row+1).ToString("00"));
                Box(yard,"StockyardPavement",new Vector3(-410,.08f,z),new Vector3(495,.16f,25),asphalt);
                foreach (float dz in new[]{-12.5f,12.5f}) Box(yard,"StockyardWall",new Vector3(-410,.6f,z+dz),new Vector3(495,1.2f,.45f),concrete);
                for (int segment=0;segment<4;segment++)
                {
                    var heap=new GameObject("CoalHeap_"+segment); heap.transform.SetParent(yard,false);
                    heap.transform.localPosition=new Vector3(-597+segment*123,.17f,z);
                    heap.transform.localScale=new Vector3(22,8+(row+segment)%4,114);
                    heap.transform.localRotation=Quaternion.Euler(0,90,0);
                    heap.AddComponent<MeshFilter>().sharedMesh=mound;
                    heap.AddComponent<MeshRenderer>().sharedMaterial=coal;
                }
                Batch(yard,"AerialYard"+row);
                var route=Owned(root,"YardConveyor_"+row);
                float corridor=z+16;
                ShoreBelt(route,new Vector3(-660,4.8f,corridor),new Vector3(-132,4.8f,corridor));
                // Small mobile stacker on the adjacent rail corridor.
                float sx=-550+(row%3)*140;
                Box(route,"StackerBase",new Vector3(sx,1.4f,corridor),new Vector3(7,1.5f,4),red);
                Box(route,"StackerTower",new Vector3(sx,5.5f,corridor),new Vector3(2.5f,7,2.5f),red);
                ShoreBelt(route,new Vector3(sx,8,corridor),new Vector3(sx+28,12,z),false);
                BoxBeam(route,"StackerStay",new Vector3(sx,13,corridor),new Vector3(sx+28,12,z),.16f,.16f,steel);
                foreach(float dz in new[]{-2.8f,2.8f}) Box(route,"StackerRail",new Vector3(-408,.18f,corridor+dz),new Vector3(490,.22f,.16f),steel);
                Batch(route,"AerialYardRoute"+row);
            }
            var north=Owned(root,"CoveredStorageAndWaterTreatment");
            for(int row=0;row<4;row++)
            {
                float z=12+row*17;
                Box(north,"CoveredShedWall",new Vector3(-323,3.5f,z),new Vector3(260,7,14),wall);
                // Repeated pitched bays reproduce the scalloped roof rhythm visible from above.
                for(int bay=0;bay<13;bay++) foreach(float side in new[]{-1f,1f})
                {
                    var panel=Box(north,"ShedRoofBay",new Vector3(-443+bay*20+side*5,8.2f,z),new Vector3(10.6f,.2f,15),roof);
                    panel.localRotation=Quaternion.Euler(0,0,-side*16);
                }
                ShoreBelt(north,new Vector3(-466,9,z),new Vector3(-170,9,z));
            }
            Box(north,"PondEmbankment",new Vector3(-572,.1f,43),new Vector3(174,.2f,116),grass);
            Box(north,"SettlingPond",new Vector3(-572,.23f,43),new Vector3(160,.08f,100),pond);
            for(int i=0;i<6;i++)
            {
                var island=GameObject.CreatePrimitive(PrimitiveType.Sphere); island.name="PondSedimentIsland";
                UnityEngine.Object.DestroyImmediate(island.GetComponent<Collider>());
                island.transform.SetParent(north,false);island.transform.localPosition=new Vector3(-622+(i%3)*46,.1f,18+(i/3)*43);
                island.transform.localScale=new Vector3(8+i%3*4,.8f,9+i%2*8);island.GetComponent<MeshRenderer>().sharedMaterial=concrete;
            }
            for(int i=0;i<4;i++) ShoreBuilding(north,new Vector3(-150,0,28+i*24),25,17,6,wall,glass??steel);
            Batch(north,"AerialNorthDistrict");

            var feeds=Owned(root,"CoastalTrunkAndTransfers");
            foreach(float x in new[]{-132f,-125f,-118f}) ShoreBelt(feeds,new Vector3(x,8,-365),new Vector3(x,8,110));
            foreach(float z in new[]{0f,-200f,-266f})
            {
                foreach(float dz in TripleLanes) ShoreBelt(feeds,new Vector3(-132,8,z+dz),new Vector3(-57.5f,5.98f,z+dz));
                Box(feeds,"TransferHouse",new Vector3(-125,9,z),new Vector3(22,5,20),blue);
                foreach(float x in new[]{-135f,-115f})foreach(float dz in new[]{-9f,9f}) Box(feeds,"TransferLeg",new Vector3(x,3.5f,z+dz),new Vector3(.4f,7,.4f),steel);
                Box(feeds,"PierApproach",new Vector3(-69,-.8f,z),new Vector3(25,1.6f,27),concrete);
            }
            Batch(feeds,"AerialTrunk");

            BuildAerialDisplayPier(root,env,-200,2);
            BuildAerialDisplayPier(root,env,-266,3);
            BuildAerialServiceBerths(root,concrete);
            env.Find("VIS_VideoSea/HarborWater").localScale=new Vector3(600,1,600);
            RenderSettings.fogDensity=.00023f;
            var focus=GameObject.Find("AerialPortCameraFocus") ?? new GameObject("AerialPortCameraFocus");
            focus.transform.position=new Vector3(-175,6,-130);
            var camera=Camera.main; camera.farClipPlane=3500;
            var orbit=camera.GetComponent<ShiploaderOrbitCamera>();
            var so=new SerializedObject(orbit);
            so.FindProperty("target").objectReferenceValue=focus.transform;
            so.FindProperty("distance").floatValue=900;
            so.FindProperty("yaw").floatValue=-8;
            so.FindProperty("pitch").floatValue=64;
            so.ApplyModifiedPropertiesWithoutUndo(); orbit.SetPreset(ShiploaderCameraPreset.Perspective);
        }

        static void BuildAerialDisplayPier(Transform root,Transform env,float z,int index)
        {
            var pier=Owned(root,"DisplayPier_0"+index);
            foreach(string name in new[]{"ConcreteApron","VIS_VideoPort","ExtendedTravelRails","ThreeLongitudinalConveyors","SharedConveyorFrame","ConveyorTerminalEnclosure"})
            {
                var source=env.Find(name);if(source==null)throw new InvalidOperationException("Missing original pier part: "+name);
                CloneDisplay(source.gameObject,pier,name);
            }
            var loader=GameObject.Find("SL_ShipLoaderRoot");
            for(int i=0;i<2;i++)
            {
                float x=45+i*185;
                var copy=CloneDisplay(loader,pier,"DisplayShiploader_"+i);copy.transform.localPosition=new Vector3(x,0,0);
                foreach(var ring in copy.GetComponentsInChildren<Transform>(true)
                    .Where(t=>t.name==FixedTurntableName||t.name==MovingTurntableName).ToArray())
                    UnityEngine.Object.DestroyImmediate(ring.gameObject);
                BatchAerialDisplay(copy.transform,"AerialLoader"+index+"_"+i);
                foreach(string name in new[]{index == 2 ? "Vessel-R" : "Vessel-L"})
                {
                    var source=GameObject.Find(name);var vessel=CloneDisplay(source,pier,name+"_Display_"+i);
                    vessel.transform.localPosition=source.transform.position+Vector3.right*x;
                    // Preview scene vessels use different display scales. Fit distant replicas
                    // to the berth envelope so the two close southern piers stay navigable.
                    var renderers=vessel.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();
                    var bounds=renderers[0].bounds;
                    foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                    vessel.transform.localScale*=Mathf.Min(1f,16f/bounds.size.z,110f/bounds.size.x);
                    bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                    vessel.transform.localPosition+=Vector3.forward*((name=="Vessel-L"?-24f:24f)-bounds.center.z);
                    BatchAerialDisplay(vessel.transform,"AerialShip"+index+"_"+i+"_"+name);
                }
            }
            pier.localPosition=new Vector3(0,0,z);
        }

        [MenuItem("Tools/SL15/Correct Aerial Berth Sides And Service Piers")]
        public static void CorrectAerialBerths()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode first");
            InitQuayMaterials();
            var original=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            string path=original.path;
            string backup="Temp/aerial-berth-fix/backup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
            System.IO.Directory.CreateDirectory(backup);
            EditorSceneManager.SaveScene(original,backup+"/ActiveScene.unity",true);
            EditorSceneManager.SaveScene(original);
            try {
                foreach(string scenePath in new[]{SL15SceneBuilder.ScenePath,"Assets/Shiploader/Scenes/SL15_VideoPreview.unity"}) {
                    System.IO.File.Copy(scenePath,backup+"/"+System.IO.Path.GetFileName(scenePath));
                    var scene=EditorSceneManager.OpenScene(scenePath);
                    var root=GameObject.Find("PortEnvironment").transform.Find("AerialReferencePort");
                    if(root==null)throw new InvalidOperationException("Missing aerial reference layout");
                    // Left/right are relative to looking offshore (+X): left is +Z.
                    foreach(var t in root.Find("DisplayPier_02").Cast<Transform>().Where(t=>t.name.StartsWith("Vessel-L_Display_")).ToArray())
                        UnityEngine.Object.DestroyImmediate(t.gameObject);
                    foreach(var t in root.Find("DisplayPier_03").Cast<Transform>().Where(t=>t.name.StartsWith("Vessel-R_Display_")).ToArray())
                        UnityEngine.Object.DestroyImmediate(t.gameObject);
                    BuildAerialServiceBerths(root,Mat("AerialConcrete",new Color32(125,133,132,255),0,.16f));
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                }
                AssetDatabase.SaveAssets();
            } finally {EditorSceneManager.OpenScene(path);}
            Debug.Log("Corrected both scenes: middle pier left-side vessels only; lower pier right-side vessels only; two L-shaped service piers with one static shiploader each. Backup: "+backup);
        }

        static void BuildAerialServiceBerths(Transform root,Material concrete)
        {
            var service=Owned(root,"CoastalServiceBerths");
            foreach(bool south in new[]{false,true})
            {
                string id=south?"South":"North";
                var pier=Owned(service,"ServicePier_"+id);
                var decks=Owned(pier,"Decks");
                // Narrow shore access at one end, with an offshore face parallel to the coast.
                // North opens southwards; south opens northwards, as in the aerial reference.
                float shoreX=south?-52f:-80f;
                float faceX=south?-7f:-28f;
                float junctionZ=south?-379f:-57f;
                float centerZ=junctionZ+(south?38f:-38f);
                Box(decks,"ShoreAccess",new Vector3((shoreX+faceX)/2,.05f,junctionZ),new Vector3(faceX-shoreX+5,.8f,5),concrete);
                Box(decks,"OffshoreBerthingFace",new Vector3(faceX,.05f,centerZ),new Vector3(5,.8f,81),concrete);
                // Widen the working deck landward of the berthing face so the loader portal,
                // rails and conveyor have a credible support surface without entering the ship gap.
                Box(decks,"LoaderWorkingDeck",new Vector3(faceX-8.5f,.05f,centerZ),new Vector3(22,.8f,81),concrete);
                Box(decks,"TurningPlatform",new Vector3(faceX,.05f,junctionZ),new Vector3(8,.8f,8),concrete);
                foreach(float dx in new[]{-9.5f,1.5f})
                {
                    Box(decks,"LoaderRail",new Vector3(faceX+dx,.55f,centerZ),new Vector3(.28f,.3f,76),steel);
                    for(float z=centerZ-36;z<=centerZ+36;z+=6)
                        Box(decks,"RailSleeper",new Vector3(faceX+dx,.38f,z),new Vector3(1.1f,.14f,.22f),steel);
                }
                for(float z=centerZ-36;z<=centerZ+36;z+=9)
                {
                    foreach(float dx in new[]{-1.5f,1.5f})Cylinder(decks,"SupportPile",new Vector3(faceX+dx,-1.5f,z),.65f,3,steel);
                    Box(decks,"BerthingFender",new Vector3(faceX+2.85f,-.1f,z),new Vector3(.8f,1.4f,1.6f),black);
                    Cylinder(decks,"MooringBollard",new Vector3(faceX+1.8f,.65f,z),.35f,.45f,steel);
                }
                for(float x=shoreX+4;x<faceX;x+=9)Cylinder(decks,"AccessPile",new Vector3(x,-1.5f,junctionZ),.65f,3,steel);
                Batch(decks,"AerialServicePier"+id);
                BuildStaticServiceShiploader(pier,faceX,centerZ,id);
                var feed=Owned(pier,"ShoreFeed");
                ShoreBelt(feed,new Vector3(shoreX,7.5f,junctionZ),new Vector3(faceX-12f,7.5f,junctionZ));
                ShoreBelt(feed,new Vector3(faceX-12f,7.5f,junctionZ),new Vector3(faceX-12f,7.5f,centerZ));
                Batch(feed,"AerialServiceFeed"+id);
                var vessel=CloneDisplay(GameObject.Find("Vessel-L"),pier,"ServiceVessel_"+id);
                // Size using actual rendered bounds instead of shrinking the model arbitrarily.
                vessel.transform.localRotation=Quaternion.identity;
                var renderers=vessel.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();
                Bounds b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);
                Vector3 scale=vessel.transform.localScale;
                vessel.transform.localScale=new Vector3(scale.x*64f/b.size.x,
                    scale.y*Mathf.Min(64f/b.size.x,10f/b.size.z),scale.z*10f/b.size.z);
                vessel.transform.localRotation=Quaternion.Euler(0,90,0);
                vessel.transform.localPosition=new Vector3(faceX+10,1.6f,centerZ);
                b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);
                vessel.transform.position+=new Vector3(faceX+4.3f-b.min.x,0,centerZ-b.center.z);
                BatchAerialDisplay(vessel.transform,"AerialServiceShip"+id);
            }
        }

        static void BuildStaticServiceShiploader(Transform pier,float faceX,float centerZ,string id)
        {
            var loader=Owned(pier,"ServiceShiploader_"+id);
            float land=faceX-9.5f,sea=faceX+1.5f,z0=centerZ;
            // Four portal legs and two transverse box girders follow the short pier rails.
            foreach(float x in new[]{land,sea})foreach(float z in new[]{z0-4f,z0+4f})
            {
                BoxBeam(loader,"PortalLeg",new Vector3(x,.55f,z),new Vector3(x,10.5f,z),.55f,.55f,red);
                Box(loader,"TravelBogie",new Vector3(x,.8f,z),new Vector3(2.2f,1.1f,1.25f),steel);
                foreach(float dz in new[]{-.42f,.42f})Cylinder(loader,"TravelWheel",new Vector3(x,.42f,z+dz),.7f,.38f,black).localRotation=Quaternion.Euler(90,0,0);
            }
            foreach(float z in new[]{z0-4f,z0+4f})
            {
                BoxBeam(loader,"PortalCrossGirder",new Vector3(land,10.5f,z),new Vector3(sea,10.5f,z),.75f,.8f,red);
                BoxBeam(loader,"PortalDiagonal",new Vector3(land,2f,z),new Vector3(sea,10f,z),.24f,.24f,red);
            }
            Box(loader,"MachineryDeck",new Vector3(faceX-4f,11.2f,z0),new Vector3(14,.9f,10),red);
            // Tower and machinery house create the recognizable loader silhouette.
            foreach(float x in new[]{faceX-7.5f,faceX-1f})foreach(float z in new[]{z0-3f,z0+3f})
                BoxBeam(loader,"TowerLeg",new Vector3(x,11.5f,z),new Vector3(faceX-4.2f,23.5f,z*.0f+z0+(z>z0?1.5f:-1.5f)),.38f,.38f,red);
            Box(loader,"TowerHead",new Vector3(faceX-4.2f,24f,z0),new Vector3(7,2.1f,5.5f),red);
            Box(loader,"MachineryHouse",new Vector3(faceX-6.2f,18f,z0),new Vector3(5.5f,4.2f,6.2f),blue);
            Box(loader,"OperatorCab",new Vector3(faceX-.5f,15.8f,z0-4.2f),new Vector3(3.2f,3,3.2f),white);
            Box(loader,"CabWindow",new Vector3(faceX+1.13f,16.1f,z0-4.2f),new Vector3(.08f,1.6f,2.5f),glass??steel);
            // Enclosed boom and conveyor reach from the tower to the vessel hold.
            Vector3 boomA=new Vector3(faceX-4f,20f,z0),boomB=new Vector3(faceX+9.3f,16.2f,z0);
            BoxBeam(loader,"BoomBelt",boomA,boomB,2.5f,.28f,black);
            foreach(float side in new[]{-1f,1f})
            {
                Vector3 off=Vector3.forward*side*1.65f;
                BoxBeam(loader,"BoomGirder",boomA+off,boomB+off,.35f,.5f,red);
                BoxBeam(loader,"BoomStay",new Vector3(faceX-4.2f,24.8f,z0)+off,boomB+off,.2f,.2f,red);
            }
            Box(loader,"BoomHead",boomB,new Vector3(4.5f,3.2f,5),blue);
            Cylinder(loader,"LoadingChute",new Vector3(faceX+9.3f,9.5f,z0),2.3f,12.5f,blue);
            var chuteGuard=Owned(loader,"ChuteGuard");
            chuteGuard.localPosition=new Vector3(faceX+9.3f,0,z0);
            for(float y=4;y<=15;y+=2.2f)Ring(chuteGuard,"ChuteGuardRing",y,1.75f,1.62f,.13f,yellow);
            foreach(float angle in new[]{0f,90f,180f,270f})
            {
                float a=angle*Mathf.Deg2Rad;
                BoxBeam(loader,"ChuteGuardRail",new Vector3(faceX+9.3f+Mathf.Cos(a)*1.75f,3.7f,z0+Mathf.Sin(a)*1.75f),
                    new Vector3(faceX+9.3f+Mathf.Cos(a)*1.75f,15.2f,z0+Mathf.Sin(a)*1.75f),.13f,.13f,yellow);
            }
            // Landside belt connects the shore access to the loader deck.
            BoxBeam(loader,"LandsideBelt",new Vector3(faceX-12f,7.5f,z0),new Vector3(faceX-5f,13f,z0),2.3f,.25f,black);
            foreach(float side in new[]{-1f,1f})BoxBeam(loader,"LandsideTruss",new Vector3(faceX-12f,7f,z0+side*1.5f),new Vector3(faceX-5f,12.5f,z0+side*1.5f),.25f,.35f,red);
            Batch(loader,"AerialServiceLoader"+id);
        }

        static void AerialRoad(Transform p,Vector3 at,float width,float length,bool alongX,Material surface,Material marking)
        {
            Box(p,"ServiceRoad",at,new Vector3(width,.1f,length),surface);
            float span=alongX?width:length;
            for(float d=-span/2+5;d<span/2;d+=13)
                Box(p,"LaneDash",at+new Vector3(alongX?d:0,.08f,alongX?0:d),alongX?new Vector3(5,.025f,.16f):new Vector3(.16f,.025f,5),marking);
        }

        // Display clones contain no animation. Merge by material, retaining multi-material hulls.
        // Legacy coal meshes can have NaNs at their sin(pi) boundary; repair only copied geometry.
        static void BatchAerialDisplay(Transform root,string prefix)
        {
            prefix=root.gameObject.scene.name+"_"+prefix;
            var groups=new Dictionary<Material,List<CombineInstance>>();
            var repairs=new Dictionary<Mesh,Mesh>();
            var sources=root.GetComponentsInChildren<MeshFilter>(true);
            foreach(var filter in sources)
            {
                var renderer=filter.GetComponent<MeshRenderer>();
                if(renderer==null || !renderer.enabled || !filter.gameObject.activeInHierarchy)continue;
                var mesh=filter.sharedMesh;if(mesh==null)continue;
                if(!repairs.TryGetValue(mesh,out var valid))
                {
                    valid=mesh;var vertices=mesh.vertices;bool changed=false;
                    for(int i=0;i<vertices.Length;i++)
                    {
                        if(float.IsNaN(vertices[i].y) || float.IsInfinity(vertices[i].y))
                        {
                            if(!mesh.name.Contains("Coal"))throw new InvalidOperationException("Invalid source geometry: "+mesh.name);
                            vertices[i].y=-.45f;changed=true;
                        }
                    }
                    if(changed){valid=UnityEngine.Object.Instantiate(mesh);valid.vertices=vertices;valid.RecalculateNormals();valid.RecalculateBounds();}
                    repairs[mesh]=valid;
                }
                for(int sub=0;sub<valid.subMeshCount;sub++)
                {
                    var material=renderer.sharedMaterials[Mathf.Min(sub,renderer.sharedMaterials.Length-1)];
                    if(!groups.TryGetValue(material,out var list)){list=new List<CombineInstance>();groups[material]=list;}
                    list.Add(new CombineInstance{mesh=valid,subMeshIndex=sub,transform=root.worldToLocalMatrix*filter.transform.localToWorldMatrix});
                }
            }
            foreach(var pair in groups)
            {
                var mesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
                mesh.CombineMeshes(pair.Value.ToArray(),true,true);mesh.RecalculateBounds();
                var node=new GameObject(prefix+"_"+pair.Key.name);node.transform.SetParent(root,false);
                node.AddComponent<MeshFilter>().sharedMesh=PersistMesh(mesh,prefix+"_"+pair.Key.name);
                node.AddComponent<MeshRenderer>().sharedMaterial=pair.Key;
            }
            foreach(var source in sources)if(source!=null)UnityEngine.Object.DestroyImmediate(source.gameObject);
            foreach(var pair in repairs)if(pair.Key!=pair.Value)UnityEngine.Object.DestroyImmediate(pair.Value);
        }
    }
}


