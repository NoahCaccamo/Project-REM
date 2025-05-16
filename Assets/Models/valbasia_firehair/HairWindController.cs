using System;
using UnityEngine;
using UnityEngine;

public class HairWindController : MonoBehaviour
{
    public HairBone[] hairBones;

    [Header("Base Gust Settings")]
    public Vector3 baseDirection = new Vector3(0, 0, 1);
    public float waveSpeed = 5f;
    public float waveFrequency = 2f;
    public float waveAmplitude = 5f;
    public bool animateWind = true;

    [Header("Player Motion Influence")]
    public Transform playerRoot; // Set to the object that moves
    public Transform characterRoot;
    public float velocityInfluence = 1.5f;
    public float maxMotionWind = 15f;

    [Header("Rotation Influence")]
    public float rotationInfluence = 5f; // How much rotation affects wind
    public float rootRadius = 0.5f;      // Radius from player center to hair base (meters)

    [Header("Sideways Amplification")]
    public float sidewaysMultiplier = 2f; // Amplify sideways velocity

    [Header("Vertical Amplification")]
    public float verticalMultiplier = 2f; // Amplify sideways velocity

    private Vector3 lastPlayerPosition;
    private Vector3 playerVelocity;

    private Quaternion lastPlayerRotation;
    private Vector3 rotationalVelocity;

    void Start()
    {
        if (playerRoot == null)
            playerRoot = transform.parent;

        lastPlayerPosition = playerRoot.position;
        lastPlayerRotation = characterRoot.rotation;

        if (hairBones == null || hairBones.Length == 0)
            hairBones = GetComponentsInChildren<HairBone>();

        for (int i = 0; i < hairBones.Length; i++)
        {
            Renderer rend = hairBones[i].GetComponentInChildren<Renderer>();
            rend.material.SetFloat("_BoneIndex", i);
        }
    }

    void Update()
    {
        UpdatePlayerVelocityAndRotation();

        float time = Time.time;

        // Meter visibility
        int totalSegments = hairBones.Length;
        // REPLACE SLOW GETCOMPONENT CALL!!!!
        float meterPercent = Mathf.Clamp01(playerRoot.gameObject.GetComponent<CharacterObject>().specialMeter / 100f);
        float visibleSegmentsFloat = meterPercent * totalSegments;

        for (int i = 0; i < hairBones.Length; i++)
        {
            float offset = i / (float)hairBones.Length;

            // Gust wave
            float wave = animateWind
                ? Mathf.Sin((time * waveFrequency) - (offset * waveSpeed)) * waveAmplitude
                : 0f;

            Vector3 waveWind = baseDirection.normalized * wave;

            // Localize velocity and amplify sideways motion
            Vector3 localVelocity = playerRoot.InverseTransformDirection(playerVelocity);
            localVelocity.x *= sidewaysMultiplier;
            localVelocity.y *= verticalMultiplier;
            Vector3 amplifiedVelocity = playerRoot.TransformDirection(localVelocity);

            // Intensify rotational effect based on segment index
            float rotationIntensity = Mathf.Lerp(0.5f, 1.5f, offset);       // later segments rotate harder
            float rotationDelay = Mathf.Lerp(1.0f, 0.4f, offset);           // later segments move slower (smoother)

            Vector3 rotationVelContribution = rotationalVelocity * rootRadius * rotationInfluence * rotationIntensity;

            Vector3 combinedMotion = amplifiedVelocity + rotationVelContribution;

            // Fade motion down the chain (but keep rotation stronger)
            float movementFade = Mathf.Pow(1f - offset, 0.05f);
            Vector3 fadedMotionWind = -combinedMotion.normalized * Mathf.Min(combinedMotion.magnitude * velocityInfluence, maxMotionWind); // times movement fade?

            // Smoother final motion for later segments
            Vector3 smoothedMotion = Vector3.Lerp(waveWind, fadedMotionWind, 0.5f * rotationDelay);

            // Apply to hair bone
            hairBones[i].enableWind = true;
            hairBones[i].windDirection = smoothedMotion.normalized;
            hairBones[i].windStrength = smoothedMotion.magnitude;


            // Determine visibility
            float fade = Mathf.Clamp01((visibleSegmentsFloat - i)); // 1 if fully visible, 0 if fully faded
            fade = Mathf.SmoothStep(0, 1, fade); // Optional: smoother transition

            // Apply to material color
            SetBoneAlpha(hairBones[i], fade);

            /*
            if (alpha < 0.01f)
            {
                hairBones[i].enableWind = false;
                // Optional: hairBones[i].gameObject.SetActive(false);
            }
            */
        }

    }

