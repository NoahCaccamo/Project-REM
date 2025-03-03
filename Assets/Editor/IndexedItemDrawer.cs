using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(IndexedItemAttribute))]
public class IndexedItemDrawer : PropertyDrawer
{
    public CoreData coreData;


    // override the default unity gui display with a lotta collapses
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // First get the attribute since it contains the range for the slider
        IndexedItemAttribute indexedItem = attribute as IndexedItemAttribute;

        if (coreData == null)
        {
            // t: is a tag you can use in unity search too
            foreach (string guid in AssetDatabase.FindAssets("t: CoreData"))
            {
                coreData = AssetDatabase.LoadAssetAtPath<CoreData>(AssetDatabase.GUIDToAssetPath(guid));
            }
        }

        switch(indexedItem.type)
        {
            case IndexedItemAttribute.IndexedItemType.SCRIPTS:
                property.intValue = EditorGUI.IntPopup(position, property.intValue, coreData.GetScriptNames(), null);
                break;

            case IndexedItemAttribute.IndexedItemType.STATES:
                property.intValue = EditorGUI.IntPopup(position, property.intValue, coreData.GetStateNames(), null);
                break;
            case IndexedItemAttribute.IndexedItemType.RAW_INPUTS:
                property.intValue = EditorGUI.IntPopup(position, property.intValue, coreData.GetRawInputNames(), null);
                break;
            case IndexedItemAttribute.IndexedItemType.MOTION_COMMAND:
                property.intValue = EditorGUI.IntPopup(position, property.intValue, coreData.GetMotionCommandNames(), null);
                break;
        }
        //coreData.SetDirty();
    }
}
