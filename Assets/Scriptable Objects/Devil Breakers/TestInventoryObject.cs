using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TestInventory", menuName = "Test/Test Inventory")]
public class TestInventoryObject : ScriptableObject
{
    [SerializeField] public int maxSize = 4;
    [SerializeField] public List<TestItem> items = new();
}

[System.Serializable]
public class TestItem
{
    public string name;
}
