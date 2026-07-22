using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PSXJitterRenderFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;

        [Header("Jitter Settings")]
        public Transform playerTransform;
        public float nearDistance = 5f;
        public float farDistance = 50f;
        [Range(10f, 500f)]
        public float snapResolution = 100f;
        [Range(0f, 50f)]
        public float jitterIntensity = 10f;

        [Header("Resolution Scaling")]
        [Tooltip("Distance at which resolution scaling begins (e.g., 100 units)")]
        public float resolutionScaleStart = 100f;
        [Tooltip("Maximum amount to reduce snap resolution beyond the start distance")]
        [Range(0f, 200f)]
        public float resolutionScaleAmount = 50f;
    }

    public Settings settings = new Settings();
    private PSXJitterPass pass;

    public override void Create()
    {
        pass = new PSXJitterPass(settings);
        pass.renderPassEvent = settings.renderPassEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(pass);
    }

    class PSXJitterPass : ScriptableRenderPass
    {
        private Settings settings;
        private static readonly int CameraPositionID = Shader.PropertyToID("_CameraPosition");
        private static readonly int CameraForwardID = Shader.PropertyToID("_CameraForward");
        private static readonly int CameraRightID = Shader.PropertyToID("_CameraRight");
        private static readonly int CameraUpID = Shader.PropertyToID("_CameraUp");
        private static readonly int NearDistanceID = Shader.PropertyToID("_NearDistance");
        private static readonly int FarDistanceID = Shader.PropertyToID("_FarDistance");
        private static readonly int SnapResolutionID = Shader.PropertyToID("_SnapResolution");
        private static readonly int JitterIntensityID = Shader.PropertyToID("_JitterIntensity");
        private static readonly int ResolutionScaleStartID = Shader.PropertyToID("_ResolutionScaleStart");
        private static readonly int ResolutionScaleAmountID = Shader.PropertyToID("_ResolutionScaleAmount");

        public PSXJitterPass(Settings settings)
        {
            this.settings = settings;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (settings.playerTransform == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get("PSX Jitter");

            // Get camera orientation
            Camera camera = renderingData.cameraData.camera;
            Transform cameraTransform = camera.transform;

            Vector3 cameraPos = cameraTransform.position;
            Vector3 cameraForward = cameraTransform.forward;
            Vector3 cameraRight = cameraTransform.right;
            Vector3 cameraUp = cameraTransform.up;

            cmd.SetGlobalVector(CameraPositionID, cameraPos);
            cmd.SetGlobalVector(CameraForwardID, cameraForward);
            cmd.SetGlobalVector(CameraRightID, cameraRight);
            cmd.SetGlobalVector(CameraUpID, cameraUp);
            cmd.SetGlobalFloat(NearDistanceID, settings.nearDistance);
            cmd.SetGlobalFloat(FarDistanceID, settings.farDistance);
            cmd.SetGlobalFloat(SnapResolutionID, settings.snapResolution);
            cmd.SetGlobalFloat(JitterIntensityID, settings.jitterIntensity);
            cmd.SetGlobalFloat(ResolutionScaleStartID, settings.resolutionScaleStart);
            cmd.SetGlobalFloat(ResolutionScaleAmountID, settings.resolutionScaleAmount);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}