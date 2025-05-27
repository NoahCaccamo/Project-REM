using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class InputBuffer
{
    /*
    public static string[] rawInputList = new string[] // NEED TO MATCH INPUT ACTION IN SETTINGS
    {
        "Jump",
        "Slash",
        //"Attack",
        "Dash",
        "Slash2"
    };
    */

    //uneeded in new version
    /*
    CoreData coreData;


    public static string[] rawInputList;

    public List<InputBufferItem> inputList = new List<InputBufferItem>();
    */
    //uneeded end

    public List<InputBufferFrame> buffer;
    public static int bufferWindow = 25;

    public List<int> buttonCommandCheck;
    public List<int> motionCommandCheck;

    void InitializeBuffer()
    {
        buffer = new List<InputBufferFrame>();
        for (int i = 0; i < bufferWindow; i++)
        {
            InputBufferFrame newB = new InputBufferFrame();
            newB.InitializeFrame();
            buffer.Add(newB);
        }

        buttonCommandCheck = new List<int>();
        for (int i = 0; i < GameEngine.coreData.rawInputs.Count; i++)
        {
            buttonCommandCheck.Add(-1);
        }

        motionCommandCheck = new List<int>();
        for (int i = 0; i < GameEngine.coreData.motionCommands.Count; i++)
        {
            motionCommandCheck.Add(-1);
        }
    }

    public void Update()
    {
        // this is REALLY bad
        GameEngine.gameEngine.playerInputBuffer = this;
        if (buffer == null) { InitializeBuffer(); }
        if (buffer.Count < GameEngine.coreData.rawInputs.Count || buffer.Count == 0)
        {
            InitializeBuffer();
        }

        for (int i = 0; i < buffer.Count - 1; i++)
        {
            for (int r = 0; r < buffer[i].rawInputs.Count; r++)
            {
                buffer[i].rawInputs[r].value = buffer[i + 1].rawInputs[r].value;
                buffer[i].rawInputs[r].hold = buffer[i + 1].rawInputs[r].hold;
                buffer[i].rawInputs[r].used = buffer[i + 1].rawInputs[r].used;
            }
        }
        buffer[buffer.Count - 1].Update();

        for (int r = 0; r < buttonCommandCheck.Count; r++)
        {
            buttonCommandCheck[r] = -1;
            for (int b = 0; b < buffer.Count; b++)
            {
                if (buffer[b].rawInputs[r].CanExecute()) { buttonCommandCheck[r] = b; }
            }
            if (GameEngine.coreData.rawInputs[r].inputType == RawInput.InputType.IGNORE) { buttonCommandCheck[r] = 0; }
        }

        for (int m = 0; m < motionCommandCheck.Count; m++)
        {
            motionCommandCheck[m] = -1;
            GameEngine.coreData.motionCommands[m].checkStep = 0;
            GameEngine.coreData.motionCommands[m].curAngle = 0;
            for (int b = 0; b < buffer.Count; b++)
            {
                // CHECKK HARDCODED RAWINPUTS ACESS
                if (GameEngine.coreData.motionCommands[m].TestCheck(buffer[b].rawInputs[4].value, buffer[b].rawInputs[5].value))
                { motionCommandCheck[m] = 1; break; }
            }
        }
    }

    public void UseInput(int _i)
    {
        buffer[buttonCommandCheck[_i]].rawInputs[_i].used = true;

        buttonCommandCheck[_i] = -1;
    }

    /*
    public void OldUpdate()
    {
        // this is REALLY bad
        GameEngine.gameEngine.playerInputBuffer = this;
        if (rawInputList == null)
        {
            InitializeBuffer();
        }
        else if (inputList.Count < rawInputList.Length || inputList.Count == 0)
        {
            InitializeBuffer();
        }

        foreach (InputBufferItem c in inputList)
        {
            c.ResolveCommand();

            // shift the array and forget the first input
            for (int b = 0; b < c.buffer.Count - 1; b++)
            {
                c.buffer[b].hold = c.buffer[b + 1].hold;
                c.buffer[b].used = c.buffer[b + 1].used;
            }
        }
    }
    */

    
    void OldInitializeBuffer()
    {
        /*
        if (coreData == null)
        {
            foreach (string guid in AssetDatabase.FindAssets("t: CoreData"))
            {
                coreData = AssetDatabase.LoadAssetAtPath<CoreData>(AssetDatabase.GUIDToAssetPath(guid));
            }
        }

        rawInputList = coreData.GetRawInputNames();
        inputList = new List<InputBufferItem>();
        //foreach (string s in rawInputList)
        //{
        //    InputBufferItem newB = new InputBufferItem();
        //    newB.button = s;
        //    inputList.Add(newB);
        //}
        for (int i = 0; i < rawInputList.Length; i++)
        {
            InputBufferItem newB = new InputBufferItem();
            newB.button = i;
            inputList.Add(newB);
        }
        */
    }


    /*
    public class InputBufferItem
    {
        public int button;
        public List<InputStateItem> buffer;

        public static int bufferWindow = 12;
        public InputBufferItem()
        {
            buffer = new List<InputStateItem>();
            for (int i = 0; i < bufferWindow; i++)
            {
                buffer.Add(new InputStateItem());
            }
        }

        public void ResolveCommand()
        {
            if (Input.GetButton(rawInputList[button]))
            {
                buffer[buffer.Count - 1].HoldUp();
            }
            else
            {
                buffer[buffer.Count - 1].ReleaseHold();
            }
        }
    }
    */

    public class InputStateItem
    {
        // frames held: 1 = just pressed and then counts up
        // can -1 for released??
        public int hold;
        // used to clear out and ignore until its pressed again or used again
        public bool used;

        public bool CanExecute()
        {
            if (hold == 1 && !used) { return true; }
            return false;
        }

        public void HoldUp() // increment hold better name?
        {
            if (hold < 0) { hold = 1; }
            else { hold += 1; }
        }

        public void ReleaseHold()
        {
            if (hold > 0) { hold = -1; used = false; }
            else { hold = 0;  }
        }
    }

    public bool CheckCommand(InputCommand command)
    {
        switch (command.inputType)
        {
            case InputCommand.InputCommandType.Motion:
                if (buttonCommandCheck[command.input] < 0) { return false; }
                if (motionCommandCheck[command.motionCommand] < 0) { return false; }
                return motionCommandCheck[command.motionCommand] >= 0;

            case InputCommand.InputCommandType.RawInput:
                return buttonCommandCheck[command.input] >= 0 &&
                       buffer[buttonCommandCheck[command.input]].rawInputs[command.input].CanExecute();

            case InputCommand.InputCommandType.Hold:
                return buffer[buffer.Count - 1].rawInputs[command.input].hold >= command.framesRequired;


            case InputCommand.InputCommandType.Delay:
                {
                    // Step 1: Find most recent input execution
                    int lastFrameIndex = -1;
                    for (int i = buffer.Count - 1; i >= 0; i--)
                    {
                        if (buffer[i].rawInputs[command.input].used)
                        {
                            lastFrameIndex = i;
                            break;
                        }
                    }

                    // Step 2: Ensure enough frames have passed
                    int currentIndex = buffer.Count - 1;
                    if (lastFrameIndex >= 0 &&
                        currentIndex - lastFrameIndex < command.framesRequired)
                        return false;

                    // Step 3: Check if the button is freshly pressed now
                    return buffer[currentIndex].rawInputs[command.input].CanExecute();
                }

            case InputCommand.InputCommandType.Mash:
                int pressCount = 0;
                foreach (var b in buffer)
                {
                    if (b.rawInputs[command.input].CanExecute())
                        pressCount++;
                }
                return pressCount >= command.mashCount;

            case InputCommand.InputCommandType.TargetingDirectional:
                if (!GameEngine.gameEngine.mainCharacter.targeting)
                {
                    return false;
                }
                int idx = buttonCommandCheck[command.input];
                if (idx < 0) return false;

                var inputFrame = buffer[idx];
                float x = inputFrame.rawInputs[4].value;
                float y = inputFrame.rawInputs[5].value;

                MotionCommand.MotionCommandDirection dir =
                    GameEngine.coreData.motionCommands[command.motionCommand].GetNumPadDirection(x, y);

                return dir == GameEngine.coreData.motionCommands[command.motionCommand].commands[0];


            default:
                return false;
        }
    }
}

