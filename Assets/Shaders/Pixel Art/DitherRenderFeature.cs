using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DitherRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class DitherSettings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        public Material ditherMaterial;

        [Range(0.0f, 1.0f)]
        public float spread = 0.8f; // 0.5

        [Range(2, 16)]
        public int redColorCount = 2;
        [Range(2, 16)]
        public int greenColorCount = 2;
        [Range(2, 16)]
        public int blueColorCount = 2;

        [Range(0, 2)]
        public int bayerLevel = 1;

        [Range(0, 8)]
        public int downSamples = 2;

        public bool pointFilterDown = false;
    }

    public DitherSettings settings = new DitherSettings();
    private DitherRenderPass ditherPass;

    public override void Create()
    {
        ditherPass = new DitherRenderPass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.ditherMaterial == null)
        {
            Debug.LogWarning("Dither material is missing!");
            return;
        }

        ditherPass.ConfigureInput(ScriptableRenderPassInput.Color);
        renderer.EnqueuePass(ditherPass);
    }

    protected override void Dispose(bool disposing)
    {
        ditherPass?.Dispose();
    }

    class DitherRenderPass : ScriptableRenderPass
    {
        private DitherSettings settings;
        private RTHandle tempTexture;
        private RTHandle[] downSampleTextures;
        private const int MaxDownSamples = 8;

        public DitherRenderPass(DitherSettings settings)
        {
            this.settings = settings;
            this.renderPassEvent = settings.renderPassEvent;
            downSampleTextures = new RTHandle[MaxDownSamples];
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;

            int width = descriptor.width;
            int height = descriptor.height;

            // Pre-allocate downsampling textures
            for (int i = 0; i < settings.downSamples && i < MaxDownSamples; i++)
            {
                width /= 2;
                height /= 2;

                if (height < 2)
                    break;

                RenderTextureDescriptor downDescriptor = descriptor;
                downDescriptor.width = width;
                downDescriptor.height = height;

                FilterMode filterMode = settings.pointFilterDown ? FilterMode.Point : FilterMode.Bilinear;
                RenderingUtils.ReAllocateIfNeeded(ref downSampleTextures[i], downDescriptor, filterMode, TextureWrapMode.Clamp, name: $"_DownSample{i}");
            }

            // Allocate temp texture
            RenderTextureDescriptor tempDescriptor = descriptor;
            tempDescriptor.width = width;
            tempDescriptor.height = height;
            RenderingUtils.ReAllocateIfNeeded(ref tempTexture, tempDescriptor, FilterMode.Point, TextureWrapMode.Clamp, name: "_TempDitherTexture");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (settings.ditherMaterial == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get("Dither Effect");

            settings.ditherMaterial.SetFloat("_Spread", settings.spread);
            settings.ditherMaterial.SetInt("_RedColorCount", settings.redColorCount);
            settings.ditherMaterial.SetInt("_GreenColorCount", settings.greenColorCount);
            settings.ditherMaterial.SetInt("_BlueColorCount", settings.blueColorCount);
            settings.ditherMaterial.SetInt("_BayerLevel", settings.bayerLevel);

            RTHandle cameraColorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;
            RTHandle currentSource = cameraColorTarget;

            // Downsampling
            for (int i = 0; i < settings.downSamples && i < MaxDownSamples; i++)
            {
                if (downSampleTextures[i] == null)
                    break;

                if (settings.pointFilterDown)
                    Blitter.BlitCameraTexture(cmd, currentSource, downSampleTextures[i], settings.ditherMaterial, 1);
                else
                    Blitter.BlitCameraTexture(cmd, currentSource, downSampleTextures[i]);

                currentSource = downSampleTextures[i];
            }

            // Apply dither
            Blitter.BlitCameraTexture(cmd, currentSource, tempTexture, settings.ditherMaterial, 0);

            // Blit to camera
            Blitter.BlitCameraTexture(cmd, tempTexture, cameraColorTarget, settings.ditherMaterial, 1);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            tempTexture?.Release();
            for (int i = 0; i < MaxDownSamples; i++)
            {
                downSampleTextures[i]?.Release();
            }
        }
    }
}