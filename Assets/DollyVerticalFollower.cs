using UnityEngine;
using Unity.Cinemachine;

public class DollyVerticalFollower : MonoBehaviour
{
    public CinemachineVirtualCamera dollyCam;
    public Transform player;
    public float yOffset = 2f;
    public float smoothTime = 0.2f;

    private float currentYVelocity;

    private void LateUpdate()
    {
        if (dollyCam == null || player == null) return;

        var dolly = dollyCam.GetCinemachineComponent<CinemachineTrackedDolly>();
        if (dolly == null) return;

        // Get current position from dolly
        Vector3 dollyPos = dollyCam.transform.position;

        // Calculate smoothed Y position
        float targetY = player.position.y + yOffset;
        float smoothedY = Mathf.SmoothDamp(dollyPos.y, targetY, ref currentYVelocity, smoothTime);

        // Apply smoothed Y while preserving XZ from dolly
        dollyCam.transform.position = new Vector3(dollyPos.x, smoothedY, dollyPos.z);
    }
}