public class InputBufferFrame
{
    public List<InputBufferFrameState> rawInputs;

    public void InitializeFrame()
    {
        rawInputs = new List<InputBufferFrameState>();
        for (int i = 0; i < GameEngine.coreData.rawInputs.Count; i++)
        {
            InputBufferFrameState newFS = new InputBufferFrameState();
            newFS.rawInput = i;
            rawInputs.Add(newFS);
        }
    }

    public void Update()
    {
        if (rawInputs == null) { InitializeFrame(); }
        if (rawInputs.Count == 0 || rawInputs.Count != GameEngine.coreData.rawInputs.Count) { InitializeFrame(); }
        foreach(InputBufferFrameState fs in rawInputs)
        {
            fs.ResolveCommand();
        }
    }
}

public class InputBufferFrameState
{
    public int rawInput;
    public float value;
    public int hold;
    public bool used;

    public void ResolveCommand()
    {
        used = false;
        switch (GameEngine.coreData.rawInputs[rawInput].inputType)
        {
            case RawInput.InputType.BUTTON:
                if (Input.GetButton(GameEngine.coreData.rawInputs[rawInput].name))
                {
                    HoldUp(1f);
                }
                else
                {
                    ReleaseHold();
                }
                break;
            case RawInput.InputType.AXIS:
                if (Mathf.Abs(Input.GetAxisRaw(GameEngine.coreData.rawInputs[rawInput].name)) > GameEngine.gameEngine.deadZone)
                {
                    HoldUp(Input.GetAxisRaw(GameEngine.coreData.rawInputs[rawInput].name));
                }
                else
                {
                    ReleaseHold();
                }
                break;
        }
    }

