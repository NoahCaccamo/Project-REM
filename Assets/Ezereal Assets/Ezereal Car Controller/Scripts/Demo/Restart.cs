using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ezereal
{
    public class Restart : MonoBehaviour // Restart Current Scene. Used in Demo scene.
    {
        void OnRestart()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}

