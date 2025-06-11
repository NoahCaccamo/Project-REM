using UnityEngine;

public class HairWindController : MonoBehaviour
{
    // Array to hold references to the HairBone scripts (attached to actual armature bones)
    public HairBone[] hairBones;

    [Header("Base Gust Settings")]
    public Vector3 baseDirection = new Vector3(0, 0, 1);
    public float waveSpeed = 5f;
    public float waveFrequency = 2f;
    public float waveAmplitude = 5f;
    public bool animateWind = true;

    [Header("Player Motion Influence")]
    public Transform playerRoot; // Set to the object that moves (e.g., player character's main transform)
    public Transform characterRoot; // Set to the object whose rotation influences wind (e.g., player's body)
    public float velocityInfluence = 1.5f;
    public float maxMotionWind = 15f;

    [Header("Rotation Influence")]
    public float rotationInfluence = 5f; // How much player's rotation affects wind
    public float rootRadius = 0.5f;      // Radius from player center to hair base (for rotational wind)

    [Header("Sideways Amplification")]
    public float sidewaysMultiplier = 2f; // Amplify sideways velocity component

    [Header("Vertical Amplification")]
    public float verticalMultiplier = 2f; // Amplify vertical velocity component

    // Cache for player velocity and rotation
    private Vector3 lastPlayerPosition;
    private Vector3 playerVelocity;
    private Quaternion lastPlayerRotation;
    private Vector3 rotationalVelocity; // Angular velocity vector

    // Reference to the SkinnedMeshRenderer whose material we will modify
    //private SkinnedMeshRenderer _targetRenderer;

    void Start()
    {
        // Auto-assign playerRoot if not set, assume it's this object's parent
        if (playerRoot == null)
            playerRoot = transform.parent;

        // Initialize last positions/rotations for velocity calculation
        lastPlayerPosition = playerRoot.position;
        lastPlayerRotation = characterRoot.rotation;

        // Get all HairBone scripts attached to children (i.e., the actual bones)
        if (hairBones == null || hairBones.Length == 0)
            hairBones = GetComponentsInChildren<HairBone>();

        // Find the SkinnedMeshRenderer on this GameObject or in its children
        /*
        _targetRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        if (_targetRenderer == null)
        {
            Debug.LogError("HairWindController: No SkinnedMeshRenderer found in children. Cannot set material properties.", this);
        }
        */

        // Initialize each HairBone's player reference (can be done in inspector too)
        foreach (HairBone bone in hairBones)
        {
            if (bone.player == null)
            {
                bone.player = playerRoot;
            }
        }
    }

