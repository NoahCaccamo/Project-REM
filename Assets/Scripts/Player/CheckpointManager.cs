using KinematicCharacterController.Examples;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CheckpointManager : MonoBehaviour
{
    public static CheckpointManager Instance { get; private set; }

    public ExampleCharacterController Character;
    public Vector3 currentCheckpointPosition;
    public Quaternion currentCheckpointRotation;

    public Transform currentCheckpointTransform;
    public string checkpointSceneName;

    void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(this.gameObject);
    }

    void Start()
    {
        Character = GetComponent<ExampleCharacterController>();
    }

    public void SetCheckpoint(Transform checkpointTransform, string sceneName)
    {
        currentCheckpointTransform = checkpointTransform;
        checkpointSceneName = sceneName;
    }

    // move to a general util
    private bool IsSceneLoaded(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.name == sceneName && scene.isLoaded)
            {
                return true;
            }
        }
        return false;
    }

    public void RespawnAtCheckpoint()
    {
        if (currentCheckpointPosition != null)
        {
            if (IsSceneLoaded(checkpointSceneName))
            {
                TeleportToCheckpoint();
            }
            else
            {
                Debug.LogError("wat the fuck");
                // if its a different scene we need to handle transform differently or it breaks which it isnt doing rn
                StartCoroutine(LoadSceneAndTeleport());
            }
        }
        else
        {
            Debug.LogWarning("No checkpoint set for respawn!");
            // respawn at default position?
        }
    }
    private void TeleportToCheckpoint()
    {
        if (currentCheckpointPosition != null && Character != null && Character.Motor != null)
        {
            Character.Motor.SetPosition(currentCheckpointTransform.position);
            // rotate towards objective direction here?
            // Character.Motor.SetRotation(currentCheckpoint.rotation);
            SetLookRotation();
            Character.Motor.BaseVelocity = Vector3.zero;
        }
    }

    private IEnumerator LoadSceneAndTeleport()
    {
        // Load the scene asynchronously
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(checkpointSceneName);

        // Wait until the scene is fully loaded
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        // Scene is loaded, now teleport the player
        yield return new WaitForEndOfFrame(); // Wait one more frame for scene initialization
        TeleportToCheckpoint();
    }

    private void SetLookRotation()
    {
        ExampleCharacterCamera camera = Camera.main.GetComponent<ExampleCharacterCamera>();

        if (camera != null && currentCheckpointTransform != null)
        {
            // Get the forward direction from the checkpoint
            Vector3 forward = currentCheckpointTransform.forward;

            // Calculate planar direction (horizontal look direction)
            Vector3 planarDirection = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;

            // Only proceed if we have a valid planar direction
            if (planarDirection.sqrMagnitude > 0.01f)
            {
                // Set the planar direction - this persists because it's a public property
                camera.PlanarDirection = planarDirection;

                // Calculate vertical angle from the forward direction
                float verticalAngle = -Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg;

                // Clamp to camera's vertical limits
                verticalAngle = Mathf.Clamp(verticalAngle, camera.MinVerticalAngle, camera.MaxVerticalAngle);

                // CRITICAL FIX: Update the camera's internal vertical angle state using reflection
                // This is what prevents the camera from snapping back to the old rotation
                FieldInfo targetVerticalAngleField = typeof(ExampleCharacterCamera).GetField("_targetVerticalAngle", BindingFlags.NonPublic | BindingFlags.Instance);
                if (targetVerticalAngleField != null)
                {
                    targetVerticalAngleField.SetValue(camera, verticalAngle);
                }

                // Calculate the target rotation for immediate visual update
                Quaternion planarRot = Quaternion.LookRotation(planarDirection, Vector3.up);
                Quaternion verticalRot = Quaternion.Euler(verticalAngle, 0, 0);

                // Apply rotation to camera transform (this provides immediate visual feedback)
                camera.Transform.rotation = planarRot * verticalRot;
            }
        }
    }
}