    void UpdatePlayerVelocityAndRotation()
    {
        // Linear velocity
        Vector3 currentPos = playerRoot.position;
        playerVelocity = (currentPos - lastPlayerPosition) / Time.deltaTime;
        lastPlayerPosition = currentPos;

        // Rotational velocity (signed)
        Quaternion currentRotation = playerRoot.rotation;
        Quaternion deltaRotation = currentRotation * Quaternion.Inverse(lastPlayerRotation);

        Vector3 axis;
        float angleDegrees;

        deltaRotation.ToAngleAxis(out angleDegrees, out axis);

        if (angleDegrees > 180f) angleDegrees -= 360f; // Normalize to -180..180

        // Determine signed rotation around world up (Y axis) — yaw only
        float signedAngle = Vector3.Dot(axis, Vector3.up) >= 0 ? angleDegrees : -angleDegrees;

        // Convert to angular velocity (radians/sec)
        float angleRadians = signedAngle * Mathf.Deg2Rad;
        rotationalVelocity = Vector3.up * (angleRadians / Time.deltaTime); // Only yaw for now

        lastPlayerRotation = currentRotation;
    }

    private MaterialPropertyBlock propertyBlock;
    void SetBoneAlpha(HairBone bone, float alpha)
    {
        // SLOW AF - TESTING ONLY REPLACE THIS GETCOMPONENT CALL!!
        var renderer = bone.GetComponentInChildren<Renderer>();
        if (renderer == null || renderer.material == null) return;


        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        renderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat("_GlobalAlpha", alpha);
        renderer.SetPropertyBlock(propertyBlock);


        // old - for default shader
        //Color currentColor = renderer.material.color;
        //currentColor.a = alpha;
        //renderer.material.color = currentColor;
    }

}


/*
public class HairWindController : MonoBehaviour
{
    public HairBone[] hairBones;

    [Header("Base Gust Settings")]
    public Vector3 baseDirection = new Vector3(0, 0, 1);
    public float waveSpeed = 5f;
    public float waveFrequency = 2f;
    public float waveAmplitude = 5f;
    public bool animateWind = true;

    [Header("Player Motion Influence")]
    public Transform playerRoot; // Set to the object that moves
    public float velocityInfluence = 1.5f;
    public float maxMotionWind = 15f;

    private Vector3 lastPlayerPosition;
    private Vector3 playerVelocity;

    void Start()
    {
        if (playerRoot == null)
            playerRoot = transform.parent;

        lastPlayerPosition = playerRoot.position;

        if (hairBones == null || hairBones.Length == 0)
            hairBones = GetComponentsInChildren<HairBone>();
    }

    void Update()
    {
        UpdatePlayerVelocity();

        float time = Time.time;

        for (int i = 0; i < hairBones.Length; i++)
        {
            float offset = i / (float)hairBones.Length;

            float wave = animateWind
                ? Mathf.Sin((time * waveFrequency) - (offset * waveSpeed)) * waveAmplitude
                : 0f;

            Vector3 waveWind = baseDirection.normalized * wave;

            // Fade motion wind down the chain (tail gets less)
            float movementFade = Mathf.Pow(1f - offset, 0.2f); // Customize curve
            Vector3 fadedMotionWind = -playerVelocity.normalized * Mathf.Min(playerVelocity.magnitude * velocityInfluence, maxMotionWind) * movementFade;

            // Blend both
            Vector3 finalWind = Vector3.Lerp(waveWind, fadedMotionWind, 0.5f);

            hairBones[i].enableWind = true;
            hairBones[i].windDirection = finalWind.normalized;
            hairBones[i].windStrength = finalWind.magnitude;

        }

    }

    void UpdatePlayerVelocity()
    {
        Vector3 currentPos = playerRoot.position;
        playerVelocity = (currentPos - lastPlayerPosition) / Time.deltaTime;
        lastPlayerPosition = currentPos;
    }
}
*/