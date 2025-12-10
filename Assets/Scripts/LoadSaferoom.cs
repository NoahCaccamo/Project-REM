using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadSaferoom : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        SceneManager.LoadScene("SmallRoom");
    }
}
