using UnityEngine;
using UnityEngine.Rendering;

namespace ZCJ.Shiploader
{
    [ExecuteAlways]
    public sealed class ShiploaderPreviewQuality : MonoBehaviour
    {
        public RenderPipelineAsset previewPipeline;
        private RenderPipelineAsset previous;
        private bool applied;
        private void OnEnable()
        {
            if(previewPipeline==null)return;
            previous=QualitySettings.renderPipeline;QualitySettings.renderPipeline=previewPipeline;applied=true;
        }
        private void OnDisable()
        {
            if(applied&&QualitySettings.renderPipeline==previewPipeline)QualitySettings.renderPipeline=previous;
            applied=false;
        }
    }
}
