using System;
using UnityEngine;

namespace ZCJ.Shiploader
{
    [DisallowMultipleComponent]
    public sealed class ShiploaderEffectsController : MonoBehaviour
    {
        private static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");

        [SerializeField] private Renderer[] beltRenderers = Array.Empty<Renderer>();
        [SerializeField] private ParticleSystem coalFlow;
        [SerializeField] private bool beltRunning;
        [SerializeField] private bool coalFlowEnabled;
        [SerializeField] private float beltVisualSpeed = 1.8f;

        private MaterialPropertyBlock propertyBlock;
        private float beltOffset;

        public bool BeltRunning => beltRunning;
        public bool CoalFlowEnabled => coalFlowEnabled;

        public void Configure(Renderer[] renderers, ParticleSystem particles)
        {
            beltRenderers = renderers ?? Array.Empty<Renderer>();
            coalFlow = particles;
            SetBeltRunning(false);
            SetCoalFlow(false);
        }

        public void SetBeltRunning(bool running)
        {
            beltRunning = running;
        }

        public void SetCoalFlow(bool enabled)
        {
            coalFlowEnabled = enabled;
            ApplyCoalFlowState();
        }

        private void OnEnable()
        {
            propertyBlock ??= new MaterialPropertyBlock();
            ApplyCoalFlowState();
            ApplyBeltOffset();
        }

        private void Update()
        {
            if (!beltRunning)
            {
                return;
            }

            beltOffset = Mathf.Repeat(beltOffset + Time.deltaTime * beltVisualSpeed, 1f);
            ApplyBeltOffset();
        }

        private void ApplyBeltOffset()
        {
            propertyBlock ??= new MaterialPropertyBlock();
            foreach (Renderer beltRenderer in beltRenderers)
            {
                if (beltRenderer == null)
                {
                    continue;
                }

                beltRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetVector(BaseMapST, new Vector4(1f, 7f, 0f, beltOffset));
                beltRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void ApplyCoalFlowState()
        {
            if (coalFlow == null)
            {
                return;
            }

            if (coalFlowEnabled)
            {
                if (!coalFlow.isPlaying)
                {
                    coalFlow.Play(true);
                }
            }
            else
            {
                coalFlow.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}
