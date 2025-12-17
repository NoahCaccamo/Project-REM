using UnityEngine;
using KinematicCharacterController.Examples;

public class VendingMachineButton : MonoBehaviour, IInteractable
{
    [SerializeField] private VendingMachine vendingMachine;
    private bool hasStock = true;
    public int price = 2;
    public GameObject item;

    public bool CanInteract()
    {
        return vendingMachine != null && hasStock && vendingMachine.storedCredits >= price;
    }

    public void OnInteract(PlayerContext context)
    {
        if (!CanInteract()) return;
        vendingMachine.DispenseItem(item);
        hasStock = false;
        vendingMachine.storedCredits -= price;
        // call function to update visuals of vending machine and button
    }
}