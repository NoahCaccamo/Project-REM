using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class MotionVectorCapturePass : ScriptableRenderPass
{
    private const string k_ProfilerTag = "Motion Vector Capture";
    private ProfilingSampler m_ProfilingSampler = new ProfilingSampler(k_ProfilerTag);
    private static readonly int s_GlobalMotionVectorTextureID = Shader.PropertyToID("_GlobalMotionVectorTexture");
    private static readonly int s_MotionVectorTextureID = Shader.PropertyToID("_MotionVectorTexture");

    public MotionVectorCapturePass()
    {
        // Execute after motion vectors have been rendered
        renderPassEvent = RenderPassEvent.AfterRenderingOpaques;

        // This tells URP we need motion vector input
        ConfigureInput(ScriptableRenderPassInput.Motion);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.camera.cameraType != CameraType.Game)
            return;

        CommandBuffer cmd = CommandBufferPool.Get(k_ProfilerTag);
        using (new ProfilingScope(cmd, m_ProfilingSampler))
        {
            // URP internally binds motion vectors to _MotionVectorTexture
            // We just create an alias with our custom name
            cmd.SetGlobalTexture(s_GlobalMotionVectorTextureID, s_MotionVectorTextureID);
        }

        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        CommandBufferPool.Release(cmd);
    }
}