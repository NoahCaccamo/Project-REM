using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Delivery/Memory Type")]
public class MemoryType : ScriptableObject
{
    [Header("Visual Identity")]
    public string themeName;
    public Color accentColor;
    public Material environmentMaterial;
    public AudioClip ambientMusic;

    [Header("Room Pool")]
    public List<GameObject> roomPrefabs; // Themed rooms for this memory
                                         // split this into multiple lists per orientation type
    [Header("Breadcrumb Pool")]
    public List<GameObject> crumbPrefabs;

    [Header("Optional Thematic Modifiers")]
    public List<ScriptableObject> modifiers; // IMModifier list (optional)


    [Header("Delivery Info")]
    public string pickupLocation;
    public string dropoffLocation;
}
