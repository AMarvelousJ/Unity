using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ZCJ.Shiploader.Editor
{
 public static partial class SL15ReferenceModel
 {
  [MenuItem("Tools/SL15/Fix Conveyor Outer Supports And End Stop")]
  public static void FixConveyorOuterSupportsAndEndStop()
  {
   if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode first");
   InitQuayMaterials();string original=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
   EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
   foreach(string path in new[]{SL15SceneBuilder.ScenePath,"Assets/Shiploader/Scenes/SL15_VideoPreview.unity"}) {
    var scene=EditorSceneManager.OpenScene(path);BuildSharedQuayConveyors(GameObject.Find("PortEnvironment").transform);
    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
   }
   AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(original);
   Debug.Log("Both scenes: only two outside support rows, shared crossheads, blue terminal enclosure.");
  }
  static void BuildSharedQuayConveyors(Transform env)
  {
   var corridors=Owned(env,"ThreeLongitudinalConveyors");
   var belt=Owned(corridors,"Conveyor_01");BuildQuayConveyor(belt,260,false);Batch(belt,"TripleBeltWithoutColumns");
   for(int i=1;i<3;i++) {
    var copy=UnityEngine.Object.Instantiate(belt.gameObject,corridors).transform;
    copy.name="Conveyor_0"+(i+1);copy.localPosition=Vector3.forward*TripleLanes[i];
   }
   var frame=Owned(env,"SharedConveyorFrame");
   var material=Mat("QuayConveyorSupport",new Color32(86,103,92,255),.18f,.22f);
   foreach(float z in new[]{-7.2f,7.2f}) {
    var row=Owned(frame,z<0?"OutsideRowLeft":"OutsideRowRight");
    for(float x=-56.5f;x<337.5f;x+=4.5f) {
     Box(row,"OuterColumn",new Vector3(x,2.43f,z),new Vector3(.25f,4.7f,.28f),material);
     Box(row,"OuterBasePlate",new Vector3(x,.1f,z),new Vector3(.65f,.12f,.65f),material);
     foreach(float dx in new[]{-.23f,.23f})Cylinder(row,"AnchorBolt",new Vector3(x+dx,.2f,z),.065f,.12f,steel);
     BoxBeam(row,"OuterKnee",new Vector3(x,3.5f,z),new Vector3(x+.8f,4.7f,z),.12f,.14f,material);
     if(x+4.5f<337.5f)BoxBeam(row,"OuterDiagonal",new Vector3(x,.6f,z),new Vector3(x+4.5f,4.7f,z),.1f,.12f,material);
    }
    Batch(row,z<0?"ConveyorOutsideLeft":"ConveyorOutsideRight");
   }
   var crossheads=Owned(frame,"SharedCrossheads");
   for(float x=-56.5f;x<337.5f;x+=4.5f) {
    Box(crossheads,"SpanningWeb",new Vector3(x,4.55f,0),new Vector3(.18f,.55f,15.8f),material);
    foreach(float y in new[]{4.25f,4.83f})Box(crossheads,"SpanningFlange",new Vector3(x,y,0),new Vector3(.48f,.07f,15.8f),material);
   }
   Batch(crossheads,"ConveyorSharedCrossheads");
   var cap=Owned(env,"ConveyorTerminalEnclosure");
   var casing=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/QuayConveyorLightBlue.mat");
   Box(cap,"TerminalLowerHousing",new Vector3(338.4f,2.25f,0),new Vector3(3.2f,4.5f,14.9f),blue);
   Box(cap,"TerminalHeadCover",new Vector3(337.8f,5.55f,0),new Vector3(4.4f,2.1f,16.2f),casing);
   Box(cap,"TerminalTopLip",new Vector3(337.8f,6.65f,0),new Vector3(4.6f,.14f,16.4f),steel);
   foreach(float z in TripleLanes) {
    Box(cap,"ServiceHatchFrame",new Vector3(340.04f,2.4f,z),new Vector3(.08f,2.4f,2.25f),steel);
    Box(cap,"ServiceHatch",new Vector3(340.1f,2.4f,z),new Vector3(.08f,2.2f,2.05f),casing);
    Box(cap,"HatchHandle",new Vector3(340.17f,2.4f,z+.65f),new Vector3(.12f,.38f,.08f),yellow);
   }
   Batch(cap,"ConveyorTerminal");
  }
 }
}
