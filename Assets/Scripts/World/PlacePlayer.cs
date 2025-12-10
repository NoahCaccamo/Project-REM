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
            controller.Motor.SetRotation(startPos.rotation);

            // Stop velocity
            controller.Motor.BaseVelocity = Vector3.zero;
        }
    }
}