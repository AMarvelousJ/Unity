using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZCJ.Shiploader.Editor
{
    // Reference-led replacement of the visible loader. The original skeleton and effects contracts survive.
    public static partial class SL15ReferenceModel
    {
        const string Folder = "Assets/Shiploader/ReferenceModel";
        const string VisualRoot = "VIS_ReferenceModel_R2";
        static Material red, edge, steel, deck, yellow, white, glass, blue, black;
        static readonly List<Renderer> belts = new();

        [MenuItem("Tools/SL15/Rebuild Reference Shiploader")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Shiploader", "ReferenceModel");
            red = Mat("OxideRed", new Color32(193, 78,  43, 255), .28f, .35f);
            edge = Mat("RedEdge", new Color32(126, 48, 30, 255), .36f, .34f);
            steel = Mat("Steel", new Color32(97, 110, 120, 255), .65f, .35f);
            deck = Mat("Walkway", new Color32(66, 76, 82, 255), .45f, .3f);
            yellow = Mat("Handrail", new Color32(207, 175, 85, 255), .2f, .34f);
            white = Mat("CabIvory", new Color32(227, 228, 218, 255), .12f, .4f);
            glass = Mat("CabGlazing", new Color32(23, 52, 65, 255), .48f, .7f);
            blue = Mat("ConveyorBlue", new Color32(32, 87, 127, 255), .2f, .3f);
            black = Mat("Rubber", new Color32(25, 30, 33, 255), .1f, .28f);
            ApplySurfaceFinish();
            string prefab = SL15SceneBuilder.PrefabFolder + "/SL15_Shiploader.prefab";
            GameObject contents = PrefabUtility.LoadPrefabContents(prefab);
            try { Build(contents); PrefabUtility.SaveAsPrefabAsset(contents, prefab); }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            GameObject root = GameObject.Find("SL_ShipLoaderRoot");
            if (root == null) throw new InvalidOperationException("Open SL15_Demo before applying.");
            Build(root);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
            Debug.Log("Reference shiploader rebuilt: box girder portal, main tower, A-frame, twin stays, enclosed conveyors, service stairs and discharge cage.");
        }

        static void Build(GameObject root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == VisualRoot).ToArray())
                UnityEngine.Object.DestroyImmediate(t.gameObject);
            // Disable legacy geometry rather than deleting transforms used by rig/effects/remote clients.
            foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
            belts.Clear();
            var rig = root.GetComponent<ShiploaderRigController>();
            var c = rig.Config;
            Transform travel = Find(root, "SL_TravelAssembly"), upper = Find(root, "SL_UpperSlewAssembly");
            Transform fixedBoom = Find(root, "SL_FixedBoomAssembly"), telescope = Find(root, "SL_TelescopicBoomAssembly");
            Transform head = Find(root, "SL_BoomHeadAttachment"), chute = Find(root, "SL_ChuteAssembly");
            BuildPortal(Part(travel), c);
            BuildTower(Part(upper));
            BuildBoom(Part(fixedBoom), c.fixedBoomLength, 3.7f, 2.7f, 9, "MainBoom");
            BuildNestedTelescope(Part(Find(root, "SL_BoomLuffPivot")), rig);
            BuildHead(Part(head));
            BuildChute(Part(chute), c.PresentationChuteLength);
            BuildFeed(Part(travel), c.baseHeight);
            Transform stays = Part(upper);
            for (int side = -1; side <= 1; side += 2)
            {
                Transform rod = Cylinder(stays, "TwinLuffStay", Vector3.zero, .2f, 1, red);
                var follow = rod.gameObject.AddComponent<ShiploaderVisualStay>();
                follow.tower = upper; follow.boom = fixedBoom;
                follow.towerOffset = new Vector3(side * 2.45f, 16, -2.7f);
                follow.boomOffset = new Vector3(side * 1.92f, 1.4f, c.fixedBoomLength * .88f);
                follow.diameter = .22f; follow.Refresh();
            }
            // Keep the original rotating outlet geometry/coal emitter; its yaw remains backend-controlled.
            Transform yaw = Find(root, "SL_ChuteYawAssembly");
            // Align the preserved rotary discharge geometry and particle source with the retracted visual.
            Transform outlet = yaw.GetComponentsInChildren<Transform>(true).First(t => t.name == "DischargeOutlet");
            Vector3 outletInYaw = yaw.InverseTransformPoint(outlet.position);
            yaw.localPosition = new Vector3(0, -c.PresentationChuteLength - outletInYaw.y, 0);
            outlet.localPosition = new Vector3(0,outletInYaw.y,0);
            var coalFlow = yaw.GetComponentInChildren<ParticleSystem>(true);
            if(coalFlow != null) coalFlow.transform.localPosition = new Vector3(0,outletInYaw.y-.2f,0);
            Find(root, "MK_CHUTE_BOTTOM").localPosition = new Vector3(0, -c.PresentationChuteLength, 0);
            Find(root, "MK_DISCHARGE").localPosition = new Vector3(0, -c.PresentationChuteLength - c.dischargeDrop, 0);
            BuildRotaryOutlet(Part(yaw), outletInYaw.y);
            BuildCoalStream(Part(yaw),root.GetComponent<ShiploaderEffectsController>(),outlet);
            BuildDualSideTurntable(root);
            rig.ApplyPose(rig.Pose);
            var effects = root.GetComponent<ShiploaderEffectsController>();
            var serialized = new SerializedObject(effects);
            var references = serialized.FindProperty("beltRenderers"); references.arraySize = belts.Count;
            for (int i = 0; i < belts.Count; i++) references.GetArrayElementAtIndex(i).objectReferenceValue = belts[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(effects);
        }

        static void BuildTower(Transform p)
        {
            Platform(p,new Vector3(0,.65f,-.5f),11,10);
            Box(p,"SlewingBoxBed",new Vector3(0,.22f,-.5f),new Vector3(9.8f,.8f,8.5f),red);
            // Tall twin box tower with a crosshead, open central throat and top A-frame.
            foreach(float x in new[]{-2.65f,2.65f}) {
                BoxBeam(p,"MainTowerColumn",new Vector3(x,1,-2.5f),new Vector3(x,11.8f,-2.5f),1.05f,1.5f,red);
                Box(p,"TowerFlange",new Vector3(x,6.1f,-1.71f),new Vector3(1.2f,10.5f,.13f),edge);
                BoxBeam(p,"TowerRearKnee",new Vector3(x,1.1f,-7.7f),new Vector3(x,8.4f,-3),.7f,.7f,red);
                BoxBeam(p,"AFrameForeLeg",new Vector3(x,11.6f,-2.5f),new Vector3(x*.92f,16,-2.7f),.5f,.55f,red);
                BoxBeam(p,"AFrameRearLeg",new Vector3(x,7.3f,-8.3f),new Vector3(x*.92f,16,-2.7f),.45f,.5f,red);
                BoxBeam(p,"RearTowerPost",new Vector3(x,1.1f,-8.3f),new Vector3(x,7.3f,-8.3f),.48f,.55f,red);
                BoxBeam(p,"RearTowerTie",new Vector3(x,7.3f,-8.3f),new Vector3(x,7.3f,-2.5f),.3f,.35f,red);
                BoxBeam(p,"RearTowerDiagonal",new Vector3(x,1.1f,-8.3f),new Vector3(x,7.3f,-2.5f),.2f,.25f,red);
                BoxBeam(p,"AFrameCrossBrace",new Vector3(x,11.7f,-2.6f),new Vector3(x,11.2f,-6.3f),.24f,.3f,edge);
                BoxBeam(p,"AFrameDiagonal",new Vector3(x,13.8f,-2.65f),new Vector3(x,11.2f,-6.3f),.18f,.22f,red);
                for(int level=0;level<4;level++) {
                    Box(p,"TowerJointPlate",new Vector3(x,2.2f+level*2.6f,-1.57f),new Vector3(1.24f,.36f,.07f),edge);
                    foreach(float dx in new[]{-.4f,.4f}) Cylinder(p,"TowerBolt",new Vector3(x+dx,2.2f+level*2.6f,-1.49f),.095f,.08f,steel).localRotation=Quaternion.Euler(90,0,0);
                }
            }
            Box(p,"TowerCrosshead",new Vector3(0,11.9f,-2.5f),new Vector3(6.8f,1.2f,1.85f),red);
            Box(p,"AFrameCrown",new Vector3(0,16,-2.7f),new Vector3(6,.45f,.65f),red);
            Platform(p,new Vector3(0,12.65f,-2.6f),7,3.5f);
            // High rear winch drum: a distinctive silhouette visible in the references.
            Cylinder(p,"LuffWinchDrum",new Vector3(0,11.6f,-7.1f),2.25f,6.7f,red).localRotation=Quaternion.Euler(0,0,90);
            foreach(float x in new[]{-3.4f,3.4f}) Cylinder(p,"WinchEndShield",new Vector3(x,11.6f,-7.1f),2.5f,.16f,edge).localRotation=Quaternion.Euler(0,0,90);
            Platform(p,new Vector3(0,10.1f,-7),8.5f,3.7f);
            // External switchback access stair with intermediate decks.
            for(int level=0;level<5;level++) {
                float y=.85f+level*2.3f;
                Platform(p,new Vector3(4.05f,y,-2.5f),1.4f,4.5f);
                Vector3 a=new Vector3(4.05f,y,-4.2f), b=new Vector3(4.05f,y+2.3f,-.8f);
                if(level%2==1) {a.z=-.8f;b.z=-4.2f;}
                Stairs(p,a,b,.95f);
            }
            // Machinery room is a subordinate volume behind the tower, not the central silhouette.
            Box(p,"WinchMachineryRoom",new Vector3(-.5f,2.05f,-6.7f),new Vector3(5.8f,2.45f,3),red);
            Box(p,"MachineryRoof",new Vector3(-.5f,3.34f,-6.7f),new Vector3(6.1f,.17f,3.3f),edge);
            for(int i=0;i<15;i++) Box(p,"MachineLouvre",new Vector3(-2.8f+i*.28f,2.1f,-8.23f),new Vector3(.15f,1.35f,.06f),deck);
            // White projecting operator cab with dark wrap-around glazing.
            BoxBeam(p,"CabCantilever",new Vector3(-2.7f,6,-2.5f),new Vector3(-5.25f,6,-.4f),.35f,.4f,red);
            Platform(p,new Vector3(-4.7f,6.15f,-.1f),3.4f,3.7f);
            Box(p,"OperatorCab",new Vector3(-4.7f,7.25f,.1f),new Vector3(2.7f,2.05f,2.7f),white);
            Box(p,"CabFrontGlass",new Vector3(-4.7f,7.58f,1.47f),new Vector3(2.38f,1.15f,.06f),glass);
            foreach(float x in new[]{-6.07f,-3.33f}) Box(p,"CabSideGlass",new Vector3(x,7.58f,.1f),new Vector3(.05f,1.15f,2.35f),glass);
            for(int i=-1;i<=1;i++) Box(p,"CabFrontMullion",new Vector3(-4.7f+i*.8f,7.58f,1.52f),new Vector3(.055f,1.2f,.05f),white);
            Box(p,"CabRoof",new Vector3(-4.7f,8.34f,.2f),new Vector3(3.05f,.17f,3.1f),white);
            Cylinder(p,"Beacon",new Vector3(-4.7f,8.63f,.2f),.21f,.4f,yellow);
            Batch(p,"Tower");
        }

        static void BuildBoom(Transform p,float length,float width,float height,int bays,string prefix,bool normalized=false)
        {
            float half=width*.5f, h=height*.5f;
            foreach(float x in new[]{-half,half}) {
                Box(p,"UpperBoxChord",new Vector3(x,h,length*.5f),new Vector3(.36f,.48f,length),red);
                Box(p,"LowerBoxChord",new Vector3(x,-h,length*.5f),new Vector3(.26f,.32f,length),edge);
                for(int i=0;i<=bays;i++) {
                    float z=length*i/bays;
                    Box(p,"VerticalWeb",new Vector3(x,0,z),new Vector3(.25f,height,.1f),red);
                    if(i<bays) BoxBeam(p,"WebDiagonal",new Vector3(x,-h,z),new Vector3(x,h,z+length/bays),.16f,.18f,red);
                }
                Box(p,"ConveyorSkirt",new Vector3(x*.84f,-.28f,length*.5f),new Vector3(.075f,.54f,length),blue);
            }
            for(int i=0;i<=bays;i++) Box(p,"FloorCrossBearer",new Vector3(0,-h,length*i/bays),new Vector3(width,.23f,.14f),steel);
            // Large solid girder panels give the reference its box-girder profile.
            if(!normalized) foreach(float x in new[]{-half-.04f,half+.04f}) {
                Box(p,"WeldedGirderPanel",new Vector3(x,.49f,length*.5f),new Vector3(.12f,1.6f,length*.96f),red);
                for(int i=0;i<9;i++) Box(p,"PanelStiffener",new Vector3(x*1.018f,.49f,length*(.05f+i*.11f)),new Vector3(.13f,1.6f,.07f),edge);
                Box(p,"MachineIdentificationPanel",new Vector3(x*1.05f,.57f,length*.49f),new Vector3(.04f,.92f,length*.42f),white);
            }
            // Keep the animated belt separate from static combined geometry.
            Transform belt=Box(p,"ConveyorBelt",new Vector3(0,-.02f,length*.5f),new Vector3(width*.73f,.12f,length),AssetDatabase.LoadAssetAtPath<Material>(SL15SceneBuilder.MaterialFolder+"/Conveyor_Belt.mat"));
            belts.Add(belt.GetComponent<Renderer>());
            for(int i=0;i<=bays*2;i++) Cylinder(p,"TroughIdler",new Vector3(0,-.18f,length*i/(bays*2)),.22f,width*.77f,steel).localRotation=Quaternion.Euler(0,0,90);
            if(!normalized) {
                foreach(float x in new[]{-half-.64f,half+.64f}) {
                    Box(p,"InspectionWalkway",new Vector3(x,-.62f,length*.5f),new Vector3(.88f,.1f,length),deck);
                    Rail(p,new Vector3(x+Mathf.Sign(x)*.42f,-.55f,0),new Vector3(x+Mathf.Sign(x)*.42f,-.55f,length));
                }
            }
            Batch(p,prefix);
        }

        static void BuildHead(Transform p)
        {
            // The reference has a low tapered drum enclosure, not a standing platform on the boom tip.
            Box(p,"HeadTopCap",new Vector3(0,.83f,.15f),new Vector3(4.2f,.14f,2.8f),red);
            Cylinder(p,"HeadDrum",new Vector3(0,.18f,.28f),.94f,3.5f,steel).localRotation=Quaternion.Euler(0,0,90);
            foreach(float x in new[]{-1.94f,1.94f}) {
                TaperedCheek(p,new Vector3(x,0,0));
                BoxBeam(p,"HeadLowerRim",new Vector3(x,-.55f,-1.1f),new Vector3(x,-.12f,1.55f),.15f,.14f,edge);
                BoxBeam(p,"DischargeHanger",new Vector3(x,-.4f,-.8f),new Vector3(x*.63f,-1.7f,0),.2f,.22f,red);
            }
            Batch(p,"Head");
        }

        static void TaperedCheek(Transform parent,Vector3 offset)
        {
            Vector3[] profile={new Vector3(0,-.6f,-1.2f),new Vector3(0,.8f,-1.2f),new Vector3(0,.8f,1.6f),new Vector3(0,-.15f,1.6f)};
            var vertices=new Vector3[8];
            for(int i=0;i<4;i++){vertices[i]=profile[i]+Vector3.left*.09f;vertices[i+4]=profile[i]+Vector3.right*.09f;}
            var mesh=new Mesh{name="TaperedHeadCheek"};mesh.vertices=vertices;
            mesh.triangles=new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,1,2,6,1,6,5,2,3,7,2,7,6,3,0,4,3,4,7};
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=new GameObject("TaperedHeadCheek");go.transform.SetParent(parent,false);go.transform.localPosition=offset;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=red;
        }

        static void BuildChute(Transform p,float length)
        {
            float top=-2.25f, bottom=-length+1.4f;
            Frustum(p,"TransferHopper",-.4f,-1.85f,1.6f,.78f,red);
            Cylinder(p,"HopperNeck",new Vector3(0,-2.03f,0),1.5f,.5f,red);
            PlatformRing(p,-2.18f,4.4f,3.7f,1.65f,1.65f);
            foreach(float x in new[]{-1.85f,1.85f})
                BoxBeam(p,"PlatformBracket",new Vector3(x,-.45f,-.5f),new Vector3(x,-2.18f,-1.5f),.16f,.22f,edge);
            Cylinder(p,"InnerTelescopicTube",new Vector3(0,(top+bottom)/2,0),1.36f,top-bottom,red);
            int levels=4;
            for(int j=0;j<=levels;j++) {
                float y=Mathf.Lerp(top,bottom,j/(float)levels);
                Ring(p,"ChuteAnnularCollar",y,1.05f,.93f,.13f,steel);
                Ring(p,"CollarLip",y+.09f,1.08f,.99f,.04f,edge);
            }
            for(int i=0;i<28;i++) {
                float a=i*Mathf.PI*2/28;
                Box(p,"ChuteVerticalGuide",new Vector3(Mathf.Cos(a)*.98f,(top+bottom)/2,Mathf.Sin(a)*.98f),new Vector3(.045f,top-bottom,.045f),steel);
            }
            Cylinder(p,"LowerSwivelNeck",new Vector3(0,bottom-.3f,0),1.25f,.6f,steel);
            Ring(p,"LowerFlange",bottom-.59f,.8f,.57f,.1f,red);
            Batch(p,"Chute");
        }

        static void BuildFeed(Transform p,float baseHeight)
        {
            // A covered landside belt on an independent trussed trestle.
            Transform feed=new GameObject("LandsideConveyor").transform; feed.SetParent(p,false);
            feed.localPosition=new Vector3(-3,baseHeight+1,0);
            feed.localRotation=Quaternion.LookRotation(Vector3.left,Vector3.up);
            float length=29;
            foreach(float x in new[]{-1.6f,1.6f}) {
                Box(feed,"FeedTopChord",new Vector3(x,1,length*.5f),new Vector3(.27f,.33f,length),red);
                Box(feed,"FeedBottomChord",new Vector3(x,-1,length*.5f),new Vector3(.25f,.3f,length),edge);
                for(int i=0;i<10;i++) BoxBeam(feed,"FeedWeb",new Vector3(x,-1,i*length/10),new Vector3(x,1,(i+1)*length/10),.16f,.18f,red);
                Box(feed,"EnclosedBlueSide",new Vector3(x*.83f,.1f,length*.5f),new Vector3(.1f,1.25f,length),blue);
            }
            Box(feed,"ConveyorWeatherRoof",new Vector3(0,.85f,length*.5f),new Vector3(2.8f,.12f,length),blue);
            for(int i=0;i<30;i++) Box(feed,"RoofRib",new Vector3(0,.94f,i*length/30),new Vector3(2.85f,.06f,.04f),steel);
            foreach(float x in new[]{-2.15f,2.15f}) {Box(feed,"FeedWalkway",new Vector3(x,-.6f,length*.5f),new Vector3(.9f,.12f,length),deck);Rail(feed,new Vector3(x+Mathf.Sign(x)*.45f,-.52f,0),new Vector3(x+Mathf.Sign(x)*.45f,-.52f,length));}
            foreach(float x in new[]{-5f,5f}) BoxBeam(feed,"FeedTrestle",new Vector3(x,-baseHeight-.3f,17),new Vector3(Mathf.Sign(x)*1.55f,-1,17),.4f,.45f,red);
            BuildFeedConnection(feed,baseHeight,length);
            Batch(p,"Feed");
        }

        static void Platform(Transform p,Vector3 c,float w,float d)
        {
            Box(p,"PlatformFloor",c,new Vector3(w,.14f,d),deck);
            foreach(float x in new[]{-w*.5f,w*.5f}) Rail(p,c+new Vector3(x,.08f,-d*.5f),c+new Vector3(x,.08f,d*.5f));
            foreach(float z in new[]{-d*.5f,d*.5f}) Rail(p,c+new Vector3(-w*.5f,.08f,z),c+new Vector3(w*.5f,.08f,z));
        }
        static void PlatformRing(Transform p,float y,float w,float d,float holeW,float holeD)
        {
            foreach(float x in new[]{-1f,1f}) {Box(p,"RingSideDeck",new Vector3(x*(w+holeW)*.25f,y,0),new Vector3((w-holeW)*.5f,.14f,d),deck);Rail(p,new Vector3(x*w*.5f,y+.08f,-d*.5f),new Vector3(x*w*.5f,y+.08f,d*.5f));}
            foreach(float z in new[]{-1f,1f}) {Box(p,"RingEndDeck",new Vector3(0,y,z*(d+holeD)*.25f),new Vector3(holeW,.14f,(d-holeD)*.5f),deck);Rail(p,new Vector3(-w*.5f,y+.08f,z*d*.5f),new Vector3(w*.5f,y+.08f,z*d*.5f));}
        }
        static void Rail(Transform p,Vector3 a,Vector3 b)
        {
            BoxBeam(p,"Handrail",a+Vector3.up*.95f,b+Vector3.up*.95f,.045f,.045f,yellow);
            BoxBeam(p,"Midrail",a+Vector3.up*.5f,b+Vector3.up*.5f,.032f,.032f,yellow);
            BoxBeam(p,"ToeBoard",a+Vector3.up*.09f,b+Vector3.up*.09f,.04f,.16f,yellow);
            int n=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/1.5f));
            for(int i=0;i<=n;i++) Box(p,"Stanchion",Vector3.Lerp(a,b,i/(float)n)+Vector3.up*.5f,new Vector3(.045f,1,.045f),yellow);
        }
        static void Stairs(Transform p,Vector3 a,Vector3 b,float width)
        {
            Vector3 across=Vector3.Cross((b-a).normalized,Vector3.up).normalized;
            int n=Mathf.Max(2,Mathf.CeilToInt(Mathf.Abs(b.y-a.y)/.2f));
            Vector3 flat=b-a;flat.y=0;
            for(int i=0;i<=n;i++) {var t=Box(p,"StairTread",Vector3.Lerp(a,b,i/(float)n),new Vector3(width,.065f,flat.magnitude/n+.06f),steel);t.localRotation=Quaternion.LookRotation(flat);}
            foreach(float side in new[]{-1f,1f}) {
                Vector3 off=across*width*.5f*side;
                BoxBeam(p,"StairStringer",a+off-Vector3.up*.12f,b+off-Vector3.up*.12f,.12f,.22f,red);
                Rail(p,a+off,b+off);
            }
        }
        static Transform Find(GameObject root,string name)=>root.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
        static Transform Part(Transform parent){var g=new GameObject(VisualRoot);g.transform.SetParent(parent,false);return g.transform;}
        static Transform Box(Transform p,string n,Vector3 at,Vector3 size,Material m)
        {
            if(n=="ConveyorBelt") {
                var belt=GameObject.CreatePrimitive(PrimitiveType.Cube);belt.name=n;
                UnityEngine.Object.DestroyImmediate(belt.GetComponent<Collider>());
                belt.transform.SetParent(p,false);belt.transform.localPosition=at;belt.transform.localScale=size;
                belt.GetComponent<MeshRenderer>().sharedMaterial=m;return belt.transform;
            }
            var g=new GameObject(n);g.transform.SetParent(p,false);g.transform.localPosition=at;
            g.AddComponent<MeshFilter>().sharedMesh=BevelBox(size);
            g.AddComponent<MeshRenderer>().sharedMaterial=m;return g.transform;
        }
        static Transform Cylinder(Transform p,string n,Vector3 at,float diameter,float length,Material m)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=n;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(p,false);g.transform.localPosition=at;g.transform.localScale=new Vector3(diameter,length*.5f,diameter);g.GetComponent<MeshRenderer>().sharedMaterial=m;return g.transform;
        }
        static void BoxBeam(Transform p,string n,Vector3 a,Vector3 b,float w,float depth,Material m)
        {
            Transform t=Box(p,n,(a+b)*.5f,new Vector3(w,(b-a).magnitude,depth),m);t.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
        }
        static Material Mat(string name,Color color,float metal,float smooth)
        {
            string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
        }
        static void Batch(Transform p,string prefix)
        {
            var sources=p.GetComponentsInChildren<MeshFilter>().Where(f=>f.name!="ConveyorBelt").ToArray();
            var temporaryMeshes=sources.Select(f=>f.sharedMesh).Where(m=>m!=null&&!EditorUtility.IsPersistent(m)).Distinct().ToArray();
            foreach(var group in sources.GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial)){
                var mesh=new Mesh{indexFormat=IndexFormat.UInt32,name=prefix+"_"+group.Key.name};
                mesh.CombineMeshes(group.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=p.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);
                string path=Folder+"/"+mesh.name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(old!=null){UpdateMesh(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);mesh=old;EditorUtility.SetDirty(old);}else AssetDatabase.CreateAsset(mesh,path);
                var g=new GameObject(prefix+"_"+group.Key.name);g.transform.SetParent(p,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=group.Key;
            }
            foreach(var f in sources) UnityEngine.Object.DestroyImmediate(f.gameObject);
            foreach(var mesh in temporaryMeshes) UnityEngine.Object.DestroyImmediate(mesh);
        }
    }
}
