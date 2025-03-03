using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Core Data", menuName = "Character Action Prototype/Core Data", order = 1)]
[System.Serializable]
public class CoreData : ScriptableObject
{
    // IF YOU RENAME ANY LIST IT WILL DELETE COREDATA SECTION

    // Character States
    public List<CharacterState> characterStates;

    public List<CharacterScript> characterScripts;

    // delete commands
    //public List<InputCommand> commands;

    public List<CommandState> commandStates;

    public List<MoveList> moveLists;

    public List<GameObject> globalPrefabs;


    // TODO: USE THIS IN INPUT BUFFER INSTEAD OF HARDCODING
    public List<RawInput> rawInputs;
    public List<MotionCommand> motionCommands;
    // Save Files


    public string[] GetScriptNames()
    {
        string[] _names = new string[characterScripts.Count];
        for (int i = 0; i < _names.Length; i++)
        {
            _names[i] = characterScripts[i].name;
        }

        return _names;
    }

    public string[] GetStateNames()
    {
        string[] _names = new string[characterStates.Count];
        for (int i = 0; i < _names.Length; i++)
        {
            _names[i] = characterStates[i].stateName;
        }

        return _names;
    }

    public string[] GetPrefabNames()
    {
        string[] _names = new string[globalPrefabs.Count];
        for (int i = 0; i < _names.Length; i++)
        {
            _names[i] = globalPrefabs[i].name; //global default inherited name
        }

        return _names;
    }


    public string[] GetRawInputNames()
    {
        string[] _names = new string[rawInputs.Count];
        for (int i = 0; i < _names.Length; i++)
        {
            _names[i] = rawInputs[i].name;
        }
        return _names;
    }

    public string[] GetFollowUpNames(int _commandState, bool _deleteField)
    {
        int nameCount = commandStates[_commandState].commandSteps.Count;
        if (_deleteField) { nameCount += 2; }
        string[] _names = new string[nameCount];
        for (int i = 0; i < _names.Length; i++)
        {
            if (i < _names.Length - 2)
            {
                _names[i] = commandStates[_commandState].commandSteps[i].idIndex.ToString();
            }
            else if (i < _names.Length - 1)
            {
                _names[i] = "+ ADD";
            }
            else
            {
                _names[i] = "- DELETE";
            }
        }
        return _names;
    }

    public string[] GetMotionCommandNames()
    {
        string[] _names = new string[motionCommands.Count];
        for (int i = 0; i < _names.Length; i++)
        {
            _names[i] = motionCommands[i].name;
        }
        return _names;
    }

    //UNTESTED
    public string[] GetCommandStateNames()
    {
        int nameCount = commandStates.Count;
        string[] _names = new string[nameCount];
        for (int i = 0; i < _names.Length; i++)
        {
            _names[i] = commandStates[i].stateName;
        }
        return _names;
    }
}