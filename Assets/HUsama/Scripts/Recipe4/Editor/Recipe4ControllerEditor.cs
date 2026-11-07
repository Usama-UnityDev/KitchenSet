using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Recipe4Controller))]
public class Recipe4ControllerEditor : Editor
{
    private SerializedProperty creamBowlParentProp;
    private SerializedProperty creamBowlSlideOutProp;
    private SerializedProperty completedCakeTransformProp;
    private SerializedProperty cakeSlideOutProp;
    private SerializedProperty cakePopScaleMultiplierProp;

    //private SerializedProperty requireDecorationPanelProp;
    //private SerializedProperty decorationPanelProp;
    //private SerializedProperty creamBagToolProp;
    //private SerializedProperty toppingDropperProp;

    private void OnEnable()
    {
        creamBowlParentProp = serializedObject.FindProperty("creamBowlParent");
        creamBowlSlideOutProp = serializedObject.FindProperty("creamBowlSlideOut");
        completedCakeTransformProp = serializedObject.FindProperty("completedCakeTransform");
        cakeSlideOutProp = serializedObject.FindProperty("cakeSlideOut");
        cakePopScaleMultiplierProp = serializedObject.FindProperty("cakePopScaleMultiplier");

        //requireDecorationPanelProp = serializedObject.FindProperty("requireDecorationPanel");
        //decorationPanelProp = serializedObject.FindProperty("decorationPanel");
        //creamBagToolProp = serializedObject.FindProperty("creamBagTool");
        //toppingDropperProp = serializedObject.FindProperty("toppingDropper");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Base Cake & Bowl References", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(creamBowlParentProp);
        EditorGUILayout.PropertyField(creamBowlSlideOutProp);
        EditorGUILayout.PropertyField(completedCakeTransformProp);
        EditorGUILayout.PropertyField(cakeSlideOutProp);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cakeItems"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("finalPresentation"));

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Polish & Juice Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(cakePopScaleMultiplierProp);

        //EditorGUILayout.Space(12);
        //EditorGUILayout.LabelField("Decoration Panel Settings (Generic Toggle)", EditorStyles.boldLabel);
        //EditorGUILayout.PropertyField(
        //    requireDecorationPanelProp,
        //    new GUIContent("Require Decoration Panel", "Tick karein agar frosting ke baad side decoration panel show karwana ho"));

        //// Recipe 1 style dynamic toggle: Checkbox on hone par hi fields nazar aayengi!
        //if (requireDecorationPanelProp.boolValue)
        //{
        //    EditorGUI.indentLevel++;
        //    EditorGUILayout.HelpBox("Decoration Panel enabled: Side panel, Piping Bag aur Topping Dropper assign karein.", MessageType.Info);

        //    EditorGUILayout.PropertyField(decorationPanelProp, new GUIContent("Side Decoration Panel"));
        //    EditorGUILayout.PropertyField(creamBagToolProp, new GUIContent("Cream Bag Draggable Tool"));
        //    EditorGUILayout.PropertyField(toppingDropperProp, new GUIContent("Topping Dropper (Strawberries / Sprinkles)"));
        //    EditorGUI.indentLevel--;
        //}

        serializedObject.ApplyModifiedProperties();
    }
}
