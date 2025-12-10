using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldSection : MonoBehaviour
{
    [Tooltip("Optional: Spawn point or parent where the room prefab will be instantiated.")]
    public Transform roomContainer;

    private GameObject currentRoom;

    public bool IsUniqueSection;

    public List<GameObject> uniqueRooms;

    private Collider sectionCollider;

    private void Start()
    {
        if (WorldSectionManager.Instance != null)
        {
            WorldSectionManager.Instance.RegisterSection(this);
        }
        else
        {
            Debug.LogWarning($"WorldSectionManager not found for {name}. Section will try to register later.");
            // Optional: you can invoke a delayed registration or retry
            StartCoroutine(WaitForManager());
        }

        sectionCollider = GetComponent<Collider>();
    }

    private IEnumerator WaitForManager()
    {
        while (WorldSectionManager.Instance == null)
            yield return null;

        WorldSectionManager.Instance.RegisterSection(this);
    }


    public void LoadRoom(GameObject roomPrefab)
    {
        if (currentRoom != null)
            Destroy(currentRoom);

        if (roomPrefab != null)
            currentRoom = Instantiate(roomPrefab, roomContainer);
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerCharacter player = other.GetComponent<PlayerCharacter>();
        if (player != null)
        {
            player.EnterSection();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerCharacter player = other.GetComponent<PlayerCharacter>();
        if (player != null)
        {
            player.ExitSection();
        }
    }
}
