using UnityEditor;
using UnityEngine;

public static class GameObjectComponentSetupEditor
{
    private const string MenuPath = "GameObject/Add Kitchen Item Components %#d";

    [MenuItem(MenuPath, false, 20)]
    private static void AddComponents()
    {
        foreach (GameObject selectedObject in Selection.gameObjects)
        {
            AddIfMissing<BoxCollider2D>(selectedObject);
            Rigidbody2D rigidbody = AddIfMissing<Rigidbody2D>(selectedObject);
            if (rigidbody.bodyType != RigidbodyType2D.Kinematic)
            {
                Undo.RecordObject(rigidbody, "Set Rigidbody2D Kinematic");
                rigidbody.bodyType = RigidbodyType2D.Kinematic;
                EditorUtility.SetDirty(rigidbody);
            }

            AddIfMissing<SpriteClickMover>(selectedObject);

            SlideOutObject slideOut = selectedObject.GetComponent<SlideOutObject>();
            if (slideOut == null)
                slideOut = Undo.AddComponent<SlideOutObject>(selectedObject);

            if (slideOut.enabled)
            {
                Undo.RecordObject(slideOut, "Disable Slide Out Object");
                slideOut.enabled = false;
                EditorUtility.SetDirty(slideOut);
            }
        }
    }

    [MenuItem(MenuPath, true)]
    private static bool CanAddComponents()
    {
        return Selection.gameObjects.Length > 0;
    }

    private static T AddIfMissing<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(target);
    }
}
