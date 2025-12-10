using KinematicCharacterController.Examples;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CheckpointManager : MonoBehaviour
{
    public static CheckpointManager Instance { get; private set; }

    public ExampleCharacterController Character;
    public Vector3 currentCheckpoint;
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

    public void SetCheckpoint(Vector3 checkpoint, string sceneName)
    {
        currentCheckpoint = checkpoint;
        checkpointSceneName = sceneName;
    }

    public void RespawnAtCheckpoint()
    {
        if (currentCheckpoint != null)
        {
            if (checkpointSceneName == SceneManager.GetActiveScene().name)
            {
                TeleportToCheckpoint();
            }
            else
            {
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
        if (currentCheckpoint != null && Character != null && Character.Motor != null)
        {
            Character.Motor.SetPosition(currentCheckpoint);
            // rotate towards objective direction here?
           // Character.Motor.SetRotation(currentCheckpoint.rotation);
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
}
