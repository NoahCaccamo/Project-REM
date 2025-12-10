using KinematicCharacterController.Examples;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;

public class BackpackSurface : MonoBehaviour
{
    public InventoryObject inventoryData;
    public Transform backpackRoot; // parent for all placed items

    public ExampleCharacterController characterController;

    private Camera playerCamera;

    void Start()
    {
        playerCamera = Camera.main;
    }

    // this is always running, make it run only when inventory is open
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            TryPlaceItemFromHand(true); // true = left hand
        }

        if (Input.GetMouseButtonDown(1))
        {
            TryPlaceItemFromHand(false); // false = right hand
        }
    }

    void TryPlaceItemFromHand(bool isLeftHand)
    {
        Hand activeHand = isLeftHand ? characterController.leftHand : characterController.rightHand;

        if (activeHand.IsEmpty) return;

        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out RaycastHit hit, 3.0f))
        {
            if (hit.collider.gameObject == gameObject)
            {
                PlaceItemAt(hit, activeHand);
            }
        }
    }

    void PlaceItemAt(RaycastHit hit, Hand hand)
    {
        var itemData = hand.heldItem;

        // Save to inventory data
        var slot = new InventorySlot(itemData, 1);
        slot.localPosition = backpackRoot.InverseTransformPoint(hit.point);

        // Orient to surface
        slot.localRotation = Quaternion.LookRotation(hit.normal) * Quaternion.Euler(90, 0, 0);
        inventoryData.Container.Add(slot);

        // Instantiate visual
        var visual = Instantiate(itemData.prefab, backpackRoot);
        visual.transform.localPosition = slot.localPosition;
        visual.transform.localRotation = slot.localRotation;

        visual.transform.GetComponent<Rigidbody>().isKinematic = true;

        hand.Drop();

        //var backpackItem = visual.AddComponent<BackpackItemVisual>();
        //backpackItem.slot = slot;
    }


    float distanceFromCamera = 2f;
    public void OpenBackpack()
    {
        backpackRoot.gameObject.SetActive(true);

        // 1. Calculate spawn point directly in front of camera
        Vector3 spawnPos = playerCamera.transform.position + playerCamera.transform.forward * distanceFromCamera;

        // 2. Orient it to face the camera (so it's centered where the player is looking)
        Quaternion lookRot = Quaternion.LookRotation(spawnPos - playerCamera.transform.position);
        // lookRot *= Quaternion.Euler(rotationOffset); // optional tilt

        // 3. Spawn
        backpackRoot.transform.position = spawnPos;
        backpackRoot.transform.rotation = lookRot;
        /*
        currentBackpack = Instantiate(backpackPrefab, spawnPos, lookRot);
        currentBackpack.GetComponent<BackpackSurface>().inventoryData = inventoryData;

        isOpen = true;
        */
    }

    public void CloseBackpack()
    {
        backpackRoot.gameObject.SetActive(false);
    }
}
