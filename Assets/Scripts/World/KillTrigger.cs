using UnityEngine;

public class KillTrigger : MonoBehaviour
{
    private CheckpointManager checkpointManager;
    private DatamoshController datamoshController;

    void Start()
    {
        checkpointManager = CheckpointManager.Instance;
        datamoshController = FindObjectOfType<DatamoshController>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            datamoshController?.StartDatamosh(4f);
            datamoshController?.DisableMotionStrengthTemporarily(0.5f);
            checkpointManager.RespawnAtCheckpoint();
        }
    }
}
