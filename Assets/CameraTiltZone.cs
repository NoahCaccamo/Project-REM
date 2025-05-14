using UnityEngine;
using Unity.Cinemachine;

public class CameraTiltZone : MonoBehaviour
{
    public Vector2 desiredTilt = new Vector2(10f, 5f); // Pan (Y), Tilt (X)
    public float transitionSpeed = 3f;

    private CinemachineCamera vcam;
    private CinemachinePanTilt panTilt;
    private bool inZone = false;

    void Start()
    {
        vcam = FindObjectOfType<CinemachineCamera>();
        if (vcam != null)
        {
            panTilt = vcam.GetComponent<CinemachinePanTilt>();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            inZone = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            inZone = false;
        }
    }

    void Update()
    {
        if (panTilt == null) return;

        // Lerp pan/tilt angles toward or away from desired values
        Vector2 targetAngles = inZone ? desiredTilt : Vector2.zero;

        panTilt.PanAxis.Value = Mathf.Lerp(panTilt.PanAxis.Value, targetAngles.x, Time.deltaTime * transitionSpeed);
        panTilt.TiltAxis.Value = Mathf.Lerp(panTilt.TiltAxis.Value, targetAngles.y, Time.deltaTime * transitionSpeed);
    }
}
