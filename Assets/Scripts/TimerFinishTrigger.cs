using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TimerFinishTrigger : MonoBehaviour
{
    [SerializeField] private SpeedrunTimer timerReference;

    [Header("Trigger Settings")]
    [SerializeField] private string playerTag = "Player";

    private bool hasTriggered = false;

    private void Awake()
    {
        // Find timer if not assigned
        if (timerReference == null)
        {
            timerReference = FindObjectOfType<SpeedrunTimer>();
        }

        // Ensure trigger is enabled
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {

        // Check if it's the player
        if (!other.CompareTag(playerTag))
            return;

        // Stop the timer
        if (timerReference != null)
        {
            timerReference.OnTimerFinish();
            hasTriggered = true;
            Debug.Log($"Timer finished! Final time: {timerReference.GetCurrentTime():F3}s");
        }
        else
        {
            Debug.LogWarning("TimerFinishTrigger: No SpeedrunTimer reference found!");
        }
    }

    private void OnDrawGizmos()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = hasTriggered ? Color.yellow : Color.green;
            Gizmos.DrawWireCube(transform.position, col.bounds.size);
        }
    }
}