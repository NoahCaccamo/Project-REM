using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DatamoshController : MonoBehaviour
{
    [Header("Renderer Features")]
    [Tooltip("Reference to the BlitToRTHandle renderer feature (auto-found if null)")]
    public BlitToRTHandleRendererFeature blitFeature;

    [Header("Effect Controls")]
    [Tooltip("Key to trigger frame capture and start datamoshing")]
    public KeyCode captureKey = KeyCode.G;

    [Tooltip("Key to reset the effect")]
    public KeyCode resetKey = KeyCode.R;

    [Header("Datamosh Parameters")]
    [Tooltip("Motion strength - how much motion vectors distort the image (for your ShaderGraph)")]
    [Range(0f, 10f)]
    public float motionStrength = 1.0f;

    [Tooltip("Effect opacity - controls visibility of datamosh effect (for your ShaderGraph)")]
    [Range(0f, 1f)]
    public float effectOpacity = 1.0f;

    [Header("Pixel Decay Settings")]
    [Tooltip("Enable/disable pixel decay effect")]
    public bool enablePixelDecay = false;

    [Tooltip("Starting threshold value when decay begins")]
    [Range(0f, 3f)]
    public float startThreshold = 2.0f;

    [Tooltip("Ending threshold value after decay completes")]
    [Range(0f, 3f)]
    public float endThreshold = 0.1f;

    [Tooltip("Time in seconds to lerp from start to end threshold (0 = instant)")]
    [Range(0f, 30f)]
    public float decayDuration = 5.0f;

    [Tooltip("Animation curve for threshold lerp (default is linear)")]
    public AnimationCurve decayCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Auto Capture")]
    [Tooltip("Automatically capture frame on start")]
    public bool captureOnStart = false;

    [Tooltip("Delay before auto capture (seconds)")]
    public float captureDelay = 0f;

    [Header("Debug")]
    [Tooltip("Show debug information")]
    public bool debugMode = false;

    // Shader property IDs for your ShaderGraph to read
    private static readonly int MotionStrengthId = Shader.PropertyToID("_MotionStrength");
    private static readonly int EffectOpacityId = Shader.PropertyToID("_EffectOpacity");
    private static readonly int ColorDiffThresholdId = Shader.PropertyToID("_ColorDifferenceThreshold");
    private static readonly int EnablePixelDecayId = Shader.PropertyToID("_EnablePixelDecay");

    private bool isEffectActive = false;
    private float captureTimer = 0f;
    private bool pendingCapture = false;

    // Decay animation state
    private bool isDecayAnimating = false;
    private float decayStartTime = 0f;
    private float currentThreshold = 2f;

    private void Awake()
    {
        // Auto-find the renderer feature if not assigned
        if (blitFeature == null)
        {
            blitFeature = FindBlitFeature();
        }

        currentThreshold = startThreshold;
    }

    private void Start()
    {
        if (blitFeature == null)
        {
            Debug.LogError("DatamoshController: BlitToRTHandle renderer feature reference is missing!");
            enabled = false;
            return;
        }

        if (debugMode)
        {
            Debug.Log($"DatamoshController: BlitToRTHandle feature found: {blitFeature.name}");
        }

        if (captureOnStart)
        {
            if (captureDelay > 0f)
            {
                pendingCapture = true;
                captureTimer = captureDelay;
            }
            else
            {
                CaptureFrame();
            }
        }

        UpdateShaderProperties();

        if (debugMode)
        {
            Debug.Log($"DatamoshController: Started - MotionStrength: {motionStrength}, Opacity: {effectOpacity}");
        }
    }

    private void Update()
    {
        // Handle auto capture with delay
        if (pendingCapture)
        {
            captureTimer -= Time.deltaTime;
            if (captureTimer <= 0f)
            {
                CaptureFrame();
                pendingCapture = false;
            }
        }

        // Handle manual capture input
        if (Input.GetKeyDown(captureKey))
        {
            ResetEffect();


            CaptureFrame();
            SetEffectOpacity(0.02f); // was 0.03
            enablePixelDecay = enabled;
            // reset threshold too?
            StartDecayAnimation();
        }

        // Handle reset input
        if (Input.GetKeyDown(resetKey))
        {
            ResetEffect();
        }

        // Update decay animation
        if (isDecayAnimating)
        {
            UpdateDecayAnimation();
        }

        // Update shader properties every frame so your ShaderGraph can read them
        UpdateShaderProperties();
    }

    /// <summary>
    /// Updates the decay threshold animation over time
    /// </summary>
    private void UpdateDecayAnimation()
    {
        if (decayDuration <= 0f)
        {
            // Instant transition
            currentThreshold = endThreshold;
            isDecayAnimating = false;
            return;
        }

        float elapsed = Time.time - decayStartTime;
        float normalizedTime = Mathf.Clamp01(elapsed / decayDuration);

        // Apply animation curve
        float curveValue = decayCurve.Evaluate(normalizedTime);
        currentThreshold = Mathf.Lerp(startThreshold, endThreshold, curveValue);

        // Check if animation is complete
        if (normalizedTime >= 1f)
        {
            // Stop animating
            isDecayAnimating = false;
            SetEffectOpacity(1.0f);
            if (debugMode)
            {
                Debug.Log($"Decay animation cycle complete - Current threshold: {currentThreshold}");
            }
        }
    }

    /// <summary>
    /// Finds the BlitToRTHandleRendererFeature in the current URP Renderer
    /// </summary>
    private BlitToRTHandleRendererFeature FindBlitFeature()
    {
        var pipelineAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipelineAsset == null)
        {
            Debug.LogError("DatamoshController: No URP asset found!");
            return null;
        }

        var rendererDataList = typeof(UniversalRenderPipelineAsset)
            .GetField("m_RendererDataList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(pipelineAsset) as ScriptableRendererData[];

        if (rendererDataList == null || rendererDataList.Length == 0)
        {
            Debug.LogError("DatamoshController: No renderer data found in URP asset!");
            return null;
        }

        foreach (var rendererData in rendererDataList)
        {
            if (rendererData == null) continue;

            var features = rendererData.rendererFeatures;
            foreach (var feature in features)
            {
                if (feature is BlitToRTHandleRendererFeature blitFeature)
                {
                    if (debugMode)
                    {
                        Debug.Log($"DatamoshController: Found BlitToRTHandleRendererFeature in renderer: {rendererData.name}");
                    }
                    return blitFeature;
                }
            }
        }

        Debug.LogError("DatamoshController: BlitToRTHandleRendererFeature not found!");
        return null;
    }

    /// <summary>
    /// Captures the current frame and starts the datamoshing effect
    /// </summary>
    public void CaptureFrame()
    {
        if (blitFeature == null || blitFeature.blitPass == null)
        {
            Debug.LogWarning("DatamoshController: Cannot capture frame - blit pass not available");
            return;
        }

        blitFeature.blitPass.RequestFrameCapture();
        isEffectActive = true;

        Debug.Log($"Datamosh frame captured - MotionStrength: {motionStrength}");
    }

    /// <summary>
    /// Resets the datamoshing effect
    /// </summary>
    public void ResetEffect()
    {
        if (blitFeature == null || blitFeature.blitPass == null)
        {
            Debug.LogWarning("DatamoshController: Cannot reset - blit pass not available");
            return;
        }

        blitFeature.blitPass.ResetCapture();
        isEffectActive = false;
        isDecayAnimating = false;
        currentThreshold = startThreshold;
        SetEffectOpacity(1.0f);

        Debug.Log("Datamosh effect reset");
    }

    /// <summary>
    /// Sets the motion strength value (for your ShaderGraph to read)
    /// </summary>
    public void SetMotionStrength(float value)
    {
        motionStrength = Mathf.Clamp(value, 0f, 10f);
        UpdateShaderProperties();

        if (debugMode)
        {
            Debug.Log($"MotionStrength set to: {motionStrength}");
        }
    }

    /// <summary>
    /// Manually start the decay animation
    /// </summary>
    public void StartDecayAnimation()
    {
        isDecayAnimating = true;
        decayStartTime = Time.time;
        currentThreshold = startThreshold;

        if (debugMode)
        {
            Debug.Log($"Decay animation started - Duration: {decayDuration}s, From: {startThreshold} To: {endThreshold}");
        }
    }

    /// <summary>
    /// Stop the decay animation at current threshold
    /// </summary>
    public void StopDecayAnimation()
    {
        isDecayAnimating = false;

        if (debugMode)
        {
            Debug.Log($"Decay animation stopped at threshold: {currentThreshold}");
        }
    }

    /// <summary>
    /// Sets the effect opacity value (for your ShaderGraph to read)
    /// </summary>
    public void SetEffectOpacity(float value)
    {
        effectOpacity = Mathf.Clamp01(value);
        UpdateShaderProperties();

        if (debugMode)
        {
            Debug.Log($"EffectOpacity set to: {effectOpacity}");
        }
    }

    /// <summary>
    /// Updates the global shader properties that your ShaderGraph reads
    /// </summary>
    private void UpdateShaderProperties()
    {
        // Set global properties that your ShaderGraph can access
        Shader.SetGlobalFloat(MotionStrengthId, motionStrength);
        Shader.SetGlobalFloat(EffectOpacityId, effectOpacity);
        Shader.SetGlobalFloat(ColorDiffThresholdId, currentThreshold);
        Shader.SetGlobalFloat(EnablePixelDecayId, enablePixelDecay ? 1f : 0f);
    }


    /// <summary>
    /// Checks if the effect is currently active
    /// </summary>
    public bool IsEffectActive()
    {
        return isEffectActive && blitFeature != null && blitFeature.blitPass != null && blitFeature.blitPass.IsFrameCaptured();
    }

    private void OnValidate()
    {
        // Clamp values in the inspector
        motionStrength = Mathf.Clamp(motionStrength, 0f, 10f);
        effectOpacity = Mathf.Clamp01(effectOpacity);
        captureDelay = Mathf.Max(0f, captureDelay);
        startThreshold = Mathf.Clamp(startThreshold, 0f, 3f);
        endThreshold = Mathf.Clamp(endThreshold, 0f, 3f);
        decayDuration = Mathf.Max(0f, decayDuration);

        // Update shader properties when values change in inspector
        if (Application.isPlaying)
        {
            UpdateShaderProperties();
        }
    }
}