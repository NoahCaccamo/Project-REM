using UnityEngine;
using UnityEngine.Rendering.Universal;

public class MotionVectorCaptureRendererFeature : ScriptableRendererFeature
{
    private MotionVectorCapturePass m_RenderPass = null;

    public override void Create()
    {
        m_RenderPass = new MotionVectorCapturePass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType == CameraType.Game)
        {
            renderer.EnqueuePass(m_RenderPass);
        }
    }
}