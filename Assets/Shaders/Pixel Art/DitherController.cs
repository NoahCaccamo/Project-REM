using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DitherController : MonoBehaviour
{
    [SerializeField] private UniversalRendererData rendererData;
    private DitherRendererFeature ditherFeature;

    void Start()
    {
        // Find the DitherRendererFeature in the renderer
        if (rendererData != null)
        {
            foreach (var feature in rendererData.rendererFeatures)
            {
                if (feature is DitherRendererFeature dither)
                {
                    ditherFeature = dither;
                    break;
                }
            }
        }

        if (ditherFeature == null)
        {
            Debug.LogError("DitherRendererFeature not found!");
        }
    }

    // Public methods to modify settings at runtime
    public void SetSpread(float value)
    {
        if (ditherFeature != null)
        {
            ditherFeature.settings.spread = Mathf.Clamp01(value);
        }
    }

    public void SetColorCount(int red, int green, int blue)
    {
        if (ditherFeature != null)
        {
            ditherFeature.settings.redColorCount = Mathf.Clamp(red, 2, 16);
            ditherFeature.settings.greenColorCount = Mathf.Clamp(green, 2, 16);
            ditherFeature.settings.blueColorCount = Mathf.Clamp(blue, 2, 16);
        }
    }

    public void SetBayerLevel(int level)
    {
        if (ditherFeature != null)
        {
            ditherFeature.settings.bayerLevel = Mathf.Clamp(level, 0, 2);
        }
    }

    public void SetDownSamples(int samples)
    {
        if (ditherFeature != null)
        {
            ditherFeature.settings.downSamples = Mathf.Clamp(samples, 0, 8);
        }
    }

    public void SetPointFilterDown(bool enabled)
    {
        if (ditherFeature != null)
        {
            ditherFeature.settings.pointFilterDown = enabled;
        }
    }

    // Example: Animate dither effect
    void Update()
    {
        // Example: Pulse the spread value
        // SetSpread(0.5f + Mathf.Sin(Time.time) * 0.3f);
    }
}