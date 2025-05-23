using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class CharacterScriptEditorWindow : EditorWindow
{
    [MenuItem("Window/Character Script Editor")]
    static void Init()
    {
        EditorWindow.GetWindow(typeof(CharacterScriptEditorWindow), false, "Character Script Editor");
    }
    CoreData coreData;
    int currentScriptIndex;

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
        GUILayout.BeginHorizontal();
        GUILayout.Label("Script Index : " + currentScriptIndex.ToString());
        currentScriptIndex = EditorGUILayout.Popup(currentScriptIndex, coreData.GetScriptNames());
        if (GUILayout.Button("New Character Script")) { coreData.characterScripts.Add(new CharacterScript()); currentScriptIndex = coreData.characterScripts.Count - 1; }
        GUILayout.EndHorizontal();
        CharacterScript currentScript = coreData.characterScripts[currentScriptIndex];

        currentScript.name = EditorGUILayout.TextField("Name: ", currentScript.name);

        int deleteParam = -1;

        for (int p = 0; p < currentScript.parameters.Count; p++)
        {
            ScriptParameter currentParam = currentScript.parameters[p];
            GUILayout.BeginHorizontal();
            currentParam.name = EditorGUILayout.TextField("Parameter Name : ", currentParam.name);
            if (GUILayout.Button("x", GUILayout.Width(25))) { deleteParam = p; }
            GUILayout.EndHorizontal();
            currentParam.val = EditorGUILayout.FloatField("Default Value : ", currentParam.val);
        }

        if (deleteParam > -1) { currentScript.parameters.RemoveAt(deleteParam); }

        if (GUILayout.Button("+", GUILayout.Width(25)))
        {
            currentScript.parameters.Add(new ScriptParameter());
        }


        GUILayout.EndScrollView();
        EditorUtility.SetDirty(coreData);
    }
}
