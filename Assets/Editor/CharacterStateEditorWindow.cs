using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class CharacterStateEditorWindow : EditorWindow
{
    [MenuItem("Window/Character State Editor")]
    static void Init()
    {
        EditorWindow.GetWindow(typeof(CharacterStateEditorWindow), false, "Character State Editor");
    }
    CoreData coreData;
    CharacterState currentCharacterState;
    int currentStateIndex;
    bool eventFold;

    Vector2 scrollView;

    private void OnGUI()
    {

        // make this a global call?
        if (coreData == null)
        {
            foreach (string guid in AssetDatabase.FindAssets("t: CoreData"))
            {
                coreData = AssetDatabase.LoadAssetAtPath<CoreData>(AssetDatabase.GUIDToAssetPath(guid));
            }
        }
        scrollView = GUILayout.BeginScrollView(scrollView);
        currentCharacterState = coreData.characterStates[currentStateIndex];

        GUILayout.BeginHorizontal();
        GUILayout.Label(currentStateIndex.ToString() + " : " + currentCharacterState.stateName, GUILayout.Width(200));
        currentStateIndex = EditorGUILayout.Popup(
            currentStateIndex, coreData.GetStateNames());

        if (GUILayout.Button("New Character State")) { coreData.characterStates.Add(new CharacterState()); currentStateIndex = coreData.characterStates.Count - 1; }

        GUILayout.EndHorizontal();

        currentCharacterState.stateName = EditorGUILayout.TextField("State Name : ", currentCharacterState.stateName, GUILayout.Width(500));
        // ANIMATION
        GUILayout.BeginHorizontal();
        currentCharacterState.length = EditorGUILayout.FloatField("Length : ", currentCharacterState.length);
        currentCharacterState.blendRate = EditorGUILayout.FloatField("Blend Rate : ", currentCharacterState.blendRate);
        currentCharacterState.loop = GUILayout.Toggle(currentCharacterState.loop, "Loop?", EditorStyles.miniButton);
        GUILayout.EndHorizontal();

        // FLAGS
        currentCharacterState.groundedReq = GUILayout.Toggle(currentCharacterState.groundedReq, "Grounded?", EditorStyles.miniButton, GUILayout.Width(75));

        // EVENTS!
        GUILayout.Label("");
        //GUILayout.Label("Events");
        eventFold = EditorGUILayout.Foldout(eventFold, "Events");
        if (eventFold)
        {
            int deleteEvent = -1;
            // if (GUILayout.Button("+", EditorStyles.miniButton, GUILayout.Width(35))) { currentCharacterState.events.Add(new StateEvent()); }
            for (int e = 0; e < currentCharacterState.events.Count; e++)
            {
                StateEvent currentEvent = currentCharacterState.events[e];
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("X", EditorStyles.miniButton, GUILayout.Width(30))) { deleteEvent = e; }
                currentEvent.active = EditorGUILayout.Toggle(currentEvent.active, GUILayout.Width(20));
                GUILayout.Label(e.ToString() + " : ", GUILayout.Width(25));
                EditorGUILayout.MinMaxSlider(ref currentEvent.start, ref currentEvent.end, 0f, currentCharacterState.length, GUILayout.Width(400));
                GUILayout.Label(Mathf.Round(currentEvent.start).ToString() + " ~ " + Mathf.Round(currentEvent.end).ToString(), GUILayout.Width(75));
                currentEvent.script = EditorGUILayout.Popup(currentEvent.script, coreData.GetScriptNames());
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();

                if (currentEvent.parameters.Count != coreData.characterScripts[currentEvent.script].parameters.Count)
                {
                    currentEvent.parameters = new List<ScriptParameter>();
                    for (int i = 0; i < coreData.characterScripts[currentEvent.script].parameters.Count; i++)
                    {
                        currentEvent.parameters.Add(new ScriptParameter());
                    }
                }

                for (int p = 0; p < currentEvent.parameters.Count; p++)
                {
                    if (p % 3 == 0) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); GUILayout.Label("", GUILayout.Width(250)); }
                    GUILayout.Label(coreData.characterScripts[currentEvent.script].parameters[p].name + " : ", GUILayout.Width(85));
                    currentEvent.parameters[p].val = EditorGUILayout.FloatField(currentEvent.parameters[p].val, GUILayout.Width(75));
                }
                GUILayout.EndHorizontal();
                GUILayout.Label("");
            }
            if (deleteEvent > -1) { currentCharacterState.events.RemoveAt(deleteEvent); }
            if (GUILayout.Button("+", EditorStyles.miniButton, GUILayout.Width(35))) { currentCharacterState.events.Add(new StateEvent()); }
            GUILayout.Label("");
        }

        GUILayout.EndScrollView();
        EditorUtility.SetDirty(coreData);
    }
}
