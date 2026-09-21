using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace ZCJ.Shiploader.Editor {
public static class YardBucketWheelBuilder {
const string Folder="Assets/Shiploader/Generated/YardReclaimer";
static Material orange,dark,blue,glass,yellow;
static Transform root;
static Material Mat(string n,Color c,float metal=0){string p=Folder+"/"+n+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(p);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,p);}m.color=c;m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",.3f);return m;}
static Transform Group(string n,Transform parent){var g=new GameObject(n);g.transform.SetParent(parent,false);return g.transform;}
static Transform Part(string n,Vector3 p,Vector3 s,Material m,Transform parent,PrimitiveType type=PrimitiveType.Cube){var g=GameObject.CreatePrimitive(type);g.name=n;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=s;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=m;return g.transform;}
static void Beam(string n,Vector3 a,Vector3 b,float width,Material m,Transform p){var t=Part(n,(a+b)/2,new Vector3(width,(b-a).magnitude,width),m,p);t.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
static void Rail(Vector3 a,Vector3 b,Transform p){Beam("Handrail",a+Vector3.up,b+Vector3.up,.055f,yellow,p);Beam("Midrail",a+Vector3.up*.5f,b+Vector3.up*.5f,.04f,yellow,p);int count=Mathf.CeilToInt(Vector3.Distance(a,b)/1.5f);for(int i=0;i<=count;i++){var at=Vector3.Lerp(a,b,i/(float)count);Beam("Post",at,at+Vector3.up,.055f,yellow,p);}}
static void Deck(string n,Vector3 c,Vector3 size,Transform p){Part(n,c,size,orange,p);Rail(c+new Vector3(-size.x/2,.12f,-size.z/2),c+new Vector3(size.x/2,.12f,-size.z/2),p);Rail(c+new Vector3(-size.x/2,.12f,size.z/2),c+new Vector3(size.x/2,.12f,size.z/2),p);}
static void Stairs(Vector3 a,Vector3 b,Transform p){Vector3 flat=b-a;flat.y=0;var across=Vector3.Cross(flat.normalized,Vector3.up)*.6f;int count=Mathf.CeilToInt((b.y-a.y)/.22f);for(int i=0;i<=count;i++){var t=Part("AccessTread",Vector3.Lerp(a,b,i/(float)count),new Vector3(1.2f,.09f,flat.magnitude/count+.1f),dark,p);t.localRotation=Quaternion.LookRotation(flat);}foreach(float s in new[]{-1f,1f}){Beam("StairStringer",a+across*s,b+across*s,.12f,orange,p);Rail(a+across*s,b+across*s,p);}}
static void Cylinder(string n,Vector3 at,float radius,float height,Material m,Transform p,bool axle=false){var t=Part(n,at,new Vector3(radius*2,height/2,radius*2),m,p,PrimitiveType.Cylinder);if(axle)t.localRotation=Quaternion.Euler(90,0,0);}
static GameObject Build(){
var g=new GameObject("Yard_BucketWheelReclaimer_01");root=g.transform;
var baseFrame=Group("RailTravelBase",root);
foreach(float x in new[]{-4f,4f})foreach(float z in new[]{-2.8f,2.8f}){
Part("Bogie",new Vector3(x,1,z),new Vector3(3.3f,.65f,1),orange,baseFrame);
for(int i=0;i<3;i++)Cylinder("TravelWheel",new Vector3(x-1+i,.5f,z),.48f,.8f,dark,baseFrame,true);
Beam("PortalLeg",new Vector3(x,1.2f,z),new Vector3(x*.7f,5.8f,z*.8f),.8f,orange,baseFrame);
}
foreach(float z in new[]{-2.8f,2.8f})Part("TravelGirder",new Vector3(0,4.8f,z),new Vector3(10,.8f,.7f),orange,baseFrame);
Cylinder("SlewBase",new Vector3(0,6,0),4.6f,1,orange,baseFrame);
Cylinder("SlewBearing",new Vector3(0,6.6f,0),3.9f,.3f,dark,baseFrame);
var upper=Group("SlewingSuperstructure",root);upper.localRotation=Quaternion.Euler(0,55,0);
Cylinder("RotatingDeck",new Vector3(0,7,0),4.5f,.5f,orange,upper);
Deck("MainServiceDeck",new Vector3(0,7.3f,0),new Vector3(8,.25f,7),upper);
// Tall asymmetric A-frame and rear counterweight match the reference silhouette.
foreach(float z in new[]{-2f,2f}){
Beam("TowerRearLeg",new Vector3(-3,7.5f,z),new Vector3(-4,23,z),.65f,orange,upper);
Beam("TowerFrontLeg",new Vector3(3,7.5f,z),new Vector3(7,20,z),.65f,orange,upper);
Beam("TowerHead",new Vector3(-4,23,z),new Vector3(7,20,z),.6f,orange,upper);
Beam("TowerDiagonal",new Vector3(-3,8,z),new Vector3(6,17,z),.38f,orange,upper);
Beam("TowerDiagonal",new Vector3(3,8,z),new Vector3(-3.5f,16,z),.38f,orange,upper);
Beam("HighMast",new Vector3(-4,23,z),new Vector3(-3,29,z),.55f,orange,upper);
Beam("HighMastBrace",new Vector3(7,20,z),new Vector3(-3,29,z),.48f,orange,upper);
Beam("RearTie",new Vector3(-3,29,z),new Vector3(-20,22,z),.48f,orange,upper);
Beam("RearLowerChord",new Vector3(-20,22,z),new Vector3(-4,21,z),.55f,orange,upper);
Beam("RearBrace",new Vector3(-9,25.5f,z),new Vector3(-10,21.4f,z),.4f,orange,upper);
Beam("BoomTie",new Vector3(-3,29,z),new Vector3(32,9,z),.36f,orange,upper);
Beam("TieSupport",new Vector3(9,22,z),new Vector3(14,9,z),.16f,yellow,upper);
}
Beam("TowerCrosshead",new Vector3(-3,29,-2),new Vector3(-3,29,2),.7f,orange,upper);
Deck("MachineryFloor",new Vector3(0,16,0),new Vector3(9,.35f,6),upper);
Part("BluePowerHouse",new Vector3(0,17.5f,0),new Vector3(4.8f,2.6f,3.6f),blue,upper);
for(int i=0;i<9;i++)Part("CoolingLouver",new Vector3(-2.42f,16.7f+i*.17f,0),new Vector3(.05f,.06f,2.5f),dark,upper);
Deck("RearCatwalk",new Vector3(-12,21.6f,0),new Vector3(17,.22f,3.5f),upper);
for(int i=0;i<4;i++)Part("Counterweight",new Vector3(-19+i*.95f,22.3f,0),new Vector3(.85f,1.7f,5.5f),orange,upper);
// Horizontal belt boom with box chords, repeating lattice and service walkways.
var boom=Group("LuffingBoom",upper);
for(int i=0;i<12;i++){
float x=i*2.7f;foreach(float z in new[]{-1.15f,1.15f}){
Beam("BoomLowerChord",new Vector3(x,7.8f,z),new Vector3(x+2.7f,7.8f,z),.25f,orange,boom);
Beam("BoomUpperChord",new Vector3(x,9.1f,z),new Vector3(x+2.7f,9.1f,z),.25f,orange,boom);
Beam("BoomLattice",new Vector3(x,7.8f,z),new Vector3(x+2.7f,9.1f,z),.15f,orange,boom);
}Cylinder("BeltRoller",new Vector3(x,8.65f,0),.16f,2.1f,dark,boom,true);
}
Part("ReclaimConveyorBelt",new Vector3(16,8.8f,0),new Vector3(33,.12f,1.9f),dark,boom);
foreach(float z in new[]{-1.8f,1.8f})Deck("BoomWalkway",new Vector3(16,8.3f,z),new Vector3(33,.16f,.9f),boom);
Part("ConveyorDrive",new Vector3(18,9.4f,-2.2f),new Vector3(2,1,1.1f),blue,boom);
Deck("CabPlatform",new Vector3(26,10,2.6f),new Vector3(3.7f,.18f,2.5f),boom);
Part("OperatorCab",new Vector3(26,11.3f,2.6f),new Vector3(2.6f,2.4f,2),blue,boom);
Part("CabFrontGlass",new Vector3(27.31f,11.65f,2.6f),new Vector3(.035f,1.25f,1.7f),glass,boom);
Part("CabSideGlass",new Vector3(26,11.65f,3.61f),new Vector3(2.2f,1.25f,.035f),glass,boom);
var wheel=Group("BucketWheel",boom);wheel.localPosition=new Vector3(34,9,0);
Cylinder("WheelDrum",Vector3.zero,3.4f,1.7f,orange,wheel,true);
Cylinder("WheelHub",new Vector3(0,0,1.1f),.75f,.7f,dark,wheel,true);
for(int i=0;i<10;i++){
float a=i*Mathf.PI*2/10;var radial=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);Beam("WheelSpoke",radial*.6f+Vector3.forward*.95f,radial*3.2f+Vector3.forward*.95f,.22f,orange,wheel);
var bucket=Group("DiggingBucket_"+i,wheel);bucket.localPosition=radial*3.8f;bucket.localRotation=Quaternion.Euler(0,0,a*Mathf.Rad2Deg-90);
Part("BucketBack",new Vector3(0,0,-.85f),new Vector3(1.5f,1.5f,.13f),orange,bucket);
Part("BucketFloor",new Vector3(0,-.65f,0),new Vector3(1.5f,.16f,1.9f),orange,bucket);
foreach(float s in new[]{-1f,1f})Part("BucketSide",new Vector3(s*.7f,0,0),new Vector3(.12f,1.5f,1.9f),orange,bucket);
for(int j=0;j<3;j++)Part("CuttingTooth",new Vector3(-.5f+j*.5f,-.62f,1.12f),new Vector3(.17f,.22f,.5f),dark,bucket);
}
Deck("AccessLanding",new Vector3(-1,3.7f,6),new Vector3(4,.2f,2.5f),baseFrame);
Stairs(new Vector3(5,.1f,6),new Vector3(1,3.8f,6),baseFrame);Stairs(new Vector3(-2,3.8f,6),new Vector3(-4,7.4f,3),baseFrame);
Part("ElectricalCabinet",new Vector3(-1,4.6f,6),new Vector3(1.2f,1.6f,1),blue,baseFrame);
return g;
}
[MenuItem("Tools/SL15/Build Reference Bucket Wheel Reclaimer")]
public static void Install(){
if(EditorApplication.isPlaying)throw new Exception("Exit Play mode first");
System.IO.Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
orange=Mat("SafetyOrange",new Color(.94f,.27f,.075f),.22f);dark=Mat("BeltAndSteel",new Color(.08f,.105f,.12f),.3f);blue=Mat("EquipmentBlue",new Color(.025f,.55f,.79f),.15f);glass=Mat("CabGlass",new Color(.075f,.2f,.27f),.55f);yellow=Mat("RailOrange",new Color(1,.52f,.16f),.2f);
string original=SceneManager.GetActiveScene().path;
string backup="Temp/reference-port/reclaimer-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");System.IO.Directory.CreateDirectory(backup);
EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),backup+"/ActiveScene.unity",true);EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
foreach(string path in new[]{"Assets/Shiploader/Scenes/SL15_Demo.unity","Assets/Shiploader/Scenes/SL15_VideoPreview.unity"})System.IO.File.Copy(path,backup+"/"+System.IO.Path.GetFileName(path));
var model=Build();var prefab=PrefabUtility.SaveAsPrefabAsset(model,Folder+"/BucketWheelReclaimer.prefab");UnityEngine.Object.DestroyImmediate(model);
try {foreach(string path in new[]{"Assets/Shiploader/Scenes/SL15_Demo.unity","Assets/Shiploader/Scenes/SL15_VideoPreview.unity"}){
var scene=EditorSceneManager.OpenScene(path);var yard=GameObject.Find("PortEnvironment/AerialReferencePort");if(yard==null)throw new Exception("Aerial stockyard missing");
var old=yard.transform.Find("Yard_BucketWheelReclaimer_01") ?? yard.transform.Find("BucketWheelReclaimer");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);instance.name="Yard_BucketWheelReclaimer_01";instance.transform.SetParent(yard.transform,false);instance.transform.localPosition=new Vector3(-475,0,-49);EditorSceneManager.SaveScene(scene);
}}finally{EditorSceneManager.OpenScene(original);}
AssetDatabase.SaveAssets();var target=GameObject.Find("Yard_BucketWheelReclaimer_01");Selection.activeGameObject=target;
if(target!=null) SceneView.lastActiveSceneView?.LookAt(target.transform.position+new Vector3(7,13,-9),Quaternion.Euler(22,35,0),48,false,true);
Debug.Log("Reference bucket wheel reclaimer saved to both scenes; backup="+backup);
}
}}
