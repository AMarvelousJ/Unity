using System; using System.Linq; using System.Threading.Tasks; using UnityEngine; using ZCJ.Shiploader;
public static class FleetNetworkCheck { public static async Task<object> Main() {
var fleet=UnityEngine.Object.FindFirstObjectByType<ShiploaderFleetController>();
fleet.enabled=false;
var cs=fleet.Machines;
for(int i=0;i<100 && cs.Any(c=>!c.IsConnected);i++)await Task.Delay(100);
if(cs.Any(c=>!c.IsConnected))throw new Exception("Connection failed: "+string.Join(";",cs.Select(c=>c.MachineId+":"+c.ErrorMessage)));
try {
foreach(var c in cs.Take(3))await c.SelectVesselAsync("Vessel-R");
await Task.WhenAll(cs.Select(c=>c.StartLoadingTaskAsync()));
if(cs.Any(c=>c.CurrentTask==null))throw new Exception("Start failed: "+string.Join(";",cs.Select(c=>c.MachineId+":"+c.ErrorMessage)));
for(int i=0;i<450 && !(cs[0].CurrentTask?.status=="loading"&&cs[0].CurrentTask.loadedTotalKg>0);i++)await Task.Delay(100);
var first=cs[0]; if(first.CurrentTask?.status!="loading")throw new Exception("Did not reach loading: "+first.CurrentTask?.status+" "+first.ErrorMessage);
await first.EnterManualAsync(); await Task.Delay(300);
if(!first.IsManual || first.CurrentTask.status!="paused")throw new Exception("Manual takeover failed");
long tonnage=first.CurrentTask.loadedTotalKg;
var before=first.Rig.Pose;
for(int i=0;i<15;i++){await first.JogAsync(.8f,.6f,.4f,.8f,.5f);await Task.Delay(80);}
await first.JogAsync(0,0,0,0,0); await Task.Delay(300);
var after=first.Rig.Pose;
if(after.Approximately(before))throw new Exception("Manual did not move rig");
if(first.CurrentTask.loadedTotalKg!=tonnage)throw new Exception("Cargo advanced while paused");
fleet.Select(1);
await Task.Delay(500);
if(first.IsManual)throw new Exception("Selection did not release manual input");
if(cs.Skip(1).Any(c=>c.CurrentTask.status=="paused"))throw new Exception("Other machines paused");
await first.ResumeLoadingTaskAsync(); await Task.Delay(500);
if(first.CurrentTask.status!="positioning"&&first.CurrentTask.status!="loading")throw new Exception("Resume failed: "+first.ErrorMessage);
var result=new {connected=cs.Count(c=>c.IsConnected),manualBefore=before,manualAfter=after,preservedKg=tonnage,status=cs.Select(c=>new {id=c.MachineId,status=c.CurrentTask.status,kg=c.CurrentTask.loadedTotalKg,error=c.ErrorMessage}).ToArray()};
return result;
} finally {
foreach(var c in cs){await c.StopManualAsync(); if(c.CurrentTask!=null&&!c.CurrentTask.IsTerminal)await c.CancelLoadingTaskAsync(); if(c.CurrentTask!=null&&c.CurrentTask.IsTerminal)await c.ResetLoadingTaskAsync();}
fleet.Select(0);fleet.enabled=true;
}
} }
