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

        public ShiploaderCameraPreset Preset => preset;

        public void Configure(Transform cameraTarget)
        {
            target = cameraTarget;
            controlledCamera = GetComponent<Camera>();
            SetPreset(ShiploaderCameraPreset.Perspective);
        }

        public void SetPreset(ShiploaderCameraPreset nextPreset)
        {
            preset = nextPreset;
            EnsureCamera();
            controlledCamera.orthographic = nextPreset != ShiploaderCameraPreset.Perspective;
            controlledCamera.orthographicSize = 38f;
            ApplyPresetImmediately();
        }

        private void Awake()
        {
            EnsureCamera();
        }

        private void LateUpdate()
        {
            EnsureCamera();
            if (target == null)
            {
                return;
            }

            if (preset == ShiploaderCameraPreset.Perspective)
            {
                Mouse mouse = Mouse.current;
                if (mouse != null && mouse.rightButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    yaw += delta.x * orbitSensitivity;
                    pitch = Mathf.Clamp(pitch - delta.y * orbitSensitivity, 5f, 82f);
                }

                if (mouse != null)
                {
                    float scroll = mouse.scroll.ReadValue().y;
                    distance = Mathf.Clamp(distance - scroll * zoomSensitivity, 22f, 150f);
                }

                ApplyPerspective();
            }
            else
            {
                ApplyPresetImmediately();
            }
        }

        private void ApplyPerspective()
        {
            Vector3 focus = target.position;
            Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = focus + orbit * (Vector3.back * distance);
            transform.rotation = Quaternion.LookRotation(focus - transform.position, Vector3.up);
        }

        private void ApplyPresetImmediately()
        {
            if (target == null)
            {
                return;
            }

            Vector3 focus = target.position;
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
