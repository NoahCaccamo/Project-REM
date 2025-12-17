using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Delivery/Package")]
public class Package : ScriptableObject
{
    public string packageName;
    public MemoryType memoryType;

    [Tooltip("Additional gameplay modifiers unique to this specific package.")]
    public List<ScriptableObject> modifiers; // IModifier list
}
