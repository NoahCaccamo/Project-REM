using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(menuName = "Pet/Pet Data")]
public class PetData : ScriptableObject
{
    [Header("State References")]

    [Header("The state to enter when idle or doing nothing")]
    [IndexedItem(IndexedItemAttribute.IndexedItemType.STATES)]
    public int initialState;

    [Header("Chase State")]
    [IndexedItem(IndexedItemAttribute.IndexedItemType.STATES)]
    public int chaseState;

    public List<PetActionOption> actionOptions = new List<PetActionOption>();

    public float hunger = 5f;

    public float sightRange = 10f;
    public float interactRange = 2f;
    public float moveSpeed = 0.02f;
    public float actionCooldown = 60f;


    public PetActionOption GetActionByName(string name)
    {
        foreach (var action in this.actionOptions)
        {
            if (action.name == name)
                return action;
        }
        return null;
    }

}

[System.Serializable]
public class PetActionOption
{
    public string name;
    [IndexedItem(IndexedItemAttribute.IndexedItemType.STATES)]
    public int stateIndex;
    public float minRange = 0f;
    public float maxRange = 5f;
    public int weight = 1;
    public float cooldown = 1f;
    [HideInInspector] public float cooldownTimer;
}
