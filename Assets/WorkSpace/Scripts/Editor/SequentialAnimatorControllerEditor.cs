using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SequentialAnimatorController))]
public class SequentialAnimatorControllerEditor : Editor
{
    private SerializedProperty sequencesProperty;
    private SerializedProperty previousObjectDisableDelayProperty;
    private SerializedProperty completionParticlesProperty;
    private SerializedProperty completionParticleDelayProperty;
    private SerializedProperty mixingProgressBarProperty;
    private SerializedProperty recipeLoadingPanelProperty;
    private SerializedProperty recipeLoadingDurationProperty;
    private SerializedProperty delayBeforeLoadingPanelProperty;

    private void OnEnable()
    {
        sequencesProperty = serializedObject.FindProperty("sequences");
        previousObjectDisableDelayProperty = serializedObject.FindProperty("previousObjectDisableDelay");
        completionParticlesProperty = serializedObject.FindProperty("completionParticles");
        completionParticleDelayProperty = serializedObject.FindProperty("completionParticleDelay");
        mixingProgressBarProperty = serializedObject.FindProperty("mixingProgressBar");
        recipeLoadingPanelProperty = serializedObject.FindProperty("recipeLoadingPanel");
        recipeLoadingDurationProperty = serializedObject.FindProperty("recipeLoadingDuration");
        delayBeforeLoadingPanelProperty = serializedObject.FindProperty("delayBeforeLoadingPanel");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("General Configurations", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            previousObjectDisableDelayProperty,
            new GUIContent("Previous Object Disable Delay"));

        EditorGUILayout.PropertyField(
            completionParticlesProperty,
            new GUIContent("Item Complete Particles"),
            true);

        EditorGUILayout.PropertyField(
            completionParticleDelayProperty,
            new GUIContent("Particle Play Delay"));

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("UI Panels & Progress Controls", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            mixingProgressBarProperty,
            new GUIContent("Mixing Progress Bar (Slider)"));

        EditorGUILayout.PropertyField(
            delayBeforeLoadingPanelProperty,
            new GUIContent("Global Fallback Delay", "Agar kisi recipe mein delay set na ho toh yeh use hoga"));

        EditorGUILayout.PropertyField(
            recipeLoadingPanelProperty,
            new GUIContent("Recipe Loading Panel"));

        EditorGUILayout.PropertyField(
            recipeLoadingDurationProperty,
            new GUIContent("Recipe Loading Duration"));

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Recipes & Sequences", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(sequencesProperty, true);
        EditorGUILayout.Space();

        if (GUILayout.Button("+ Add Recipe", GUILayout.Height(30f)))
        {
            int newIndex = sequencesProperty.arraySize;
            sequencesProperty.InsertArrayElementAtIndex(newIndex);

            SerializedProperty newSequence = sequencesProperty.GetArrayElementAtIndex(newIndex);
            newSequence.FindPropertyRelative("name").stringValue = $"Recipe {newIndex + 1}";
            newSequence.FindPropertyRelative("recipeRootObject").objectReferenceValue = null;
            newSequence.FindPropertyRelative("objects").arraySize = 0;
            newSequence.FindPropertyRelative("delayBeforeLoadingPanel").floatValue = 1.2f;
            newSequence.FindPropertyRelative("requireToolSelection").boolValue = false;
            newSequence.FindPropertyRelative("toolSelectionPanel").objectReferenceValue = null;
            newSequence.FindPropertyRelative("handMixerObject").objectReferenceValue = null;
            newSequence.FindPropertyRelative("machineMixerObject").objectReferenceValue = null;
            newSequence.FindPropertyRelative("showPanelWhenComplete").boolValue = false;
            newSequence.FindPropertyRelative("completionPanel").objectReferenceValue = null;
        }

        serializedObject.ApplyModifiedProperties();
    }
}