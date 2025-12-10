using UnityEngine;

public class KillTrigger : MonoBehaviour
{
    private CheckpointManager checkpointManager;

    void Start()
    {
        checkpointManager = CheckpointManager.Instance;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            checkpointManager.RespawnAtCheckpoint();
        }
    }
}
