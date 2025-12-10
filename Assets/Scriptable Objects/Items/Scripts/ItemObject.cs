using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ItemType
{
    Default,
    Decor,
    Food,
    Equipment
}

public abstract class ItemObject : ScriptableObject
{
    public GameObject prefab;
    // bad way to do this??
    public GameObject altPrefab;

    public GameObject heldModel;
    public ItemType type;
    [TextArea(15, 20)]
    public string description;

    public virtual void OnUse(PlayerContext context, Hand hand)
    {
        Debug.Log($"Using {name}, but no special behavior implemented.");
    }
}
