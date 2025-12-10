using UnityEngine;
using UnityEngine.SceneManagement;

public class TransportLocation : MonoBehaviour
{
    [SerializeField] private string entryPointId; // Unique ID for this transport location
    [SerializeField] private string targetScene = "smallroom";

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Get the current gameplay scene (not DontDestroyOnLoad scene)
            string currentScene = GetGameplaySceneName();

            // Store which entry point to return to
            SceneTransitionManager.Instance.RegisterTransition(
                entryPointId,  // Return to THIS entry point when coming back
                currentScene
            );

            // Load the hub scene
            SceneManager.LoadScene(targetScene);
        }
    }

    private string GetGameplaySceneName()
    {
        // Get the scene this transport trigger is in (not the player's scene)
        Scene thisScene = gameObject.scene;

        // If this is in a valid scene (not DontDestroyOnLoad), use it
        if (thisScene.IsValid() && !string.IsNullOrEmpty(thisScene.name))
        {
            return thisScene.name;
        }

        // Fallback to active scene
        return SceneManager.GetActiveScene().name;
    }
}