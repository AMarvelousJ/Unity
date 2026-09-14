using System; using System.Linq; using System.Threading.Tasks; using UnityEngine; using UnityEditor; using UnityEngine.InputSystem; using UnityEngine.InputSystem.LowLevel; using ZCJ.Shiploader;
public static class XboxFleetCheck {
static GamepadState Button(GamepadState state, GamepadButton button) => state.WithButton(button); static ShiploaderFleetController testFleet; static async Task Send(Gamepad pad, GamepadState state, int ms=220){for(int elapsed=0;elapsed<ms;elapsed+=80){InputSystem.QueueStateEvent(pad,state);InputSystem.Update();testFleet.ProcessInput(pad,true);await Task.Delay(80);}}
public static async Task<object> Main() {
var gameType=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");var game=EditorWindow.GetWindow(gameType);game.Show();game.Focus();
var fleet=UnityEngine.Object.FindFirstObjectByType<ShiploaderFleetController>();fleet.enabled=false;testFleet=fleet;fleet.Select(0);
var physical=Gamepad.all.Select(p=>p.displayName).ToArray();var previous=Gamepad.current;var pad=InputSystem.AddDevice<Gamepad>();pad.MakeCurrent();
try {
await Send(pad,new GamepadState());

await Send(pad,new GamepadState().WithButton(GamepadButton.West));await Send(pad,new GamepadState());
for(int i=0;i<30&&!fleet.Selected.IsManual;i++)await Task.Delay(100);
if(!fleet.Selected.IsManual)throw new Exception("Xbox X did not enter manual: "+fleet.Selected.ErrorMessage);
var before=fleet.Selected.Rig.Pose;
var motion=new GamepadState {leftStick=new Vector2(.6f,.8f),rightStick=new Vector2(0,.6f),rightTrigger=.8f};motion=motion.WithButton(GamepadButton.DpadUp);
await Send(pad,motion,1500);await Send(pad,new GamepadState(),350);
var after=fleet.Selected.Rig.Pose;
if(Mathf.Abs(after.travel-before.travel)<.05f||Mathf.Abs(after.slew-before.slew)<.05f||Mathf.Abs(after.boomLuff-before.boomLuff)<.05f||Mathf.Abs(after.boomExtension-before.boomExtension)<.02f||Mathf.Abs(after.chuteRotate-before.chuteRotate)<.05f)throw new Exception("Five-axis mapping did not move all axes");
var old=fleet.Selected;
await Send(pad,Button(motion,GamepadButton.RightShoulder));
if(fleet.Selected.MachineId!="02")throw new Exception("RB did not select next machine");
await Send(pad,Button(motion,GamepadButton.West));
await Send(pad,motion,600);
if(!fleet.Selected.IsManual)throw new Exception("Second X failed: "+fleet.Selected.MachineId+" busy="+fleet.Selected.IsBusy+" error="+fleet.Selected.ErrorMessage);
var blocked=fleet.Selected.Rig.Pose;await Send(pad,motion,600);
if(!blocked.Approximately(fleet.Selected.Rig.Pose))throw new Exception("Held stick moved newly selected machine before neutral");
await Send(pad,new GamepadState(),300);var secondBefore=fleet.Selected.Rig.Pose;
await Send(pad,motion,600);await Send(pad,new GamepadState(),350);
if(secondBefore.Approximately(fleet.Selected.Rig.Pose))throw new Exception("Input remained blocked after neutral: "+fleet.Selected.MachineId+" manual="+fleet.Selected.IsManual+" error="+fleet.Selected.ErrorMessage+" neutral="+typeof(ShiploaderFleetController).GetField("neutralRequired",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(fleet));
if(old.IsManual)throw new Exception("Old machine retained input");
await Send(pad,new GamepadState().WithButton(GamepadButton.North));await Send(pad,new GamepadState());
var orbit=UnityEngine.Object.FindFirstObjectByType<ShiploaderOrbitCamera>();
if(orbit.Preset!=ShiploaderCameraPreset.Discharge)throw new Exception("Y did not change view");
var ui=UnityEngine.Object.FindFirstObjectByType<ShiploaderDemoUI>();var collapsed=typeof(ShiploaderDemoUI).GetField("remoteCollapsed",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
bool oldPanel=(bool)collapsed.GetValue(ui);await Send(pad,new GamepadState().WithButton(GamepadButton.Start));await Send(pad,new GamepadState());
if(oldPanel==(bool)collapsed.GetValue(ui)||orbit.Walkthrough.Active)throw new Exception("Menu conflicts with walkthrough");
return new {passed=true,physicalDevices=physical,manualBefore=before,manualAfter=after,checks="X, five axes, RB, neutral gate, old input release, Y, Menu"};
}finally{await Send(pad,new GamepadState());await fleet.Selected.StopManualAsync();InputSystem.RemoveDevice(pad);previous?.MakeCurrent();fleet.Select(0);fleet.enabled=true;}
}
}
