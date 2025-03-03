using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameEngine : MonoBehaviour
{
    // public ref can assign in editor
    public CoreData coreDataObject;
    // static ref to that public ref so we can access it from anywhere
    public static CoreData coreData;

    public static float hitStop;

    public static GameEngine gameEngine;

    public float deadZone = 0.2f;

    public CharacterObject mainCharacter;

    public int globalMovelist;

    // public MoveList CurrentMoveList()

    // Start is called before the first frame update
    void Start()
    {
        coreData = coreDataObject;
        gameEngine = this;
    }

    public static void SetHitStop(float _pow)
    {
        if (_pow > hitStop) {  hitStop = _pow; }
    }

    // Update is called once per frame
    void Update()
    {
        if (hitStop > 0) { hitStop--; }
    }

    public static void GlobalPrefab(int _index, GameObject _obj)
    {
        GameObject nextPrefab = Instantiate(coreData.globalPrefabs[_index], _obj.transform.position, Quaternion.identity, _obj.transform.root);

        // if VFX
        foreach (Animator myAnimator in nextPrefab.transform.GetComponentsInChildren<Animator>())
        {
            VFXControl[] behaves = myAnimator.GetBehaviours<VFXControl>();
            for (int i = 0; i < behaves.Length; i++)
            {
                behaves[i].vfxRoot = nextPrefab.transform;
                // behaves[i].deparent = deparent;
                /*
                if (behaves[i].slamRotation)
                {
                    myVfx.transform.rotation = Quaternion.LookRotation(new Vector3(velocityX, 0, velocityZ), Vector3.up);
                }
                */
            }
            // cut off part of animation
            // if stalling vfx bug appears revisit?
            //myAnimator.Update(Time.deltaTime);
        }
    }

    public InputBuffer playerInputBuffer;


    private void OnGUI()
    {
        int xSpace = 25;
        int ySpace = 15;

        for (int i = 0; i < playerInputBuffer.buttonCommandCheck.Count; i++)
        {
            GUI.Label(new Rect(10f + (i * xSpace), 15f, 100, 20), playerInputBuffer.buttonCommandCheck[i].ToString());
        }
        for (int b = 0; b < playerInputBuffer.buffer.Count; b++)
        {
            for (int i = 0; i < playerInputBuffer.buffer[i].rawInputs.Count; i++)
            {
                if (playerInputBuffer.buffer[b].rawInputs[i].used)
                {
                    GUI.Label(new Rect(10f + (i * xSpace), 35f + (b * ySpace), 100, 20), playerInputBuffer.buffer[b].rawInputs[i].hold.ToString("0") + ">");
                }
                else
                {
                    GUI.Label(new Rect(10f + (i * xSpace), 35f + (b * ySpace), 100, 20), playerInputBuffer.buffer[b].rawInputs[i].hold.ToString("0"));
                }
            }
        }

        for (int m = 0; m < playerInputBuffer.motionCommandCheck.Count; m++)
        {
            GUI.Label(new Rect(500f - 25f, m * ySpace, 100, 20), playerInputBuffer.motionCommandCheck[m].ToString());
            GUI.Label(new Rect(500f, m * ySpace, 100, 20), coreData.motionCommands[m].name);
        }
    }
    /* TEMP COMMENT OUT INPUT BUFFER DISPLAY
    private void OnGUI()
    {
        int xSpace = 20;
        int ySpace = 25;

        for (int i = 0; i < playerInputBuffer.inputList.Count; i++)
        {
            GUI.Label(new Rect(xSpace, i * ySpace, 100, 20), playerInputBuffer.inputList[i].button + ":");

            for (int j = 0; j < playerInputBuffer.inputList[i].buffer.Count; j++)
            {
                if (playerInputBuffer.inputList[i].buffer[i].used)
                {
                    GUI.Label(new Rect(j * xSpace + 100, i * ySpace, 100, 20), playerInputBuffer.inputList[i].buffer[j].hold.ToString() + "*");
                }
                else
                {
                    GUI.Label(new Rect(j * xSpace + 100, i * ySpace, 100, 20), playerInputBuffer.inputList[i].buffer[j].hold.ToString());
                }
            }
        }
    }
    */
}
