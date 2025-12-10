using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionTrigger : MonoBehaviour
{
    [SerializeField] private string targetSceneName;
    [SerializeField] private KeyCode triggerKey = KeyCode.Return;

    private void Update()
    {
        if (Input.GetKeyDown(triggerKey))
        {
            TriggerTransition();
        }
    }

    public void TriggerTransition()
    {
        SceneManager.LoadSceneAsync(targetSceneName);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            TriggerTransition();
        }
    }
}