using System.Collections;
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

    private DatamoshController _datamoshController;

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

    public void LoadSceneWithSequence(string targetScene)
    {
        StartCoroutine(LoadSceneSequence(targetScene));
    }

    private IEnumerator LoadSceneSequence(string targetScene)
    {
        if (_datamoshController == null)
        {
            _datamoshController = FindObjectOfType<DatamoshController>();
        }

        if (_datamoshController != null)
        {
            _datamoshController.SetMotionStrength(0f);
        }

        yield return null; // wait a frame

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(targetScene);

        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        // wait a couple frames for scene initialization
        yield return null;
        yield return null;

        if (_datamoshController != null)
        {
            _datamoshController.SetMotionStrength(1f);
        }
    }
}