    void Update()
    {
        UpdatePlayerVelocityAndRotation();

        float time = Time.time;

        // --- Wind and Force Calculation for each HairBone ---
        // Iterate through all hair bones and apply calculated wind/forces
        for (int i = 0; i < hairBones.Length; i++)
        {
            float offset = i / (float)hairBones.Length; // Normalized position along the chain (0 to 1)

            // 1. Base Gust Wave: sinusoidal wave affecting all bones
            float wave = animateWind
                ? Mathf.Sin((time * waveFrequency) - (offset * waveSpeed)) * waveAmplitude
                : 0f;
            Vector3 waveWind = baseDirection.normalized * wave;

            // 2. Player Motion Influence
            // Localize player velocity to amplify sideways/vertical motion relative to player's local space
            Vector3 localVelocity = playerRoot.InverseTransformDirection(playerVelocity);
            localVelocity.x *= sidewaysMultiplier;
            localVelocity.y *= verticalMultiplier; // Amplify vertical movement
            Vector3 amplifiedVelocity = playerRoot.TransformDirection(localVelocity); // Convert back to world space

            // 3. Rotational Influence: how player's turning affects wind on hair
            // Rotational velocity applied at a radius from player's center
            float rotationIntensity = Mathf.Lerp(0.5f, 1.5f, offset); // Later segments might feel more rotation
            Vector3 rotationVelContribution = rotationalVelocity * rootRadius * rotationInfluence * rotationIntensity;

            // Combine linear and rotational motion contributions
            Vector3 combinedMotion = amplifiedVelocity + rotationVelContribution;

            // Fade motion down the chain (tail gets less direct influence from player motion)
            // Use a slight power for a gentler fade at the start of the chain
            float movementFade = Mathf.Pow(1f - offset, 0.05f); // Very gentle fade

            // Limit motion wind magnitude to maxMotionWind
            Vector3 fadedMotionWind = -combinedMotion.normalized * Mathf.Min(combinedMotion.magnitude * velocityInfluence, maxMotionWind);
            // Apply gentle fade over the chain
            fadedMotionWind *= movementFade;

            // Smoother final motion for later segments by blending wave and motion wind
            // The `rotationDelay` was confusing; let's simplify to a blend factor that can be tweaked
            Vector3 finalWind = Vector3.Lerp(waveWind, fadedMotionWind, 0.5f); // Blend evenly

            // Apply calculated wind to the current HairBone script instance
            // hairBones[i].enableWind = true; // Ensure wind is enabled for the bone
            hairBones[i].windDirection = finalWind.normalized; // Direction of the wind force
            hairBones[i].windStrength = finalWind.magnitude;   // Strength of the wind force
        }

        // --- Material Alpha Control (Applied once globally to the entire flame) ---
        // Meter visibility logic (assuming CharacterObject script exists and has specialMeter)
        // REPLACE SLOW GETCOMPONENT CALL!!!! - For demonstration, keep it for now.
        // In a real game, you would pass this value more efficiently, e.g., via a public field
        // set by the CharacterObject itself, or a static reference.
        float meterPercent = 1f; // Default to full visibility if CharacterObject not found
        CharacterObject characterObj = playerRoot.gameObject.GetComponent<CharacterObject>();
        if (characterObj != null)
        {
            meterPercent = Mathf.Clamp01(characterObj.specialMeter / 100f);
        }


        /*
        // Use meterPercent to influence the shader's _MinAlpha property
        // When meterPercent is 1, _MinAlpha could be its default (e.g., 0.1).
        // When meterPercent is 0, _MinAlpha could be higher (e.g., 0.5 or 0.8) for more visibility.
        // Or, if meterPercent implies overall flame visibility, use it to control _MinAlpha directly.
        // Let's make it so 0% meter = max transparency (still _MinAlpha), 100% meter = min transparency (most opaque)
        float targetMinAlpha = Mathf.Lerp(0.5f, _targetRenderer.sharedMaterial.GetFloat("_MinAlpha"), meterPercent);
        // Ensure _MinAlpha is respected by the shader's own property range
        // For simple fade out, we can just map 0-1 to shader's _MinAlpha property.
        // Assuming shader's _MinAlpha property controls the base transparency.
        // The shader itself already handles its own _MinAlpha, so we can just set this to a normalized value.
        // This will globally control the base opacity of the flame effect.
        if (_targetRenderer != null && _targetRenderer.sharedMaterial != null)
        {
            // We can scale the alpha directly based on meterPercent
            // If the shader's _MinAlpha is the actual minimum opacity allowed,
            // we can adjust another property on the shader for overall visibility,
            // or modify the _MinAlpha property itself if it's the primary way to "fade" the flame.
            // Let's assume the shader's _MinAlpha property is meant to be the absolute floor.
            // Instead, let's influence _AlphaFadeOut on the shader, or a new "_OverallAlpha" property.
            // For now, let's use the shader's existing _AlphaFadeOut to control global transparency.
            // Higher meterPercent means less fade out, so lower _AlphaFadeOut value.
            // The range for _AlphaFadeOut is 0-1, so map meterPercent (0-1) to 1-0 for fade.
            float shaderAlphaFadeOut = Mathf.Lerp(1.0f, 0.0f, meterPercent); // 1.0 (max fade) when meter is 0, 0.0 (min fade) when meter is 1
            _targetRenderer.sharedMaterial.SetFloat("_AlphaFadeOut", shaderAlphaFadeOut);
        }
        */
    }

    // Calculates player's linear and rotational velocity
    void UpdatePlayerVelocityAndRotation()
    {
        // Linear velocity
        Vector3 currentPos = playerRoot.position;
        playerVelocity = (currentPos - lastPlayerPosition) / Time.deltaTime;
        lastPlayerPosition = currentPos;

        // Rotational velocity (yaw only)
        Quaternion currentRotation = characterRoot.rotation;
        Quaternion deltaRotation = currentRotation * Quaternion.Inverse(lastPlayerRotation);

        Vector3 axis;
        float angleDegrees;
        deltaRotation.ToAngleAxis(out angleDegrees, out axis);

        if (angleDegrees > 180f) angleDegrees -= 360f; // Normalize angle to -180..180

        // Only consider rotation around the Y-axis (yaw)
        float signedAngleYaw = Vector3.Dot(axis, characterRoot.up) >= 0 ? angleDegrees : -angleDegrees;

        // Convert to angular velocity (radians/sec) for yaw
        float angleRadiansYaw = signedAngleYaw * Mathf.Deg2Rad;
        // Use characterRoot.up to ensure the axis of rotation is correct relative to character
        rotationalVelocity = characterRoot.up * (angleRadiansYaw / Time.deltaTime);

        lastPlayerRotation = currentRotation;
    }
}