    public void HoldUp(float _val)
    {
        value = _val;

        if (hold < 0) { hold = 1; }
        else { hold += 1; }
    }

    public void ReleaseHold()
    {
        if (hold > 0) { hold = -1; used = false; }
        else { hold = 0; }
        value = 0;
    }

    public bool CanExecute()
    {
        if (hold == 1 && !used) { return true; }
        return false;
    }

    public bool MotionNeutral()
    {
        if (Mathf.Abs(value) < GameEngine.gameEngine.deadZone) { return true; }
        return false;
    }
}

[System.Serializable]
public class RawInput
{
    public enum InputType { BUTTON, AXIS, DOUBLE_AXIS, DIRECTION, IGNORE }
    public InputType inputType;
    public string name;
}

[System.Serializable]
public class MotionCommand
{
    public string name;
    public int motionWindow;
    public int confirmWindow;

    public List<MotionCommandDirection> commands;
    public bool clean;
    public bool anyOrder;

    public int checkStep;

    public int angleChange;

    public float prevAngle;
    public float curAngle;

    public bool TestCheck(float _x, float _y)
    {
        if (angleChange > 0)
        {
            GetNumPadDirection(_x, _y);
            if (curAngle >= angleChange) { return true; }
        }
        else
        {
            if (commands == null) { return true; }

            if (checkStep >= commands.Count) { return true; }
            if (commands[checkStep] == GetNumPadDirection(_x, _y)) { checkStep++; }
        }
        return false;
    }

    public enum MotionCommandDirection
    {
        NEUTRAL, FORWARD, BACK, SIDE, ANGLE_CHANGE
    }

    public MotionCommandDirection GetNumPadDirection(float _x, float _y)
    {
        if (Mathf.Abs(_x) > GameEngine.gameEngine.deadZone || Mathf.Abs(_y) > GameEngine.gameEngine.deadZone)
        {
            Vector3 charForward = GameEngine.gameEngine.mainCharacter.character.transform.forward;
            Vector3 stickForward = new Vector3();
            Vector3 camForward = Camera.main.transform.forward;

            camForward.y = 0;
            camForward.Normalize();
            stickForward += camForward * _y;

            stickForward += Camera.main.transform.right * _x;
            stickForward.y = 0;
            stickForward.Normalize();

            float _stickAngle = Vector2.Angle(new Vector2(charForward.x, charForward.z), new Vector2(stickForward.x, stickForward.z));

            if (angleChange > 0)
            {
                _stickAngle = Vector2.Angle(new Vector2(0f, 1f), new Vector2(stickForward.x, stickForward.z));
                curAngle += Mathf.Abs(_stickAngle - prevAngle);
                prevAngle = _stickAngle;

                return MotionCommandDirection.ANGLE_CHANGE;
            }

            if (_stickAngle < 45) { return MotionCommandDirection.FORWARD; }
            else if (_stickAngle < 135) { return MotionCommandDirection.SIDE; }
            else { return MotionCommandDirection.BACK; }
        }

        return MotionCommandDirection.NEUTRAL;
    }
}