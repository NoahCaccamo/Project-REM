using System.Collections;
using UnityEngine;

public class WorldBreadcrumb : MonoBehaviour
{
    [Tooltip("Optional: Spawn point or parent where the prefab will be instantiated.")]
    public Transform crumbPos;

    private GameObject currentCrumb;

    private void Start()
    {
        if (WorldSectionManager.Instance != null)
        {
            WorldSectionManager.Instance.RegisterBreadcrumb(this);
        }
        else
        {
            Debug.LogWarning($"WorldSectionManager not found for {name}. Crumb will try to register later.");
            // Optional: you can invoke a delayed registration or retry
            StartCoroutine(WaitForManager());
        }
    }

    private IEnumerator WaitForManager()
    {
        while (WorldSectionManager.Instance == null)
            yield return null;

        WorldSectionManager.Instance.RegisterBreadcrumb(this);
    }


    public void LoadCrumb(GameObject crumbPrefab)
    {
        if (currentCrumb != null)
            Destroy(currentCrumb);

        if (crumbPrefab != null)
            currentCrumb = Instantiate(crumbPrefab, crumbPos);
    }
}
