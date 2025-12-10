using UnityEngine;

public class VendingMachine : MonoBehaviour
{
    [SerializeField] private Transform dispensePoint; // Where items spawn
    public int storedCredits = 10;

    public void DispenseItem(GameObject item)
    {
        Instantiate(item, dispensePoint.position, dispensePoint.rotation);
        // Add sound effects, animations, etc.
    }
}