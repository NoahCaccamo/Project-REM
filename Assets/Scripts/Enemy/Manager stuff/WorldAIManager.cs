using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WorldAIManager : MonoBehaviour
{
    public static WorldAIManager instance;

    [Header("DEBUG")]
    [SerializeField] bool despawnCharacters = false;
    [SerializeField] bool respawnCharacters = false;

    [Header("Characters")]
    [SerializeField] GameObject[] aiCharacters;
    [SerializeField] List<GameObject> spawnedInCharacters;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // SPAWN ALL AI IN SCENE
        StartCoroutine(WaitForSceneToLoadThenSpawnCharacters());
    }

    private void Update()
    {
        if (respawnCharacters)
        {
            respawnCharacters = false;
            SpawnAllCharacters();
        }
        if (despawnCharacters)
        {
            despawnCharacters = false;
            DespawnAllCharacters();
        }
    }

    private IEnumerator WaitForSceneToLoadThenSpawnCharacters()
    {
        while (!SceneManager.GetActiveScene().isLoaded)
        {
            yield return null;
        }


    }

    private void SpawnAllCharacters()
    {
        foreach (var character in aiCharacters)
        {
            GameObject instantiatedCharacter = Instantiate(character);
            spawnedInCharacters.Add(instantiatedCharacter);
        }
    }

    private void DespawnAllCharacters()
    {
        foreach (var character in spawnedInCharacters)
        {
            // networkcomponent despawn -- make call ?
        }
    }

    private void DisableAllCharacters()
    {
        // TO DO DISABLE CHARACTER GAMEOBJECTS INSTEAD OF SPAWNS
        // CAN BE USED TO DISABLE CHARACTERS THAT ARE FAR FROM PLAYERS TO SAVE MEMORY
        // CHARACTERS CAN BE SPLIT INTO AREAS (AREA_00, AREA_01, AREA_02...)
    }
}
