using System.Collections.Generic;
using UnityEngine;

public class ModifierHandler : MonoBehaviour
{
    private List<TimedModifierInstance> activeTimedModifiers = new();
    private List<IModifier> permanentModifiers = new();

    private PlayerCharacter character;

    private void Awake()
    {
        character = GetComponent<PlayerCharacter>();
    }

    private void Update()
    {
        // Update timed modifiers
        for (int i = activeTimedModifiers.Count - 1; i >= 0; i--)
        {
            var instance = activeTimedModifiers[i];
            instance.remainingTime -= Time.deltaTime;

            if (instance.remainingTime <= 0f)
            {
                instance.modifier.Remove(character);
                activeTimedModifiers.RemoveAt(i);
            }
        }
    }

    // ---- UNTYPED / PERMANENT MODIFIERS ----
    public void ApplyModifier(IModifier modifier)
    {
        modifier.Apply(character);
        permanentModifiers.Add(modifier);
    }

    // ---- TIMED MODIFIERS ----
    public void ApplyTimedModifier(IModifier modifier, float duration)
    {
        modifier.Apply(character);
        activeTimedModifiers.Add(new TimedModifierInstance(modifier, duration));
    }

    public void RemoveAll()
    {
        // Remove permanent modifiers
        foreach (var mod in permanentModifiers)
            mod.Remove(character);
        permanentModifiers.Clear();

        // Remove timed modifiers
        foreach (var timed in activeTimedModifiers)
            timed.modifier.Remove(character);
        activeTimedModifiers.Clear();
    }

    private class TimedModifierInstance
    {
        public IModifier modifier;
        public float remainingTime;

        public TimedModifierInstance(IModifier modifier, float time)
        {
            this.modifier = modifier;
            this.remainingTime = time;
        }
    }
}
