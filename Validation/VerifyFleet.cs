using System; using System.Linq; using UnityEngine; using UnityEditor; using UnityEditor.SceneManagement; using ZCJ.Shiploader;
public static class VerifyFleet { public static object Main() {
string original=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
var results=new System.Collections.Generic.List<object>();
try { foreach(string path in new[]{"Assets/Shiploader/Scenes/SL15_Demo.unity","Assets/Shiploader/Scenes/SL15_VideoPreview.unity"}) {
EditorSceneManager.OpenScene(path);
var coordinators=UnityEngine.Object.FindObjectsByType<ShiploaderRemoteCoordinator>(FindObjectsSortMode.None).OrderBy(c=>c.MachineId).ToArray();
if(coordinators.Length!=7)throw new Exception("Wrong coordinator count");
foreach(var c in coordinators) {
 var r=c.Rig; var saved=r.Pose; var basePosition=r.transform.position-Vector3.right*saved.travel;
 foreach(var p in new[]{new ShiploaderPose(10,0,12,3,30),new ShiploaderPose(-10,90,-2,7,-50),new ShiploaderPose(0,180,20,15,80)}) {
 r.ApplyPose(p);
 if(r.GetMaximumKeyPointError()>.01f)throw new Exception(c.MachineId+" keypoint mismatch "+r.GetMaximumKeyPointError());
 if(Vector3.Distance(r.transform.position,basePosition+Vector3.right*p.travel)>.01f)throw new Exception("Rig base changed");
 }
 r.ApplyPose(saved);
 int n=int.Parse(c.MachineId);
 if(c.Covers.Length != (n<=3?9:n<=5?5:4)) throw new Exception("Covers not scoped "+c.MachineId);
 int rings=r.GetComponentsInChildren<Transform>(true).Count(t=>t.name.StartsWith("VIS_DualSideTurntable_"));
 if(rings!=(n<=3?2:0))throw new Exception("Wrong circular platforms "+c.MachineId);
 results.Add(new {scene=path,machine=c.MachineId,covers=c.Covers.Length,error=r.GetMaximumKeyPointError(),rings});
}
}}finally{EditorSceneManager.OpenScene(original);}return results;
} }
