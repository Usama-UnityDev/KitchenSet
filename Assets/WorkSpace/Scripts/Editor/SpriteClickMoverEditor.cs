using UnityEditor;

[CanEditMultipleObjects]
[CustomEditor(typeof(SpriteClickMover))]
public class SpriteClickMoverEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (iterator.name == "parentSprite")
                continue;

            using (new EditorGUI.DisabledScope(iterator.name == "m_Script"))
                EditorGUILayout.PropertyField(iterator, true);

            if (iterator.name == "parentSpriteDisable" &&
                (iterator.boolValue || iterator.hasMultipleDifferentValues))
            {
                SerializedProperty parentSprite =
                    serializedObject.FindProperty("parentSprite");
                EditorGUILayout.PropertyField(parentSprite);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
