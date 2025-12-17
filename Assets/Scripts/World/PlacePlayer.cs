using KinematicCharacterController.Examples;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlacePlayer : MonoBehaviour
{
    [SerializeField] private Transform startPos;
    [SerializeField] private string locationId; // Match this to transport location IDs
    [SerializeField] private bool isHubSpawn; // Mark true if this is in the hub scene

    private void Awake()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return;

        ExampleCharacterController controller = player.GetComponent<ExampleCharacterController>();
        if (controller == null) return;

        // Get the entry point we should spawn at
        string returnEntryPointId = SceneTransitionManager.Instance.GetReturnEntryPointId();

        bool shouldSpawnHere = false;

        if (isHubSpawn)
        {
            // This is the hub spawn point - always spawn here when entering hub
            shouldSpawnHere = true;
        }
        else if (!string.IsNullOrEmpty(returnEntryPointId))
        {
            // We're returning from hub - spawn at the matching entry point
            if (returnEntryPointId == locationId)
            {
                shouldSpawnHere = true;
               // SceneTransitionManager.Instance.ClearTransitionData();
            }
        }

        // Only this specific PlacePlayer should position the player
        if (shouldSpawnHere)
        {
            controller.Motor.SetPosition(startPos.position);
            // controller.Motor.SetRotation(startPos.rotation);
            SetLookRotation();

            // Stop velocity
            controller.Motor.BaseVelocity = Vector3.zero;
        }
    }

    // duplicate logic - also in checkpoint manager. Consider centralizing.
    private void SetLookRotation()
    {
        ExampleCharacterCamera camera = FindObjectOfType<ExampleCharacterCamera>();
        // move to seperate function
        if (camera != null)
        {
            // Set the planar direction (horizontal look direction)
            Vector3 forward = startPos.forward;
            Vector3 planarDirection = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;

            if (planarDirection.sqrMagnitude > 0.01f)
            {
                camera.PlanarDirection = planarDirection;
            }

            // Set the camera transform rotation directly
            // This will be smoothed by the camera system on the next update
            float verticalAngle = -Mathf.Asin(forward.y) * Mathf.Rad2Deg;
            verticalAngle = Mathf.Clamp(verticalAngle, camera.MinVerticalAngle, camera.MaxVerticalAngle);

            Quaternion planarRot = Quaternion.LookRotation(planarDirection, Vector3.up);
            Quaternion verticalRot = Quaternion.Euler(verticalAngle, 0, 0);
            camera.Transform.rotation = planarRot * verticalRot;
        }
    }
}