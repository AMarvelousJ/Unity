using System;
using UnityEngine;

namespace ZCJ.Shiploader
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class HoldCargoVisualController : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private string vesselId;
        [SerializeField] private string holdId;
        [SerializeField] private Transform coalSurface;
        [SerializeField] private Renderer coalRenderer;
        [SerializeField] private Renderer[] coamingRenderers = Array.Empty<Renderer>();
        [SerializeField, Min(0.05f)] private float fullHeight = 1.35f;
        [SerializeField] private float coalBottomY = 0.025f;
        [SerializeField, Range(0f, 1f)] private float fillRatio;
        [SerializeField] private bool active;

        private MaterialPropertyBlock highlightBlock;

        public string VesselId => vesselId;
        public string HoldId => holdId;
        public float FillRatio => fillRatio;
        public bool IsActive => active;

        public void Configure(
            string vessel,
            string hold,
            Transform surface,
            Renderer[] coamings,
            float maximumHeight,
            float initialFill)
        {
            vesselId = vessel;
            holdId = hold;
            coalSurface = surface;
            coalRenderer = surface != null ? surface.GetComponent<Renderer>() : null;
            coamingRenderers = coamings ?? Array.Empty<Renderer>();
            fullHeight = Mathf.Max(0.05f, maximumHeight);
            coalBottomY = surface != null
                ? surface.localPosition.y - surface.localScale.y * 0.5f
                : 0.025f;
            SetFillRatio(initialFill);
            SetActive(false);
        }

        public void SetFillRatio(float ratio)
        {
            fillRatio = Mathf.Clamp01(ratio);
            if (coalSurface == null)
            {
                return;
            }

            float height = Mathf.Max(0.01f, fullHeight * fillRatio);
            Vector3 scale = coalSurface.localScale;
            scale.y = height;
            coalSurface.localScale = scale;
            Vector3 position = coalSurface.localPosition;
            position.y = coalBottomY + height * 0.5f;
            coalSurface.localPosition = position;
            if (coalRenderer != null)
            {
                coalRenderer.enabled = fillRatio > 0.001f;
            }
        }

        public void SetActive(bool value)
        {
            active = value;
            highlightBlock ??= new MaterialPropertyBlock();
            foreach (Renderer renderer in coamingRenderers)
            {
                if (renderer == null)
                {
                    continue;
                }
                if (!active)
                {
                    renderer.SetPropertyBlock(null);
                    continue;
                }
                highlightBlock.Clear();
                highlightBlock.SetColor(BaseColor, new Color(1f, 0.36f, 0.05f, 1f));
                highlightBlock.SetColor(EmissionColor, new Color(0.22f, 0.045f, 0f, 1f));
                renderer.SetPropertyBlock(highlightBlock);
            }
        }

        private void OnValidate()
        {
            SetFillRatio(fillRatio);
            SetActive(active);
        }
    }
}
