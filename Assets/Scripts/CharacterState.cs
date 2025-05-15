using System.Collections;
using System.Collections.Generic;
using UnityEngine;


// state + event
[System.Serializable]
public class CharacterState
{
    public string stateName;

    [HideInInspector]
    public int index;

    public float length;
    public bool loop;
    public float blendRate = 0.1f;

    //public int frame;

    //public float currentTime;
    //public int currentState;

    public List <StateEvent> events;
    public List<Attack> attacks;


    // this is real hardcodey
    public int jumpReq;
    public int meterReq;
    public float dashCooldownReq;
    public bool groundedReq;

    public bool ConditionsMet(CharacterObject chara)
    {
        if (chara.jumps < jumpReq) { return false; }
        if (groundedReq)
        {
            if (chara.aerialFlag) { return false; }
        }

        if (dashCooldownReq > 0)
        {
            if (chara.dashCooldown > 0) { return false; }
            else { chara.dashCooldown = dashCooldownReq; }
        }

        if (meterReq > 0)
        {
            if (chara.specialMeter < meterReq) { return false; }
            else { chara.UseMeter(meterReq); }
        }

        // else { chara.jumps--; } // move out later if we implement prio or use jump for something else
        return true;
    }
}

[System.Serializable]
public class StateEvent
{
    public float start;
    public float end;
    public float variable;

    [IndexedItem(IndexedItemAttribute.IndexedItemType.SCRIPTS)]
    public int script;

    public bool hasExecuted;

    // list of scriptParameters here
}



[System.Serializable]
public class CharacterScript
{
    [HideInInspector]
    public int index;

    public string name;
    // might need more variable pass or flag for advanced in future
    //public float variable;
}

[System.Serializable]
public class InputCommand
{
    [IndexedItem(IndexedItemAttribute.IndexedItemType.MOTION_COMMAND)]
    public int motionCommand;

    [IndexedItem(IndexedItemAttribute.IndexedItemType.RAW_INPUTS)]
    public int input;

    [IndexedItem(IndexedItemAttribute.IndexedItemType.STATES)]
    public int state;

    public List<int> inputs;
}

[System.Serializable]
public class MoveList
{
    public string name = "<NEW MOVE LIST>";
    public List<CommandState> commandStates = new List<CommandState>();

    public MoveList()
    {
        commandStates.Add(new CommandState());
    }
}

[System.Serializable]
public class CommandState
{
    public string stateName;

    // Flags
    public bool aerial;

    // Explicit State
    public bool explicitState;
    [IndexedItem(IndexedItemAttribute.IndexedItemType.STATES)]
    public int state;

    public List<CommandStep> commandSteps;

    [HideInInspector]
    public List<int> omitList;

    [HideInInspector]
    public List<int> nextFollowups;

    public CommandState()
    {
        commandSteps = new List<CommandStep>();
        stateName = "<NEW COMMAND STATE>";
    }

    public CommandStep AddCommandStep()
    {

        foreach(CommandStep s in commandSteps)
        {
            if (!s.activated) { s.activated = true; return s; }
        }
        CommandStep nextStep = new CommandStep(commandSteps.Count);
        nextStep.activated = true;
        commandSteps.Add(nextStep);
        return nextStep;
    }

    public void RemoveChainCommands(int _id)
    {
        if (_id == 0) { return; }
        commandSteps[_id].activated = false;
        commandSteps[_id].followUps = new List<int>();
    }

    public void CleanUpBaseState()
    {

        omitList = new List<int>();

        for (int s = 1; s < commandSteps.Count; s++)
        {
            for (int f = 0; f < commandSteps[s].followUps.Count; f++)
            {
                omitList.Add(commandSteps[s].followUps[f]);
            }
        }

        nextFollowups = new List<int>();
        for (int s = 1; s < commandSteps.Count; s++)
        {
            bool skip = false;
            for (int m = 0; m < omitList.Count; m++)
            {
                if (omitList[m] == s) { skip = true; }
            }
            if (!skip) { nextFollowups.Add(s); }
        }

        commandSteps[0].followUps = nextFollowups;
    }
}

[System.Serializable]
public class CommandStep
{
    public int idIndex;

    public InputCommand command;

    public int priority;

    // can separate from steps
    public List<int> followUps;

    public bool strict; // Altenatively you could make a whole new Command State

    [HideInInspector]
    public Rect myRect;


    public bool activated;

    public void AddFollowUp(int _nextID)
    {
        if (_nextID == 0) { return; }
        if (idIndex == _nextID) { return; }
        for (int i = 0; i < followUps.Count; i++)
        {
            if (followUps[i] == _nextID) { return; }
        }
        followUps.Add(_nextID);
    }

    public CommandStep(int _index)
    {
        idIndex = _index;
        command = new InputCommand();
        followUps = new List<int>();
    }
}
