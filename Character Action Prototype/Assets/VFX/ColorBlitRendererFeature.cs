using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

internal class ColorBlitRendererFeature : ScriptableRendererFeature
{
    public Shader m_Shader;  // Reference to the custom shader
    private Material m_Material;  // Material created from the shader
    private ColorBlitPass m_RenderPass = null;  // Custom render pass

    public override void Create()
    {
        // Create the material using the provided shader
        m_Material = CoreUtils.CreateEngineMaterial(m_Shader);
        m_RenderPass = new ColorBlitPass(m_Material);

        // Specify when the pass should be executed
        m_RenderPass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // Only enqueue the pass for Game cameras
        if (renderingData.cameraData.cameraType == CameraType.Game)
        {
            renderer.EnqueuePass(m_RenderPass);
        }
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType == CameraType.Game)
        {
            // Ensure the opaque texture is available to the Render Pass
            m_RenderPass.ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Motion);

            var motionVectorHandle = renderingData.cameraData.renderer.cameraDepthTargetHandle;

            // Set the camera color target for the render pass
            m_RenderPass.SetTarget(renderer.cameraColorTargetHandle, motionVectorHandle);
        }
    }

    protected override void Dispose(bool disposing)
    {
        // Clean up the material when the feature is destroyed
        CoreUtils.Destroy(m_Material);
    }
}
