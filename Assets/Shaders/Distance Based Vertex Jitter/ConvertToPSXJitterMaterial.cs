#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class ConvertToPSXJitterMaterial : EditorWindow
{
    public Shader psxJitterShader;
    public List<Material> materialsToConvert = new List<Material>();

    [MenuItem("Tools/Convert Materials to PSX Jitter")]
    public static void ShowWindow()
    {
        GetWindow<ConvertToPSXJitterMaterial>("PSX Material Converter");
    }

    private void OnGUI()
    {
        GUILayout.Label("Convert Materials to PSX Jitter", EditorStyles.boldLabel);

        psxJitterShader = (Shader)EditorGUILayout.ObjectField("PSX Jitter Shader", psxJitterShader, typeof(Shader), false);

        if (GUILayout.Button("Find PSX Jitter Shader"))
        {
            string[] guids = AssetDatabase.FindAssets("PSXJitterShader t:Shader");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                psxJitterShader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            }
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Add Selected Materials"))
        {
            foreach (Object obj in Selection.objects)
            {
                if (obj is Material mat && !materialsToConvert.Contains(mat))
                {
                    materialsToConvert.Add(mat);
                }
            }
        }

        if (GUILayout.Button("Find All Scene Materials"))
        {
            materialsToConvert.Clear();
            Renderer[] renderers = FindObjectsOfType<Renderer>();
            foreach (var renderer in renderers)
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat != null && !materialsToConvert.Contains(mat))
                    {
                        materialsToConvert.Add(mat);
                    }
                }
            }
        }

        if (GUILayout.Button("Clear List"))
        {
            materialsToConvert.Clear();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Materials to convert: {materialsToConvert.Count}");

        if (materialsToConvert.Count > 0 && psxJitterShader != null)
        {
            if (GUILayout.Button("Convert Materials", GUILayout.Height(40)))
            {
                ConvertMaterials();
            }
        }

        // Display list
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Material List:", EditorStyles.boldLabel);
        for (int i = materialsToConvert.Count - 1; i >= 0; i--)
        {
            EditorGUILayout.BeginHorizontal();
            materialsToConvert[i] = (Material)EditorGUILayout.ObjectField(materialsToConvert[i], typeof(Material), false);
            if (GUILayout.Button("Remove", GUILayout.Width(60)))
            {
                materialsToConvert.RemoveAt(i);
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    private void ConvertMaterials()
    {
        if (psxJitterShader == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign the PSX Jitter Shader first!", "OK");
            return;
        }

        int converted = 0;
        foreach (Material mat in materialsToConvert)
        {
            if (mat == null)
                continue;

            // Store original properties
            Texture mainTex = mat.GetTexture("_MainTex") ?? mat.GetTexture("_BaseMap");
            Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") :
                         mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;

            float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
            float smoothness = mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") :
                              mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.5f;

            Texture normalMap = mat.GetTexture("_BumpMap") ?? mat.GetTexture("_NormalMap");
            Texture emissionMap = mat.GetTexture("_EmissionMap");
            Color emissionColor = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;

            // Change shader
            mat.shader = psxJitterShader;

            // Restore properties (adjust property names to match your Shader Graph)
            if (mainTex != null)
                mat.SetTexture("_BaseMap", mainTex);

            mat.SetColor("_BaseColor", color);

            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", metallic);

            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", smoothness);

            if (normalMap != null && mat.HasProperty("_BumpMap"))
                mat.SetTexture("_BumpMap", normalMap);

            if (emissionMap != null && mat.HasProperty("_EmissionMap"))
                mat.SetTexture("_EmissionMap", emissionMap);

            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", emissionColor);

            EditorUtility.SetDirty(mat);
            converted++;
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Success", $"Converted {converted} materials to PSX Jitter shader!", "OK");
    }
}
#endif