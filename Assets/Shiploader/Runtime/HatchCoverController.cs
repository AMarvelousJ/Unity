using System;
using UnityEngine;

namespace ZCJ.Shiploader
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class HatchCoverController : MonoBehaviour
    {
        [SerializeField] private string vesselId;
        [SerializeField] private string holdId;
        [SerializeField] private bool isOpen = true;
        [SerializeField, Min(0.1f)] private float animationSpeed = 2.5f;
        [SerializeField] private Transform[] panels = Array.Empty<Transform>();
        [SerializeField] private Vector3[] closedPositions = Array.Empty<Vector3>();
        [SerializeField] private Vector3[] closedEulerAngles = Array.Empty<Vector3>();
        [SerializeField] private Vector3[] openPositions = Array.Empty<Vector3>();
        [SerializeField] private Vector3[] openEulerAngles = Array.Empty<Vector3>();

        public string VesselId => vesselId;
        public string HoldId => holdId;
        public bool IsOpen => isOpen;
        public string DisplayName => $"{vesselId} / {holdId}";

        public void Configure(
            string vessel,
            string hold,
            Transform[] coverPanels,
            Vector3[] closedPos,
            Vector3[] closedEuler,
            Vector3[] openPos,
            Vector3[] openEuler,
            bool initiallyOpen)
        {
            vesselId = vessel;
            holdId = hold;
            panels = coverPanels;
            closedPositions = closedPos;
            closedEulerAngles = closedEuler;
            openPositions = openPos;
            openEulerAngles = openEuler;
            isOpen = initiallyOpen;
            ApplyState(true);
        }

        public void SetOpen(bool open, bool immediate = false)
        {
            isOpen = open;
            if (immediate || !Application.isPlaying)
            {
                ApplyState(true);
            }
        }

        public void Toggle() => SetOpen(!isOpen);

        private void OnEnable() => ApplyState(true);
        private void OnValidate() => ApplyState(!Application.isPlaying);

        private void Update()
        {
            ApplyState(!Application.isPlaying);
        }

        private void ApplyState(bool immediate)
        {
            int count = Mathf.Min(
                panels?.Length ?? 0,
                Mathf.Min(
                    Mathf.Min(closedPositions?.Length ?? 0, closedEulerAngles?.Length ?? 0),
                    Mathf.Min(openPositions?.Length ?? 0, openEulerAngles?.Length ?? 0)));

            float step = immediate ? float.PositiveInfinity : animationSpeed * Time.deltaTime;
            for (int index = 0; index < count; index++)
            {
                Transform panel = panels[index];
                if (panel == null)
                {
                    continue;
                }

                Vector3 targetPosition = isOpen ? openPositions[index] : closedPositions[index];
                Quaternion targetRotation = Quaternion.Euler(isOpen ? openEulerAngles[index] : closedEulerAngles[index]);
                panel.localPosition = immediate
                    ? targetPosition
                    : Vector3.MoveTowards(panel.localPosition, targetPosition, step);
                panel.localRotation = immediate
                    ? targetRotation
                    : Quaternion.RotateTowards(panel.localRotation, targetRotation, step * 45f);
            }
        }
    }
}
