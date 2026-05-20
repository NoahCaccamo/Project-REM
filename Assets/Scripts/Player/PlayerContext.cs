using UnityEngine;

public struct PlayerContext
{
    public Transform playerTransform;
    public Camera playerCamera;
    public KinematicCharacterController.KinematicCharacterMotor motor;

    public PlayerContext(Transform transform, Camera camera, KinematicCharacterController.KinematicCharacterMotor motor)
    {
        playerTransform = transform;
        playerCamera = camera;
        this.motor = motor;
    }
}
