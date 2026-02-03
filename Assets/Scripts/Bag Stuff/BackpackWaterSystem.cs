using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages water physics in the 2D backpack, including filling, draining, and buoyancy forces.
/// </summary>
public class BackpackWaterSystem : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The backpack surface component")]
    public BackpackSurface backpackSurface;

    [Header("Water Settings")]
    [Tooltip("Is water contact currently active?")]
    public bool waterContactEnabled = false;

    [Tooltip("Current water level (0 = empty, 1 = full)")]
    [Range(0f, 1f)]
    public float waterLevel = 0f;

    [Tooltip("Rate at which water fills when contact is enabled (per second)")]
    public float fillRate = 0.2f;

    [Tooltip("Rate at which water drains when contact is disabled (per second)")]
    public float drainRate = 0.05f;

    [Tooltip("Maximum water level (0 = bottom, 1 = top)")]
    [Range(0f, 1f)]
    public float maxWaterLevel = 0.8f;

    [Header("Physics Settings")]
    [Tooltip("Buoyancy force applied to objects below water surface")]
    public float buoyancyForce = 5f;

    [Tooltip("Drag applied to objects in water")]
    public float waterDrag = 3f;

    [Tooltip("Angular drag applied to objects in water")]
    public float waterAngularDrag = 1f;

    [Header("Visual Settings")]
    [Tooltip("Visual representation of water in physics world")]
    public Transform waterVisualPhysics;

    [Tooltip("Visual representation of water on visual plane (created automatically)")]
    public Transform waterVisualPlane;

    [Tooltip("Color of water")]
    public Color waterColor = new Color(0.2f, 0.5f, 0.8f, 0.5f);

    private MeshCollider backpackBounds;
    private Vector2 boundsMin;
    private Vector2 boundsMax;
    private float backpackHeight;
    private float backpackWidth;

    // Track original physics properties for restoration
    private Dictionary<Rigidbody2D, PhysicsProperties> originalPhysicsProperties = new Dictionary<Rigidbody2D, PhysicsProperties>();

    private struct PhysicsProperties
    {
        public float linearDamping;
        public float angularDamping;
        public float gravityScale;
    }

    void Start()
    {
        if (backpackSurface == null)
        {
            backpackSurface = GetComponent<BackpackSurface>();
        }

        // Find backpack bounds
        if (backpackSurface != null && backpackSurface.physicsWorldRoot != null)
        {
            backpackBounds = backpackSurface.physicsWorldRoot.GetComponentInChildren<MeshCollider>();

            if (backpackBounds != null)
            {
                UpdateBounds();
            }
        }

        // Setup water visuals
        SetupWaterVisuals();
    }

    void Update()
    {
        // Update water level
        UpdateWaterLevel();

        // Update both water visuals
        UpdateWaterVisuals();
    }

    void FixedUpdate()
    {
        // Apply water physics to all objects
        ApplyWaterPhysics();
    }

    /// <summary>
    /// Toggles water contact on/off
    /// </summary>
    public void ToggleWaterContact()
    {
        waterContactEnabled = !waterContactEnabled;
        Debug.Log($"Water Contact: {(waterContactEnabled ? "ENABLED" : "DISABLED")}");
    }

    /// <summary>
    /// Updates the water level based on fill/drain rates
    /// </summary>
    void UpdateWaterLevel()
    {
        if (waterContactEnabled)
        {
            // Fill water
            waterLevel += fillRate * Time.deltaTime;
            waterLevel = Mathf.Min(waterLevel, maxWaterLevel);
        }
        else
        {
            // Drain water
            waterLevel -= drainRate * Time.deltaTime;
            waterLevel = Mathf.Max(waterLevel, 0f);
        }
    }

    /// <summary>
    /// Updates bounds information
    /// </summary>
    void UpdateBounds()
    {
        if (backpackBounds == null) return;

        Bounds bounds = backpackBounds.bounds;
        boundsMin = bounds.min;
        boundsMax = bounds.max;
        backpackHeight = bounds.size.y;
        backpackWidth = bounds.size.x;
    }

    /// <summary>
    /// Gets the world Y position of the water surface
    /// </summary>
    float GetWaterSurfaceY()
    {
        if (backpackBounds == null) return 0f;

        // Water level ranges from bottom (min.y) to top based on waterLevel percentage
        return boundsMin.y + (backpackHeight * waterLevel);
    }

    /// <summary>
    /// Applies buoyancy and drag forces to objects in water
    /// </summary>
    void ApplyWaterPhysics()
    {
        if (backpackSurface == null || backpackSurface.physicsWorldRoot == null) return;
        if (waterLevel <= 0f) return; // No water, no physics

        float waterSurfaceY = GetWaterSurfaceY();

        // Get all rigidbodies in the backpack
        Rigidbody2D[] rigidbodies = backpackSurface.physicsWorldRoot.GetComponentsInChildren<Rigidbody2D>();

        foreach (Rigidbody2D rb in rigidbodies)
        {
            if (rb == null || rb.isKinematic) continue;

            float objectY = rb.position.y;

            // Check if object is below water surface
            if (objectY < waterSurfaceY)
            {
                // Calculate submersion percentage (0 = at surface, 1 = fully submerged)
                float submersionDepth = waterSurfaceY - objectY;
                float submersionPercent = Mathf.Clamp01(submersionDepth / 0.5f); // Assume 0.5 unit object height

                // Store original properties if not already stored
                if (!originalPhysicsProperties.ContainsKey(rb))
                {
                    originalPhysicsProperties[rb] = new PhysicsProperties
                    {
                        linearDamping = rb.linearDamping,
                        angularDamping = rb.angularDamping,
                        gravityScale = rb.gravityScale
                    };
                }

                // Apply buoyancy force (upward)
                Vector2 buoyancy = Vector2.up * buoyancyForce * submersionPercent;
                rb.AddForce(buoyancy, ForceMode2D.Force);

                // Apply water drag
                rb.linearDamping = Mathf.Lerp(originalPhysicsProperties[rb].linearDamping, waterDrag, submersionPercent);
                rb.angularDamping = Mathf.Lerp(originalPhysicsProperties[rb].angularDamping, waterAngularDrag, submersionPercent);

                // Reduce gravity effect when in water
                rb.gravityScale = originalPhysicsProperties[rb].gravityScale * (1f - submersionPercent * 0.5f);
            }
            else
            {
                // Object is above water, restore original properties
                if (originalPhysicsProperties.ContainsKey(rb))
                {
                    PhysicsProperties props = originalPhysicsProperties[rb];
                    rb.linearDamping = props.linearDamping;
                    rb.angularDamping = props.angularDamping;
                    rb.gravityScale = props.gravityScale;
                }
            }
        }
    }

    /// <summary>
    /// Sets up both water visual representations
    /// </summary>
    void SetupWaterVisuals()
    {
        // Setup physics world water visual (existing functionality)
        if (waterVisualPhysics != null)
        {
            SetupWaterVisual(waterVisualPhysics, true);
        }

        // Create and setup visual plane water visual
        if (waterVisualPlane == null && backpackSurface != null && backpackSurface.visualPlane != null)
        {
            GameObject planeWater = GameObject.CreatePrimitive(PrimitiveType.Quad);
            planeWater.name = "WaterVisual_Plane";

            // Remove collider so it doesn't interfere with raycasting
            Destroy(planeWater.GetComponent<Collider>());

            waterVisualPlane = planeWater.transform;
            waterVisualPlane.SetParent(backpackSurface.visualPlane);

            SetupWaterVisual(waterVisualPlane, false);
        }
    }

    /// <summary>
    /// Sets up a single water visual representation
    /// </summary>
    void SetupWaterVisual(Transform waterTransform, bool isPhysicsWorld)
    {
        if (waterTransform == null) return;

        // Ensure it has a renderer
        MeshRenderer renderer = waterTransform.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            renderer = waterTransform.gameObject.AddComponent<MeshRenderer>();
        }

        // Create/assign material
        /*
        Material waterMaterial = new Material(Shader.Find("Standard"));
        waterMaterial.color = waterColor;
        waterMaterial.SetFloat("_Mode", 3); // Transparent mode
        waterMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        waterMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        waterMaterial.SetInt("_ZWrite", 0);
        waterMaterial.DisableKeyword("_ALPHATEST_ON");
        waterMaterial.EnableKeyword("_ALPHABLEND_ON");
        waterMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        waterMaterial.renderQueue = 3000;

        renderer.material = waterMaterial;
        */

        // Parent to appropriate root
        if (isPhysicsWorld && backpackSurface != null && backpackSurface.physicsWorldRoot != null)
        {
            waterTransform.SetParent(backpackSurface.physicsWorldRoot);
        }

        // Initial position
        UpdateWaterVisuals();
    }

    /// <summary>
    /// Updates both water visuals to match current water level
    /// </summary>
    void UpdateWaterVisuals()
    {
        // Update physics world visual
        if (waterVisualPhysics != null)
        {
            UpdateSingleWaterVisual(waterVisualPhysics, true);
        }

        // Update visual plane visual
        if (waterVisualPlane != null)
        {
            UpdateSingleWaterVisual(waterVisualPlane, false);
        }
    }

    /// <summary>
    /// Updates a single water visual to match current water level
    /// </summary>
    void UpdateSingleWaterVisual(Transform waterTransform, bool isPhysicsWorld)
    {
        if (waterTransform == null || backpackBounds == null) return;

        if (waterLevel <= 0f)
        {
            // Hide water when empty
            waterTransform.gameObject.SetActive(false);
            return;
        }

        waterTransform.gameObject.SetActive(true);

        // Calculate water height in local space
        float waterHeight = backpackHeight * waterLevel;

        if (isPhysicsWorld)
        {
            // Position in physics world (absolute world space)
            Vector3 waterPosition = new Vector3(
                (boundsMin.x + boundsMax.x) * 0.5f, // Center X
                boundsMin.y + (waterHeight * 0.5f),  // Center Y based on water height
                backpackSurface.physicsWorldRoot.position.z // Same Z as physics world
            );

            waterTransform.position = waterPosition;
            waterTransform.rotation = Quaternion.identity;

            // Scale water visual
            waterTransform.localScale = new Vector3(backpackWidth, waterHeight, 1f);
        }
        else
        {
            // Position on visual plane (local space)
            // Calculate position in physics world's LOCAL space
            Vector3 physicsLocalPos = new Vector3(
                0f, // Center X in local space
                (boundsMin.y - backpackSurface.physicsWorldRoot.position.y) + (waterHeight * 0.5f), // Y in local space
                0f
            );

            // The visual plane and physics world share the same local coordinate system
            // So we can directly use the physics local position
            waterTransform.localPosition = new Vector3(
                physicsLocalPos.x,
                physicsLocalPos.y,
                -0.01f // Slightly behind to not obstruct items
            );

            waterTransform.localRotation = Quaternion.identity;

            // Scale in local space
            waterTransform.localScale = new Vector3(backpackWidth, waterHeight, 1f);
        }

        // Update transparency based on water level
        MeshRenderer renderer = waterTransform.GetComponent<MeshRenderer>();
        if (renderer != null && renderer.material != null)
        {
            Color color = waterColor;
            color.a = waterColor.a * waterLevel; // Fade in as water fills
            renderer.material.color = color;
        }
    }

    /// <summary>
    /// Checks if a point is underwater
    /// </summary>
    public bool IsUnderwater(Vector2 position)
    {
        return position.y < GetWaterSurfaceY();
    }

    /// <summary>
    /// Gets the submersion percentage for a position (0 = surface, 1 = fully submerged)
    /// </summary>
    public float GetSubmersionPercent(Vector2 position, float objectHeight = 0.5f)
    {
        float waterSurfaceY = GetWaterSurfaceY();
        if (position.y >= waterSurfaceY) return 0f;

        float submersionDepth = waterSurfaceY - position.y;
        return Mathf.Clamp01(submersionDepth / objectHeight);
    }

    void OnDrawGizmos()
    {
        if (backpackBounds == null) return;

        // Draw water surface line
        float waterSurfaceY = GetWaterSurfaceY();

        Gizmos.color = new Color(0.2f, 0.5f, 0.8f, 0.8f);
        Vector3 start = new Vector3(boundsMin.x, waterSurfaceY, backpackSurface.physicsWorldRoot.position.z);
        Vector3 end = new Vector3(boundsMax.x, waterSurfaceY, backpackSurface.physicsWorldRoot.position.z);
        Gizmos.DrawLine(start, end);

        // Draw water level indicator
        Gizmos.DrawWireCube(
            new Vector3((boundsMin.x + boundsMax.x) * 0.5f, boundsMin.y + (backpackHeight * waterLevel * 0.5f), 0),
            new Vector3(boundsMax.x - boundsMin.x, backpackHeight * waterLevel, 0.1f)
        );
    }
}