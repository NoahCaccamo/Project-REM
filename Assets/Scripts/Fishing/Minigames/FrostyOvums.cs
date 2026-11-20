using UnityEngine;

public class FrostyOvums : MinigameRoot
{
    [SerializeField] GameObject pan;
    private Rigidbody panRigidbody;
    [SerializeField] private float sensitivity = 2.5f;
    [SerializeField] float tiltSpeedX = 50f;
    [SerializeField] float tiltSpeedY = 50f;
    [SerializeField] float tiltAccelerationX = 40f;
    [SerializeField] float tiltAccelerationY = 40f;
    [SerializeField] float maxTiltSpeed = 100f;
    [SerializeField] float minTiltSpeed = 50f;
    [SerializeField] Vector2 bounds = new(8f, 4.5f);   // half extents
    [SerializeField] float lifetime = 0f;              // >0 auto-finishes after N seconds
    [SerializeField] private float smoothSpeed = 20f;

    private float currentX = 0f;
    private float currentZ = 0f;

    private Quaternion initialRotation;

    float timer;

    protected override void OnBegin()
    {

    }
    private void Awake()
    {
        if (pan != null)
        {
            panRigidbody = pan.GetComponent<Rigidbody>();
            if (panRigidbody == null)
            {
                Debug.LogError("Pan GameObject does not have a Rigidbody component.");
            }

            initialRotation = pan.transform.rotation;
        }
    }

    void Update()
    {
        if (pan == null) return;

        // Keyboard version

        /*

        if (Move.x != 0f)
        {
            tiltSpeedX += tiltAccelerationX * Time.deltaTime;
            tiltSpeedX = Mathf.Clamp(tiltSpeedX, minTiltSpeed, maxTiltSpeed);
        }
        else
        {
            tiltSpeedX -= tiltAccelerationX * Time.deltaTime;
            tiltSpeedX = Mathf.Clamp(tiltSpeedX, minTiltSpeed, maxTiltSpeed);
        }

        if (Move.y != 0f)
        {
            tiltSpeedY += tiltAccelerationY * Time.deltaTime;
            tiltSpeedY = Mathf.Clamp(tiltSpeedY, minTiltSpeed, maxTiltSpeed);
        }
        else
        {
            tiltSpeedY -= tiltAccelerationY * Time.deltaTime;
            tiltSpeedY = Mathf.Clamp(tiltSpeedY, minTiltSpeed, maxTiltSpeed);
        }
        */

        // Mouse version
        float mouseX = 0f;
        float mouseY = 0f;

        if (ControlEnabled)
        {
            mouseX = Input.GetAxisRaw("Mouse X") * sensitivity;
            mouseY = Input.GetAxisRaw("Mouse Y") * sensitivity;
        }


        // Keyboard version
        // currentX += Move.y * tiltSpeedY * Time.deltaTime;
        // currentZ -= Move.x * tiltSpeedX * Time.deltaTime;

        // Mouse version
        currentX += mouseY * sensitivity * DeltaTime;
        currentZ -= mouseX * sensitivity * DeltaTime;

        currentX = Mathf.Clamp(currentX, -bounds.y, bounds.y);
        currentZ = Mathf.Clamp(currentZ, -bounds.x, bounds.x);
    }

    private void FixedUpdate()
    {
        if (panRigidbody == null) return;
        // panRigidbody.MoveRotation(Quaternion.Euler(currentX, 0f, currentZ));


        // Calculate target rotation (X = pitch, Z = roll)
        Quaternion targetTilt = Quaternion.Euler(currentX, 0f, currentZ);
        Quaternion targetRotation = initialRotation * targetTilt;

        // Smoothly move the kinematic Rigidbody so it imparts physics velocity to items on top
        Quaternion smoothedRotation = Quaternion.Slerp(panRigidbody.rotation, targetRotation, smoothSpeed * FixedDeltaTime);
        panRigidbody.MoveRotation(smoothedRotation);
    }
}