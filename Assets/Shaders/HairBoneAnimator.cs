using UnityEngine;

public class HairBoneAnimator : MonoBehaviour
{
    [Header("Hair Settings")]
    public Transform[] hairBones; // Assign from root to tip
    public float gravityStrength = 1.5f;
    public float waveSpeed = 2f;
    public float waveAmplitude = 10f;

    [Header("Movement Response")]
    public Transform characterTransform; // Typically the root of the character
    public float movementInfluence = 2f;
    public float rotationInfluence = 30f;
    public float smoothing = 5f;

    private Quaternion[] initialLocalRotations;
    private Vector3 lastCharacterPosition;
    private Quaternion lastCharacterRotation;

    void Start()
    {
        initialLocalRotations = new Quaternion[hairBones.Length];
        for (int i = 0; i < hairBones.Length; i++)
        {
            initialLocalRotations[i] = hairBones[i].localRotation;
        }

        lastCharacterPosition = characterTransform.position;
        lastCharacterRotation = characterTransform.rotation;
    }

    void LateUpdate()
    {
        Vector3 characterVelocity = (characterTransform.position - lastCharacterPosition) / Time.deltaTime;
        Quaternion deltaRotation = characterTransform.rotation * Quaternion.Inverse(lastCharacterRotation);

        for (int i = 1; i < hairBones.Length; i++)
        {
            float t = i / (float)(hairBones.Length - 1);

            // Wave: ripple from top to bottom
            float wave = Mathf.Sin(Time.time * waveSpeed + t * 10f) * waveAmplitude;
            Quaternion waveRotation = Quaternion.Euler(wave, 0f, 0f); // Ripple is vertical (pitch)

            // Gravity tilt
            Quaternion gravityRotation = Quaternion.Euler(20f * gravityStrength * t, 0f, 0f); // Forward droop

            // Inertia from movement
            Vector3 localMovement = characterTransform.InverseTransformDirection(characterVelocity);
            float movementTilt = -localMovement.z * movementInfluence * t;
            Quaternion movementRotation = Quaternion.Euler(movementTilt, 0f, 0f);

            // Inertia from rotation
            float yawDelta = Quaternion.Angle(lastCharacterRotation, characterTransform.rotation);
            Quaternion rotationLag = Quaternion.Euler(0f, yawDelta * rotationInfluence * t, 0f);

            // Combine everything
            Quaternion targetRotation = initialLocalRotations[i] * gravityRotation * waveRotation * movementRotation * rotationLag;
            hairBones[i].localRotation = Quaternion.Slerp(hairBones[i].localRotation, targetRotation, Time.deltaTime * smoothing);
        }

        lastCharacterPosition = characterTransform.position;
        lastCharacterRotation = characterTransform.rotation;
    }
}
