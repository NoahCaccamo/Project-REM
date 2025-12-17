using UnityEngine;

[RequireComponent(typeof(Camera))]
[ExecuteAlways]
public class EnableCameraMotionVectors : MonoBehaviour
{
    void OnEnable()
    {
        GetComponent<Camera>().depthTextureMode |= DepthTextureMode.MotionVectors;
    }
}