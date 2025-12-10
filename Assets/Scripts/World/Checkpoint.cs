using UnityEngine;
using UnityEngine.SceneManagement;

public class Checkpoint : MonoBehaviour
{
    private CheckpointManager checkpointManager;
    private Vector3 checkpointTransform;
    public string scene;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        checkpointManager = CheckpointManager.Instance;
        checkpointTransform = this.transform.position;


        Scene thisScene = gameObject.scene;

        // If this is in a valid scene (not DontDestroyOnLoad), use it
        if (thisScene.IsValid() && !string.IsNullOrEmpty(thisScene.name))
        {
            scene = thisScene.name;
        } else
        {
            Debug.LogError("Checkpoint not in a valid scene!");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            checkpointManager.SetCheckpoint(checkpointTransform, scene);
        }
    }

    // can expand this later to have a bounding box, where the plaer is respawned at the closest point within the box
}
