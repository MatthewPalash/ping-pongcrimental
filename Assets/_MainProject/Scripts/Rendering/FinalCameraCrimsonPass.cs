using UnityEngine.Rendering.Universal;

namespace TennisPrototype.Rendering
{
    // A camera stack renders the table and paddle separately. Grade the composed frame once.
    public sealed class FinalCameraCrimsonPass : FullScreenPassRendererFeature
    {
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.resolveFinalTarget)
                base.AddRenderPasses(renderer, ref renderingData);
        }
    }
}
