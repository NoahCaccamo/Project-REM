using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionManager : MonoBehaviour
{
    private static SceneTransitionManager _instance;
    public static SceneTransitionManager Instance {
        get {
            if (_instance == null)
            {
                GameObject go = new GameObject("SceneTransitionManager");
                _instance = go.AddComponent<SceneTransitionManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    // Store the scene and spawn point identifier
    private string _previousScene;
    private string _returnEntryPointId;  // Which entry point to spawn at when returning

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void RegisterTransition(string returnToEntryPointId, string fromScene)
    {
        _returnEntryPointId = returnToEntryPointId;
        _previousScene = fromScene;
        Debug.LogWarning(_returnEntryPointId);
    }

    public string GetReturnEntryPointId()
    {
        return _returnEntryPointId;
    }

    public string GetPreviousScene()
    {
        return _previousScene;
    }

    public void ClearTransitionData()
    {
        _returnEntryPointId = null;
        _previousScene = null;
    }
}