using UnityEngine;
using UnityEngine.SceneManagement;

public class HubExitTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            string previousScene = SceneTransitionManager.Instance.GetPreviousScene();

            if (!string.IsNullOrEmpty(previousScene))
            {
                // Load the scene the player came from
                // The transition manager still has the return entry point ID stored
                SceneManager.LoadScene(previousScene);
            }
            else
            {
                Debug.LogWarning("No previous scene stored. Cannot return from hub.");
            }
        }
    }
}