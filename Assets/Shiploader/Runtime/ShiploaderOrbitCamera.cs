using UnityEngine;
using UnityEngine.InputSystem;

namespace ZCJ.Shiploader
{
    public enum ShiploaderCameraPreset
    {
        Perspective,
        Front,
        Side,
        Top,
        Work,
        Discharge,
        PortTop,
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class ShiploaderOrbitCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private ShiploaderCameraPreset preset = ShiploaderCameraPreset.Perspective;
        [SerializeField] private float distance = 78f;
        [SerializeField] private float yaw = 42f;
        [SerializeField] private float pitch = 24f;
        [SerializeField] private float orbitSensitivity = 0.18f;
        [SerializeField] private float zoomSensitivity = 0.03f;

        private Camera controlledCamera;
        private ShiploaderRigController workingRig;
        private Vector3 overviewFocus;
        private float overviewDistance, overviewYaw, overviewPitch;
        private Vector3 smoothedFocus;
        private bool following = true;
        private bool initialized;
        private Rect[] inputBlocks = System.Array.Empty<Rect>();

        public bool Following => following;
        public ShiploaderRigController WorkingRig => workingRig;
        public ShiploaderWalkthrough Walkthrough { get; private set; }
        public void BindWorkingRig(ShiploaderRigController rig) => workingRig = rig;
        public void SetInputBlocks(params Rect[] blocks) => inputBlocks = blocks;

        private void Start()
        {
            overviewFocus = target != null ? target.position : transform.position + transform.forward * distance;
            overviewDistance = distance;
            overviewYaw = yaw;
            overviewPitch = pitch;
            smoothedFocus = overviewFocus;
            initialized = true;
            if (workingRig != null)
            {
                SetPreset(ShiploaderCameraPreset.Work);
                smoothedFocus = DesiredFocus();
                Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
                transform.SetPositionAndRotation(smoothedFocus + orbit * (Vector3.back * distance), orbit);
            }
        }

        public ShiploaderCameraPreset Preset => preset;

        public void Configure(Transform cameraTarget)
        {
            target = cameraTarget;
            controlledCamera = GetComponent<Camera>();
            SetPreset(ShiploaderCameraPreset.Perspective);
        }

        public void SetPreset(ShiploaderCameraPreset nextPreset)
        {
            if (Walkthrough != null && Walkthrough.Active) Walkthrough.Exit(false);
            preset = nextPreset;
            EnsureCamera();
            controlledCamera.orthographic = nextPreset == ShiploaderCameraPreset.Front ||
                nextPreset == ShiploaderCameraPreset.Side || nextPreset == ShiploaderCameraPreset.Top ||
                nextPreset == ShiploaderCameraPreset.PortTop;
            controlledCamera.orthographicSize = nextPreset == ShiploaderCameraPreset.PortTop ? 310f : 38f;
            following = true;
            if (initialized)
            {
                if (nextPreset == ShiploaderCameraPreset.Work) { distance = 115f; yaw = -32f; pitch = 27f; }
                else if (nextPreset == ShiploaderCameraPreset.Discharge)
                {
                    distance = 45f; pitch = 30f;
                    // Observe from the vessel side instead of looking through the gantry.
                    yaw = workingRig != null && workingRig.DischargeMarker != null &&
                        workingRig.DischargeMarker.position.z >= workingRig.transform.position.z ? 145f : -35f;
                }
                else if (nextPreset == ShiploaderCameraPreset.Perspective)
                { distance = overviewDistance; yaw = overviewYaw; pitch = overviewPitch; }
            }
            if (!Application.isPlaying) ApplyPresetImmediately();
        }

        private void Awake()
        {
            EnsureCamera();
            Walkthrough = GetComponent<ShiploaderWalkthrough>();
            if (Walkthrough == null) Walkthrough = gameObject.AddComponent<ShiploaderWalkthrough>();
            Walkthrough.Configure(this);
        }

        private void LateUpdate()
        {
            if (Walkthrough != null && Walkthrough.Active) return;
            EnsureCamera();
            if (target == null)
            {
                return;
            }

            if (!controlledCamera.orthographic)
            {
                Mouse mouse = Mouse.current;
                bool blocked = false;
                if (mouse != null)
                {
                    Vector2 point = mouse.position.ReadValue();
                    point.y = Screen.height - point.y;
                    foreach (Rect rect in inputBlocks) blocked |= rect.Contains(point);
                }
                if (mouse != null && !blocked && mouse.rightButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    if (delta.sqrMagnitude > 0f) following = false;
                    yaw += delta.x * orbitSensitivity;
                    pitch = Mathf.Clamp(pitch - delta.y * orbitSensitivity, 5f, 82f);
                }

                if (mouse != null && !blocked)
                {
                    float scroll = mouse.scroll.ReadValue().y;
                    distance = Mathf.Clamp(distance - scroll * zoomSensitivity, 16f, 1800f);
                }

                if (following) smoothedFocus = Vector3.Lerp(smoothedFocus, DesiredFocus(),
                    1f - Mathf.Exp(-5f * Time.unscaledDeltaTime));
                ApplyPerspective();
            }
            else
            {
                ApplyPresetImmediately();
            }
        }

        private void ApplyPerspective()
        {
            Vector3 focus = initialized ? smoothedFocus : target.position;
            Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 desiredPosition = focus + orbit * (Vector3.back * distance);
            Quaternion desiredRotation = Quaternion.LookRotation(focus - desiredPosition, Vector3.up);
            float blend = Application.isPlaying ? 1f - Mathf.Exp(-7f * Time.unscaledDeltaTime) : 1f;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, blend);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, blend);
        }

        private Vector3 DesiredFocus()
        {
            if (workingRig == null || preset == ShiploaderCameraPreset.Perspective || preset == ShiploaderCameraPreset.PortTop) return overviewFocus;
            if (preset == ShiploaderCameraPreset.Discharge && workingRig.DischargeMarker != null)
                return workingRig.DischargeMarker.position + Vector3.up * 5f;
            // Follow translation only: slewing and luffing must not rotate or shake the camera.
            return workingRig.transform.position + Vector3.up * 16f;
        }

        private void ApplyPresetImmediately()
        {
            if (target == null)
            {
                return;
            }

            Vector3 focus = initialized && workingRig != null ? DesiredFocus() : target.position;
            switch (preset)
            {
                case ShiploaderCameraPreset.Front:
                    transform.position = focus + Vector3.right * 90f;
                    transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
                    break;
                case ShiploaderCameraPreset.Side:
                    transform.position = focus + Vector3.forward * 90f;
                    transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
                    break;
                case ShiploaderCameraPreset.Top:
                    transform.position = focus + Vector3.up * 100f;
                    transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.back);
                    break;
                case ShiploaderCameraPreset.PortTop:
                    transform.position = focus + Vector3.up * 1000f;
                    transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    break;
                default:
                    ApplyPerspective();
                    break;
            }
        }

        private void EnsureCamera()
        {
            if (controlledCamera == null)
            {
                controlledCamera = GetComponent<Camera>();
            }
        }
    }
}
