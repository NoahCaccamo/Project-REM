using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WorldSectionManager : MonoBehaviour
{
    public static WorldSectionManager Instance { get; private set; }

    private List<WorldSection> sections = new();

    private List<WorldBreadcrumb> breadcrumbs = new();

    private void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(this.gameObject);

#if !UNITY_EDITOR
        SceneManager.LoadScene("Hub", LoadSceneMode.Additive);
        SceneManager.LoadScene("Zone1", LoadSceneMode.Additive);
#endif
    }

    public void RegisterSection(WorldSection section)
    {
        if (!sections.Contains(section))
            sections.Add(section);
    }

    // maybe register a path of crumbs depending on the package
    // then add and remove from list when picking up or dropping pack
    public void RegisterBreadcrumb(WorldBreadcrumb breadcrumb)
    {
        if (!breadcrumbs.Contains(breadcrumb))
            breadcrumbs.Add(breadcrumb);
    }

    public void RefreshWorld(MemoryType memoryType)
    {
        if (memoryType == null) return;

        foreach (var section in sections)
        {
            // Load only from unique pool if its a unique section
            // maybe make a type or dictionary to pair room pools with memory types
            if (section.IsUniqueSection)
            {
                if (section.uniqueRooms.Count > 0)
                {
                    var randomPrefab = section.uniqueRooms[Random.Range(0, section.uniqueRooms.Count)];
                    section.LoadRoom(randomPrefab);
                }
            }
            // Load generic room if not unique
            else
            {
                if (memoryType.roomPrefabs.Count > 0)
                {
                    var randomPrefab = memoryType.roomPrefabs[Random.Range(0, memoryType.roomPrefabs.Count)];
                    section.LoadRoom(randomPrefab);
                }
            }

            // separate into direction and size here
            // horizontal
            // vertical
            // diagonal
            // bigger sizes
        }

        foreach (var crumb in breadcrumbs)
        {

            if (memoryType.crumbPrefabs.Count > 0)
            {
                var randomPrefab = memoryType.crumbPrefabs[Random.Range(0, memoryType.crumbPrefabs.Count)];
                crumb.LoadCrumb(randomPrefab);
            }
        }

        // Optional: Update visuals globally
        RenderSettings.skybox = memoryType.environmentMaterial;

        Camera.main.backgroundColor = memoryType.accentColor;
        // Could also change post-processing, lighting, etc.
    }
}
