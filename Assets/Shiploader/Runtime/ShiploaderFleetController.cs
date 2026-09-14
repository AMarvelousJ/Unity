using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZCJ.Shiploader
{
    [DisallowMultipleComponent]
    public sealed class ShiploaderFleetController : MonoBehaviour
    {
        [SerializeField] private ShiploaderRemoteCoordinator[] machines = Array.Empty<ShiploaderRemoteCoordinator>();
        [SerializeField] private ShiploaderDemoUI ui;
        [SerializeField] private ShiploaderOrbitCamera orbit;
        private int selected;
        private bool neutralRequired = true;
        private bool hadFocus;
        private bool wasWalking;
        private float nextJog;
        private int previousButtons;
        public float[] UiInput { get; } = new float[5];
        public ShiploaderRemoteCoordinator Selected => machines.Length == 0 ? null : machines[selected];
        public ShiploaderRemoteCoordinator[] Machines => machines;
        public string SelectionText => Selected == null ? "" : $"{Selected.MachineId} 号装船机 · " +
            (Selected.IsManual ? "设备手动控制" : "自动作业 / 观察") + " · " +
            (Selected.NeedsSide ? "请先选择左右船" : Selected.SelectedVessel == "Vessel-R" ? "左侧船（面向海侧）" : "右侧船（面向海侧）");

        public void Configure(ShiploaderRemoteCoordinator[] list, ShiploaderDemoUI view, ShiploaderOrbitCamera camera)
        { machines = list; ui = view; orbit = camera; }
        private void Start() { Bind(); }
        private void Bind()
        {
            if (Selected == null) return;
            ui.Configure(Selected.Rig, Selected.Effects, orbit, Selected);
        }
        public void Select(int index)
        {
            if (machines.Length == 0) return;
            _ = Selected.StopManualAsync();
            selected = (index + machines.Length) % machines.Length;
            Array.Clear(UiInput, 0, UiInput.Length);
            neutralRequired = true;
            Bind();
            orbit.SetPreset(ShiploaderCameraPreset.Work);
        }
        public void Next(int delta) => Select(selected + delta);
        public void Manual() { neutralRequired = true; _ = Selected.EnterManualAsync(); ui.ExpandControls(); }
        public void StartOrResume()
        {
            neutralRequired = true;
            if (Selected.CurrentTask?.status == "paused") _ = Selected.ResumeLoadingTaskAsync();
            else _ = Selected.StartLoadingTaskAsync();
        }
        public void SuspendInput() { neutralRequired = true; if (Selected != null) _ = Selected.StopManualAsync(); }
        public static bool HasInputFocus()
        {
            if (!Application.isFocused) return false;
#if UNITY_EDITOR
            if (UnityEditor.EditorWindow.focusedWindow == null || UnityEditor.EditorWindow.focusedWindow.GetType().Name != "GameView") return false;
#endif
            return true;
        }
        private void Update()
        { ProcessInput(Gamepad.current, HasInputFocus()); }

        // Separate the focus gate from device processing so deterministic input
        // tests can exercise the exact runtime mapping without foregrounding the OS.
        public void ProcessInput(Gamepad pad, bool focus)
        {
            if (Selected == null) return;
            int buttons = pad == null ? 0 :
                (pad.leftShoulder.isPressed ? 1 : 0) | (pad.rightShoulder.isPressed ? 2 : 0) |
                (pad.buttonWest.isPressed ? 4 : 0) | (pad.buttonSouth.isPressed ? 8 : 0) |
                (pad.buttonEast.isPressed ? 16 : 0) | (pad.buttonNorth.isPressed ? 32 : 0) |
                (pad.startButton.isPressed ? 64 : 0) | (pad.dpad.left.isPressed ? 128 : 0) |
                (pad.dpad.right.isPressed ? 256 : 0);
            int pressed = buttons & ~previousButtons;
            previousButtons = buttons;
            bool Pressed(int bit) => (pressed & (1 << bit)) != 0;
            bool walking = orbit.Walkthrough != null && orbit.Walkthrough.Active;
            bool justLeftWalking = wasWalking && !walking;
            wasWalking = walking;
            if (!focus || walking || justLeftWalking) {
                if (hadFocus) SuspendInput();
                hadFocus = false; Array.Clear(UiInput, 0, 5); return;
            }
            hadFocus = true;
            if (pad != null) {
                if (Pressed(0)) { Next(-1); return; }
                if (Pressed(1)) { Next(1); return; }
                if (Pressed(2)) { Manual(); return; }
                if (Pressed(3)) { StartOrResume(); return; }
                if (Pressed(4)) { SuspendInput(); _ = Selected.PauseLoadingTaskAsync(); return; }
                if (Pressed(5))
                    orbit.SetPreset(orbit.Preset == ShiploaderCameraPreset.Work ? ShiploaderCameraPreset.Discharge : ShiploaderCameraPreset.Work);
                if (Pressed(6)) ui.ToggleOperationPanel();
                if (int.Parse(Selected.MachineId) <= 3) {
                    if (Pressed(7)) _ = Selected.SelectVesselAsync("Vessel-R");
                    if (Pressed(8)) _ = Selected.SelectVesselAsync("Vessel-L");
                }
            }
            Vector2 left = pad?.leftStick.ReadValue() ?? Vector2.zero;
            Vector2 right = pad?.rightStick.ReadValue() ?? Vector2.zero;
            float extension = pad == null ? 0 : pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue();
            float chute = pad?.dpad.ReadValue().y ?? 0;
            bool neutral = left.sqrMagnitude < .01f && right.sqrMagnitude < .01f && Mathf.Abs(extension) < .1f && Mathf.Abs(chute) < .1f;
            if (neutralRequired && neutral) neutralRequired = false;
            if (Selected.IsManual && Time.unscaledTime >= nextJog) {
                nextJog = Time.unscaledTime + .08f;
                _ = Selected.JogAsync(
                    neutralRequired ? 0 : Mathf.Clamp(left.y + UiInput[0], -1, 1),
                    neutralRequired ? 0 : Mathf.Clamp(left.x + UiInput[1], -1, 1),
                    neutralRequired ? 0 : Mathf.Clamp(right.y + UiInput[2], -1, 1),
                    neutralRequired ? 0 : Mathf.Clamp(extension + UiInput[3], -1, 1),
                    neutralRequired ? 0 : Mathf.Clamp(chute + UiInput[4], -1, 1));
            }
        }
        private void OnDisable() { SuspendInput(); }
    }
}
