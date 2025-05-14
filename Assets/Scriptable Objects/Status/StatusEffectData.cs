using UnityEngine;

[CreateAssetMenu(menuName = "Status Effect")]
public class StatusEffectData : ScriptableObject
{
    public string Name;
    public float Stacks;
    public float Lifetime;

    public GameObject EffectIcon;
}
