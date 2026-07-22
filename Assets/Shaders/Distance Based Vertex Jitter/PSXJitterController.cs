using UnityEngine;
using System.Collections.Generic;

[ExecuteInEditMode]
public class PSXJitterController : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The player/camera transform to measure distance from")]
    public Transform playerTransform;

    [Header("Distance Settings")]
    [Tooltip("Distance where jitter starts (minimal)")]
    public float nearDistance = 5f;

    [Tooltip("Distance where jitter is maximum")]
    public float farDistance = 50f;

    [Header("Jitter Settings")]
    [Tooltip("Base vertex snap resolution (higher = smoother near objects)")]
    [Range(10f, 500f)]
    public float snapResolution = 100f;

    [Tooltip("Intensity multiplier for distance-based jitter (higher = more jitter on far objects)")]
    [Range(0.1f, 50f)]
    public float jitterIntensity = 10f;

    [Header("Resolution Scaling")]
    [Tooltip("Distance at which resolution scaling begins (e.g., 100 units)")]
    public float resolutionScaleStart = 100f;

    [Tooltip("Maximum amount to reduce snap resolution beyond the start distance (e.g., 50 units)")]
    [Range(-200f, 200f)]
    public float resolutionScaleAmount = 50f;

    [Header("Material Control")]
    [Tooltip("Auto-find all materials with PSX shader")]
    public bool autoFindMaterials = true;

    [Tooltip("Manually assigned materials to control")]
    public Material[] manualMaterials;

    private List<Material> controlledMaterials = new List<Material>();
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

    private void Start()
    {
        if (playerTransform == null)
        {
            // Try to find main camera if no player set
            if (Camera.main != null)
            {
                playerTransform = Camera.main.transform;
            }
            else
            {
                Debug.LogWarning("PSXJitterController: No player transform assigned and no main camera found.");
            }
        }

        RefreshMaterialList();
    }

    private void Update()
    {
        if (playerTransform == null)
            return;

        UpdateShaderProperties();
    }

    public void RefreshMaterialList()
    {
        controlledMaterials.Clear();

        if (autoFindMaterials)
        {
            // Find all renderers in scene
            Renderer[] renderers = FindObjectsOfType<Renderer>();
            foreach (var renderer in renderers)
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat != null && mat.shader.name.Contains("PSXJitter") && !controlledMaterials.Contains(mat))
                    {
                        controlledMaterials.Add(mat);
                    }
                }
            }
        }

        // Add manual materials
        if (manualMaterials != null)
        {
            foreach (var mat in manualMaterials)
            {
                if (mat != null && !controlledMaterials.Contains(mat))
                {
                    controlledMaterials.Add(mat);
                }
            }
        }

        Debug.Log($"PSXJitterController: Controlling {controlledMaterials.Count} materials");
    }

    private void UpdateShaderProperties()
    {
        // Get camera transform (use playerTransform as fallback)
        Transform cameraTransform = Camera.main != null ? Camera.main.transform : playerTransform;

        Vector3 cameraPos = cameraTransform.position;
        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;
        Vector3 cameraUp = cameraTransform.up;

        foreach (var mat in controlledMaterials)
        {
            if (mat == null)
                continue;

            mat.SetVector(CameraPositionID, cameraPos);
            mat.SetVector(CameraForwardID, cameraForward);
            mat.SetVector(CameraRightID, cameraRight);
            mat.SetVector(CameraUpID, cameraUp);
            mat.SetFloat(NearDistanceID, nearDistance);
            mat.SetFloat(FarDistanceID, farDistance);
            mat.SetFloat(SnapResolutionID, snapResolution);
            mat.SetFloat(JitterIntensityID, jitterIntensity);
            mat.SetFloat(ResolutionScaleStartID, resolutionScaleStart);
            mat.SetFloat(ResolutionScaleAmountID, resolutionScaleAmount);
        }
    }

    private void OnValidate()
    {
        // Clamp values
        nearDistance = Mathf.Max(0f, nearDistance);
        farDistance = Mathf.Max(nearDistance + 0.1f, farDistance);
        resolutionScaleStart = Mathf.Max(0f, resolutionScaleStart);
        // resolutionScaleAmount = Mathf.Max(0f, resolutionScaleAmount);
    }

#if UNITY_EDITOR
    [ContextMenu("Refresh Material List")]
    private void RefreshMaterialListMenu()
    {
        RefreshMaterialList();
    }
#endif
}