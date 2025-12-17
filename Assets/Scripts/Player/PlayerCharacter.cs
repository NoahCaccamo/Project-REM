using KinematicCharacterController;
using KinematicCharacterController.Examples;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.Rendering.ReloadAttribute;

public class PlayerCharacter : MonoBehaviour
{
    public CharacterStats BaseStats;
    public CharacterStats CurrentStats;
    public MemoryType currentPackage;
    // private List<IModifier> activeModifiers = new();


    public ModifierHandler modifierHandler;

    // player references
    public ExamplePlayer examplePlayer;
    public KinematicCharacterMotor motor;

    void Awake()
    {
        examplePlayer = GetComponent<ExamplePlayer>();
        motor = GetComponent<KinematicCharacterMotor>();
        modifierHandler = GetComponent<ModifierHandler>();
    }

    void Start()
    {
        DontDestroyOnLoad(this.gameObject);
        CurrentStats = new CharacterStats(BaseStats);
    }

    public void AcceptPackage(MemoryType package)
    {
        DropPackage();
        currentPackage = package;


        // commented out to test sections
        /*
        foreach (var obj in package.modifiers)
        {
            if (obj is IModifier modifier)
                modifierHandler.ApplyModifier(modifier);
        }
        */
    }

    public void DropPackage()
    {
        modifierHandler.RemoveAll();
        CurrentStats = new CharacterStats(BaseStats);
        currentPackage = null;
    }

    public void EnterSection()
    {
        if (currentPackage == null)
            return;

        foreach (var obj in currentPackage.modifiers)
        {
            if (obj is IModifier modifier)
                modifierHandler.ApplyModifier(modifier);
        }
    }

    public void ExitSection()
    {
        modifierHandler.RemoveAll();

        if (currentPackage == null)
            return;

        foreach (var obj in currentPackage.modifiers)
        {
            if (obj is IModifier modifier)
                modifierHandler.ApplyTimedModifier(modifier, 3f);
        }
    }

    /* old
             public void AcceptPackage(Package package)
        {
            DropPackage(); // remove old modifiers
            currentPackage = package;

            foreach (var obj in package.modifiers)
            {
                if (obj is IModifier modifier)
                {
                    modifier.Apply(this);
                    activeModifiers.Add(modifier);
                }
            }
        }

        public void DropPackage()
        {
            foreach (var modifier in activeModifiers)
                modifier.Remove(this);

            activeModifiers.Clear();
            currentPackage = null;
            CurrentStats = new CharacterStats(BaseStats); // reset
        }
     */
}
