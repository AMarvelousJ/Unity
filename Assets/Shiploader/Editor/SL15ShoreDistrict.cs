using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ZCJ.Shiploader.Editor
{
 public static partial class SL15ReferenceModel
 {
  [MenuItem("Tools/SL15/Build Shore Operations District")]
  public static void BuildShoreOperationsDistrict()
  {
   if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode first");
   InitQuayMaterials();
   string original=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
   EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
   foreach(string path in new[]{SL15SceneBuilder.ScenePath,"Assets/Shiploader/Scenes/SL15_VideoPreview.unity"}) {
    var scene=EditorSceneManager.OpenScene(path);var env=GameObject.Find("PortEnvironment");
    VideoEnvironment(env,260,false,false);BuildShoreDistrict(env.transform);
    env.transform.Find("VIS_VideoSea/HarborWater").localScale=new Vector3(600,1,600);
    RenderSettings.fogDensity=.0006f;Camera.main.farClipPlane=2500;
    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
   }
   AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(original);
   var view=SceneView.lastActiveSceneView;
   if(view!=null){view.sceneViewState.showImageEffects=false;view.LookAt(new Vector3(-130,0,0),Quaternion.Euler(50,-70,0),235,false,true);}
   Debug.Log("Shore district saved to both scenes: 3 coal yards, 3 connected conveyor routes, roads and service facilities.");
  }

  static void BuildShoreDistrict(Transform env)
  {
   var root=Owned(env,"ShoreOperationsDistrict");
   var concrete=Mat("ShoreConcrete",new Color32(123,130,126,255),.02f,.17f);
   var asphalt=Mat("ShoreAsphalt",new Color32(48,55,58,255),0,.12f);
   var line=Mat("RoadMarking",new Color32(208,204,178,255),0,.12f);
   var coal=Mat("StockyardCoal",new Color32(32,35,35,255),0,.09f);
   coal.SetTexture("_BaseMap",SurfaceTexture("CoalGrain",true));
   var grass=Mat("ShorePlanting",new Color32(62,85,54,255),0,.05f);
   var buildings=Mat("ShoreBuilding",new Color32(174,183,180,255),.02f,.2f);
   var window=Mat("ShoreWindow",new Color32(40,66,77,255),.05f,.15f);
   var ground=Owned(root,"GroundRoadsAndWaterfront");
   Box(ground,"LandFoundation",new Vector3(-150,-1.05f,0),new Vector3(140,2.1f,600),concrete);
   // Two longitudinal roads with three cross streets keep work areas accessible.
   foreach(float x in new[]{-91f,-193f}) {
    Box(ground,"ServiceRoad",new Vector3(x,.035f,0),new Vector3(11,.07f,585),asphalt);
    foreach(float dx in new[]{-5f,5f})Box(ground,"RoadEdge",new Vector3(x+dx,.08f,0),new Vector3(.12f,.025f,580),line);
    for(float z=-285;z<285;z+=10)Box(ground,"RoadCenterDash",new Vector3(x,.083f,z),new Vector3(.14f,.025f,4),line);
   }
   foreach(float z in new[]{-245f,-78f,78f,245f}) {
    Box(ground,"CrossStreet",new Vector3(-143,.05f,z),new Vector3(105,.09f,9),asphalt);
    for(float x=-185;x<-94;x+=10)Box(ground,"CrossStreetDash",new Vector3(x,.11f,z),new Vector3(4,.02f,.14f),line);
   }
   Box(ground,"Seawall",new Vector3(-80,-.75f,0),new Vector3(1.2f,2.1f,600),steel);
   foreach(float z in new[]{-300f,300f})Box(ground,"ShoreEndCap",new Vector3(-150,-.55f,z),new Vector3(140,1.8f,1),concrete);
   foreach(float z in new[]{-156f,156f}) {
    Rail(ground,new Vector3(-80, .4f,z-140),new Vector3(-80,.4f,z+140));
    Box(ground,"DrainChannel",new Vector3(-98,.055f,z),new Vector3(.45f,.08f,280),steel);
   }
   for(float z=-280;z<=280;z+=28) {
    Box(ground,"RearFencePost",new Vector3(-218,1.3f,z),new Vector3(.12f,2.6f,.12f),steel);
    if(z<280)foreach(float y in new[]{.6f,1.4f,2.2f})Box(ground,"RearFenceRail",new Vector3(-218,y,z+14),new Vector3(.055f,.055f,28),steel);
   }
   Batch(ground,"ShoreGround");
   for(int i=0;i<3;i++) {
    float z=(i-1)*160f;var yard=Owned(root,"CoalYard_0"+(i+1));
    Box(yard,"YardPavement",new Vector3(-154,.09f,z),new Vector3(65,.18f,128),asphalt);
    foreach(float x in new[]{-186f,-122f})Box(yard,"RetainingWall",new Vector3(x,1,z),new Vector3(.55f,2,128),concrete);
    foreach(float dz in new[]{-64f,64f})Box(yard,"EndWall",new Vector3(-154,1,z+dz),new Vector3(64,2,.55f),concrete);
    var mound=new GameObject("CoalStockpile");mound.transform.SetParent(yard,false);mound.transform.localPosition=new Vector3(-155,.19f,z);
    mound.AddComponent<MeshFilter>().sharedMesh=ShoreCoalMesh();mound.AddComponent<MeshRenderer>().sharedMaterial=coal;
    mound.transform.localScale=new Vector3(54,11+i*2,115);
    for(float dz=-54;dz<=54;dz+=18) {
     Box(yard,"DustSprayMast",new Vector3(-123,3.5f,z+dz),new Vector3(.12f,7,.12f),steel);
     BoxBeam(yard,"SprayNozzle",new Vector3(-123,7,z+dz),new Vector3(-125,7,z+dz),.1f,.1f,blue);
    }
    Batch(yard,"ShoreYard_0"+(i+1));
    var route=Owned(root,"ShoreFeedRoute_0"+(i+1));float lane=TripleLanes[i],rx=-106-i*5.4f,h=5.98f+i*2.5f;
    ShoreBelt(route,new Vector3(-57.5f,5.98f,lane),new Vector3(rx,h,lane));
    if(Mathf.Abs(z-lane)>1)ShoreBelt(route,new Vector3(rx,h,lane),new Vector3(rx,h,z));
    ShoreBelt(route,new Vector3(rx,h,z),new Vector3(-126,8,z));
    Box(route,"TransferHousing",new Vector3(rx,h+1,lane),new Vector3(4.5f,2.4f,4.5f),blue);
    foreach(float dx in new[]{-2f,2f})foreach(float dz in new[]{-2f,2f})Box(route,"TransferColumn",new Vector3(rx+dx,h/2,lane+dz),new Vector3(.25f,h,.25f),red);
    // Small stacker at each feed destination, with a raised boom over its stockpile.
    Box(route,"StackerChassis",new Vector3(-126,1.2f,z),new Vector3(4,1,6),red);
    Box(route,"StackerTower",new Vector3(-126,6,z),new Vector3(2,9,2),red);
    ShoreBelt(route,new Vector3(-126,8,z),new Vector3(-156,17,z+22),false);
    BoxBeam(route,"StackerStay",new Vector3(-126,13,z),new Vector3(-156,17,z+22),.13f,.13f,steel);
    Box(route,"StackerCab",new Vector3(-123,8,z),new Vector3(2.3f,2.2f,2.8f),buildings);
    Box(route,"CabWindow",new Vector3(-121.83f,8.2f,z),new Vector3(.05f,1.2f,2.3f),window);
    Batch(route,"ShoreFeed_0"+(i+1));
   }
   var facilities=Owned(root,"ServiceBuildingsAndEquipment");
   foreach(float z in new[]{-205f,-155f,155f,205f})ShoreBuilding(facilities,new Vector3(-206,0,z),18,34,7,buildings,window);
   ShoreBuilding(facilities,new Vector3(-205,0,-24),21,32,11,buildings,window);
   ShoreBuilding(facilities,new Vector3(-205,0,27),17,18,5,buildings,window);
   foreach(float z in new[]{-270f,270f}) {
    Box(facilities,"PlantingBed",new Vector3(-156,.15f,z),new Vector3(65,.3f,17),grass);
    for(float x=-180;x<-125;x+=11) {
     Cylinder(facilities,"TreeTrunk",new Vector3(x,1.8f,z),.35f,3.6f,edge);
     var crown=GameObject.CreatePrimitive(PrimitiveType.Sphere);crown.name="TreeCrown";UnityEngine.Object.DestroyImmediate(crown.GetComponent<Collider>());
     crown.transform.SetParent(facilities,false);crown.transform.localPosition=new Vector3(x,4,z);crown.transform.localScale=new Vector3(4.5f,5.8f,4.5f);crown.GetComponent<MeshRenderer>().sharedMaterial=grass;
    }
   }
   for(float z=-275;z<=275;z+=50)foreach(float x in new[]{-82f,-190f}) {
    Cylinder(facilities,"HighMastLight",new Vector3(x,9,z),.23f,18,steel);
    Box(facilities,"LightCrossarm",new Vector3(x,18,z),new Vector3(3,.16f,.2f),steel);
    foreach(float dx in new[]{-1f,0f,1f})Box(facilities,"Floodlight",new Vector3(x+dx,17.8f,z),new Vector3(.55f,.3f,.7f),white);
   }
   foreach(float z in new[]{-224f,-122f,115f,224f})ShoreTruck(facilities,new Vector3(-91,0,z));
   for(int i=0;i<7;i++) {
    float z=-52+i*7;Box(facilities,"ParkingBay",new Vector3(-183,.12f,z),new Vector3(7,.025f,.12f),line);
    if(i%2==0)ShoreTruck(facilities,new Vector3(-180,0,z+3));
   }
   Batch(facilities,"ShoreFacilities");
  }

  static void ShoreDeckBeam(Transform p,string name,Vector3 a,Vector3 b,float width,float thickness,Material material)
  {
   Vector3 direction=(b-a).normalized,side=Vector3.Cross(direction,Vector3.up).normalized;
   var part=Box(p,name,(a+b)/2,new Vector3(width,Vector3.Distance(a,b),thickness),material);
   part.localRotation=Quaternion.LookRotation(Vector3.Cross(side,direction),direction);
  }
  static void ShoreBelt(Transform p,Vector3 a,Vector3 b,bool supports=true)
  {
   Vector3 d=(b-a).normalized,side=Vector3.Cross(d,Vector3.up).normalized;
   // Local Y follows the belt; local X is horizontal across it.
   ShoreDeckBeam(p,"CoveredBelt",a,b,2.7f,.16f,black);ShoreDeckBeam(p,"BeltRoof",a+Vector3.up*.9f,b+Vector3.up*.9f,3,.16f,blue);
   foreach(float s in new[]{-1f,1f}) {
    ShoreDeckBeam(p,"BeltSide",a+side*1.5f*s,b+side*1.5f*s,.16f,1.2f,blue);
    ShoreDeckBeam(p,"Walkway",a+side*2*s-Vector3.up*.6f,b+side*2*s-Vector3.up*.6f,.7f,.12f,deck);
    Rail(p,a+side*2.35f*s-Vector3.up*.5f,b+side*2.35f*s-Vector3.up*.5f);
   }
   int n=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/8));
   for(int i=0;i<=n;i++) {
    Vector3 at=Vector3.Lerp(a,b,i/(float)n);
    if(!supports||Mathf.Abs(at.x+91)<7)continue;
    foreach(float s in new[]{-1f,1f}) {
     Vector3 top=at+side*1.8f*s-Vector3.up*.8f;
     BoxBeam(p,"Trestle",new Vector3(top.x,.2f,top.z),top,.18f,.18f,steel);
    }
    BoxBeam(p,"CrossBrace",at-side*1.8f-Vector3.up*.8f,at+side*1.8f-Vector3.up*.8f,.18f,.2f,red);
   }
  }
  static Mesh ShoreCoalMesh()
  {
   var vv=new List<Vector3>();var uv=new List<Vector2>();var tt=new List<int>();const int nx=20,nz=40;
   for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++) {
    float u=x/(float)nx,v=z/(float)nz;
    float height=Mathf.Pow(Mathf.Max(0,Mathf.Sin(u*Mathf.PI)),.85f)*Mathf.Pow(Mathf.Max(0,Mathf.Sin(v*Mathf.PI)),.35f);
    height*=.88f+.12f*Mathf.PerlinNoise(u*9,v*13);
    vv.Add(new Vector3(u-.5f,height,v-.5f));uv.Add(new Vector2(u*5,v*10));
   }
   for(int z=0;z<nz;z++)for(int x=0;x<nx;x++){int a=z*(nx+1)+x;tt.AddRange(new[]{a,a+nx+1,a+1,a+1,a+nx+1,a+nx+2});}
   var mesh=new Mesh();mesh.SetVertices(vv);mesh.SetUVs(0,uv);mesh.SetTriangles(tt,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return PersistMesh(mesh,"ShoreCoalHeap");
  }
  static void ShoreBuilding(Transform p,Vector3 at,float width,float length,float height,Material wall,Material window)
  {
   Box(p,"Workshop",at+Vector3.up*height/2,new Vector3(width,height,length),wall);
   foreach(float s in new[]{-1f,1f}) {
    var roof=Box(p,"PitchedRoof",at+new Vector3(s*width/4,height+width*.06f,0),new Vector3(width*.515f,.2f,length+1),blue);roof.localRotation=Quaternion.Euler(0,0,-s*14);
   }
   for(float z=-length/2+3;z<length/2-2;z+=6) {
    Box(p,"ServiceDoor",at+new Vector3(width/2+.04f,2,z),new Vector3(.08f,4,3.7f),deck);
    if(height>8)Box(p,"UpperWindows",at+new Vector3(width/2+.06f,height-2,z),new Vector3(.08f,1.5f,4),window);
   }
  }
  static void ShoreTruck(Transform p,Vector3 at)
  {
   Box(p,"TruckChassis",at+Vector3.up*.8f,new Vector3(2.1f,.4f,5.5f),steel);
   Box(p,"TruckCab",at+new Vector3(0,1.8f,1.7f),new Vector3(2.1f,1.8f,1.7f),yellow);
   Box(p,"TruckWindshield",at+new Vector3(0,2.1f,2.57f),new Vector3(1.8f,.7f,.05f),glass??steel);
   Box(p,"TruckBed",at+new Vector3(0,1.5f,-.9f),new Vector3(2.2f,1.1f,3.3f),blue);
   foreach(float x in new[]{-1.12f,1.12f})foreach(float z in new[]{-1.8f,1.7f})Cylinder(p,"TruckWheel",at+new Vector3(x,.6f,z),1,.3f,black).localRotation=Quaternion.Euler(0,0,90);
  }
 }
}
