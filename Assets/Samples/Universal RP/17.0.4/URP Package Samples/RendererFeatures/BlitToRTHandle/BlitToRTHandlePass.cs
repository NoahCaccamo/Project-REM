using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

// This pass creates an RTHandle and blits the camera color to it.
// When a frame is captured, it freezes that frame and makes it available as _CopyColorTexture
public class BlitToRTHandlePass : ScriptableRenderPass
{
    private class PassData
    {
    }

    private ProfilingSampler m_ProfilingSampler = new ProfilingSampler("BlitToRTHandle_CopyColor");
    private RTHandle m_InputHandle;
    private RTHandle m_OutputHandle;
    private const string k_OutputName = "_CopyColorTexture";
    private static readonly int m_OutputId = Shader.PropertyToID(k_OutputName);
    private Material m_Material;

    // Frame capture control
    private bool m_CaptureRequested = false;
    private bool m_FrameCaptured = false;

    public BlitToRTHandlePass(RenderPassEvent evt, Material mat)
    {
        renderPassEvent = evt;
        m_Material = mat;
    }

    // Call this method to request a frame capture
    public void RequestFrameCapture()
    {
        m_CaptureRequested = true;
        m_FrameCaptured = false;
    }

    // Call this method to reset and allow continuous capturing again
    public void ResetCapture()
    {
        m_CaptureRequested = false;
        m_FrameCaptured = false;
    }

    // Check if a frame has been captured
    public bool IsFrameCaptured()
    {
        return m_FrameCaptured;
    }

#pragma warning disable 618, 672 // Type or member is obsolete, Member overrides obsolete member

    // Unity calls the Configure method in the Compatibility mode (non-RenderGraph path)
    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        // Configure the custom RTHandle
        var desc = cameraTextureDescriptor;
        desc.depthBufferBits = 0;
        desc.msaaSamples = 1;
        RenderingUtils.ReAllocateIfNeeded(ref m_OutputHandle, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: k_OutputName);

        // Set the RTHandle as the output target in the Compatibility mode
        ConfigureTarget(m_OutputHandle);
    }

    // Unity calls the Execute method in the Compatibility mode
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        // Set camera color as the input
        m_InputHandle = renderingData.cameraData.renderer.cameraColorTargetHandle;

        CommandBuffer cmd = CommandBufferPool.Get();

        // Perform the Blit operation
        using (new ProfilingScope(cmd, m_ProfilingSampler))
        {
            // If not capturing, continuously update the texture
            if (!m_CaptureRequested)
            {
                // Normal passthrough - continuously update
                Blitter.BlitCameraTexture(cmd, m_InputHandle, m_OutputHandle, m_Material, 0);
            }
            else if (!m_FrameCaptured)
            {
                // Capture the frame - freeze it
                Blitter.BlitCameraTexture(cmd, m_InputHandle, m_OutputHandle, m_Material, 0);
                m_FrameCaptured = true;
            }
            // else: Frame is captured and frozen - don't update m_OutputHandle

            // Make the output texture available for shaders (including your ShaderGraph)
            cmd.SetGlobalTexture(m_OutputId, m_OutputHandle.nameID);
        }

        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        CommandBufferPool.Release(cmd);
    }

#pragma warning restore 618, 672

    // Unity calls the RecordRenderGraph method to add and configure one or more render passes in the render graph system.
    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

        if (cameraData.camera.cameraType != CameraType.Game)
            return;

        // Only execute if capture is requested and hasn't been captured yet, or if not capturing at all
        if (m_CaptureRequested && m_FrameCaptured)
            return; // Frame is frozen, don't update

        // Create the custom RTHandle
        var desc = cameraData.cameraTargetDescriptor;
        desc.depthBufferBits = 0;
        desc.msaaSamples = 1;
        RenderingUtils.ReAllocateHandleIfNeeded(ref m_OutputHandle, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: k_OutputName);

        // Make the output texture available for shaders
        Shader.SetGlobalTexture(m_OutputId, m_OutputHandle);

        // Set camera color as a texture resource for this render graph instance
        TextureHandle source = resourceData.activeColorTexture;

        // Set RTHandle as a texture resource for this render graph instance
        TextureHandle destination = renderGraph.ImportTexture(m_OutputHandle);

        if (!source.IsValid() || !destination.IsValid())
            return;

        // Capture or update the frame
        if (!m_CaptureRequested || !m_FrameCaptured)
        {
            // Blit the input texture to the destination texture
            RenderGraphUtils.BlitMaterialParameters para = new(source, destination, m_Material, 0);
            renderGraph.AddBlitPass(para, "BlitToRTHandle_CopyColor");

            if (m_CaptureRequested && !m_FrameCaptured)
            {
                m_FrameCaptured = true;
            }
        }

        // Set as camera color target
        resourceData.cameraColor = destination;
    }

    public void Dispose()
    {
        m_InputHandle?.Release();
        m_OutputHandle?.Release();
    }
}