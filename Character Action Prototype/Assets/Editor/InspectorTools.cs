using System.Collections;
using UnityEngine;
using UnityEditor;

// whenever theres a hitbox, will draw the custom editor
[CustomEditor(typeof(Hitbox))]
public class InspectorTools : Editor
{
    
    public int attackEventIndex = 0;
    // public ActionObject act;
    public CoreData coreData;
    public CharacterState state;

    public override void OnInspectorGUI()
    {
        Hitbox h = (Hitbox)target;
        DrawDefaultInspector();

        if (coreData == null)
        {
            // t: is a tag you can use in unity search too
            foreach (string guid in AssetDatabase.FindAssets("t: CoreData"))
            {
                //TODO: Check if the name is JUST CoreData
                // Then back it up
                coreData = AssetDatabase.LoadAssetAtPath<CoreData>(AssetDatabase.GUIDToAssetPath(guid));
            }
        }

        // UNEEDED 

        // coreData = EditorGUILayout.ObjectField(coreData, typeof(CoreData), false) as CoreData; uneeded
        //stateIndex = EditorGUILayout.Popup(stateIndex, coreData.GetStateNames());
        //state = coreData.characterStates[stateIndex];
        //act = EditorGUILayout.ObjectField(act, typeof(ActionObject), false) as ActionObject;

        // if its clicked it does something
        if (GUILayout.Button ("Apply Hitbox"))
        {

            state = coreData.characterStates[h.stateIndex];
            for (int i = 0; i < state.attacks.Count; i++)
            {
                Attack atk = state.attacks[i];
                atk.hitboxPos = h.transform.localPosition;
                atk.hitboxScale = h.transform.localScale;
            }
            EditorUtility.SetDirty(coreData);
            AssetDatabase.SaveAssets();
        }
    }
    
}
