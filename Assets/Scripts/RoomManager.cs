using System.Collections.Generic;
using UnityEngine;

public class RoomManager : MonoBehaviour
{
    [SerializeField] private List<SpawnPoint> enemySpawns;

    [Header("Characters")]
    // [SerializeField] GameObject[] aiCharacters;
    [SerializeField] List<GameObject> spawnedInCharacters;

    [SerializeField] SpawnPoint reward;

    public GameObject doors;

    bool roomCleared = false;
    bool challengeActive = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void SpawnAllCharacters()
    {
        foreach (var spawn in enemySpawns)
        {
            GameObject instantiatedCharacter = Instantiate(spawn.prefab, spawn.localTransform.position, spawn.localTransform.rotation);
            instantiatedCharacter.GetComponent<CharacterObject>().roomManager = this;
            spawnedInCharacters.Add(instantiatedCharacter);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.name == "Player")
        {
            if (!roomCleared && !challengeActive)
            {
                doors.SetActive(true);
                SpawnAllCharacters();
                challengeActive = true;
            }
        }
    }

    public void OnEnemyDeath(GameObject opponent)
    {
        if (spawnedInCharacters.Contains(opponent))
        {
            spawnedInCharacters.Remove(opponent);
        }

        if (AreOpponentsDead())
        {
            Time.timeScale = 0.10f;
            doors.SetActive(false);

            roomCleared = true;
            challengeActive = false;
            // GIVE REWARD HERE

            if (reward.prefab && reward.localTransform)
            {
                GameObject _reward = Instantiate(reward.prefab, reward.localTransform.position, reward.localTransform.rotation);
            }
        }
    }

    public bool AreOpponentsDead()
    {
        if (spawnedInCharacters.Count <= 0)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}

[System.Serializable]
public class SpawnPoint
{
    public GameObject prefab;
    public Transform localTransform;
}