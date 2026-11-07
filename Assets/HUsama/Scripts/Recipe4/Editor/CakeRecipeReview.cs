using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Opt-in local review; never runs during normal gameplay or editor startup.</summary>
[InitializeOnLoad]
public static class CakeRecipeReview
{
    private const string Flag = "CakeRecipeReview.Running";
    static CakeRecipeReview() { EditorApplication.playModeStateChanged += StateChanged; }

    [MenuItem("Tools/Cake Maker/Validate Recipe 4 (Play Mode)")]
    public static void Run()
    {
        if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory("Temp/CodexCakeReview");
        EditorSceneManager.OpenScene("Assets/Cake Maker/Christmas Cake Making/Scenes/CakeMaking.unity");
        SessionState.SetBool(Flag, true);
        EditorApplication.EnterPlaymode();
    }

    private static void StateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Flag, false))
            new GameObject("Local cake review runner").AddComponent<CakeRecipeReviewRunner>();
    }

    internal static void Finish(string result, bool pass)
    {
        File.WriteAllText("Temp/CodexCakeReview/result.txt", result);
        SessionState.SetBool(Flag, false);
        Debug.Log(result);
        if (Application.isBatchMode) EditorApplication.Exit(pass ? 0 : 1);
        else EditorApplication.ExitPlaymode();
    }
}

public class CakeRecipeReviewRunner : MonoBehaviour
{
    private float deadline;
    private void Awake() { DontDestroyOnLoad(gameObject); deadline = Time.realtimeSinceStartup + 180f; }
    private void Update() { if (Time.realtimeSinceStartup > deadline) Fail("Validation timed out"); }
    private IEnumerator Start()
    {
        yield return null;
        var sequence = SequentialAnimatorController.Instance;
        var recipe = UnityEngine.Object.FindFirstObjectByType<Recipe4Controller>(FindObjectsInactive.Include);
        sequence.StartSequence(recipe.gameObject);
        yield return new WaitForSeconds(0.5f);
        var controller = recipe.GetComponent<CakeDecorationController>();
        var categories = (CakeDecorationController.Category[])Field(controller, "categories");
        var knife = UnityEngine.Object.FindFirstObjectByType<KnifeDraggable>();
        var paint = (CakePaintMask)Field(knife, "paint");
        if (paint == null) { Fail("Knife mask was not initialized"); yield break; }
        paint.Paint(((SpriteRenderer)Field(knife, "cakeLayerRenderer")).bounds.center, 0.3f);
        if (paint.Coverage <= 0f || paint.Coverage >= 0.5f) { Fail("Frosting must paint locally"); yield break; }
        paint.Reveal();
        knife.enabled = false;
        recipe.OnFrostingFinished();
        yield return new WaitForSeconds(4f);
        // Exercise selection in a non-sequential order: all unfinished categories must be available.
        int[] order = { 2, 0, 1, 3 };
        foreach (int index in order)
        {
            var category = categories[index];
            if (!category.button.interactable) { Fail("Unfinished category was locked: " + index); yield break; }
            controller.SelectCategory(index);
            controller.SelectOption(index % category.options.Length);
            yield return new WaitForSeconds(0.6f);
            ValidateScreenBounds(category.tool.transform, category.tool.GetComponentsInChildren<SpriteRenderer>());
            controller.SetDragging(true);
            bool done = false;
            for (int sample = 0; sample < 1600 && !done; sample++)
            {
                Bounds b = controller.GetApplicationBounds(index);
                Vector3 point;
                if (category.targets.Length > 1)
                {
                    int target = Array.FindIndex(category.dwell, t => t < 0.22f);
                    point = category.targets[Mathf.Max(0, target)].bounds.center;
                }
                else point = new Vector3(Mathf.Lerp(b.min.x, b.max.x, sample % 24 / 23f),
                    Mathf.Lerp(b.min.y, b.max.y, (sample / 24) % 18 / 17f), b.center.z);
                done = controller.Apply(index, 0.05f, point);
                yield return null;
            }
            if (!done) { Fail("Decoration never completed: " + index); yield break; }
            if (index != 3) category.tool.ReturnHome(true);
            yield return new WaitForSeconds(0.9f);
            if (!category.complete || category.button.interactable) { Fail("Completed category still selectable: " + index); yield break; }
            if (index == 2 && ((RectTransform)Field(controller, "hand")).gameObject.activeSelf)
            { Fail("Tutorial repeated after first category"); yield break; }
        }
        float deadline = Time.realtimeSinceStartup + 20f;
        CakeFinalPresentation presentation = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            presentation = UnityEngine.Object.FindFirstObjectByType<CakeFinalPresentation>();
            if (presentation != null && presentation.Photograph != null) break;
            yield return null;
        }
        if (presentation == null || presentation.Photograph == null) { Fail("Completion photograph was not created"); yield break; }
        File.WriteAllBytes("Temp/CodexCakeReview/completed-cake.png", presentation.Photograph.EncodeToPNG());
        yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot("Temp/CodexCakeReview/win-panel.png");
        yield return new WaitForSeconds(0.3f);
        if (!Application.CanStreamedLevelBeLoaded("Home")) { Fail("Home is missing from build scenes"); yield break; }
        presentation.NextMode();
        yield return null;
        if (SceneManager.GetActiveScene().name != "Home") { Fail("Next did not load Home"); yield break; }
        CakeRecipeReview.Finish("PASS: local frosting; all categories available in arbitrary order; paint completion; completed-category locks; first-use guide; full tool bounds at corners; chef + cake photograph; Win panel; Next loads Home.", true);
    }

    private static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Fail(string message) => CakeRecipeReview.Finish("FAIL: " + message, false);

    private void ValidateScreenBounds(Transform tool, SpriteRenderer[] renderers)
    {
        Vector3 position = tool.position, scale = tool.localScale;
        Quaternion rotation = tool.localRotation;
        Camera camera = Camera.main;
        for (int corner = 0; corner < 4; corner++)
        {
            tool.localScale = scale * 1.15f;
            tool.localRotation = rotation * Quaternion.Euler(0, 0, corner * 17f);
            Vector3 screen = camera.WorldToScreenPoint(tool.position);
            screen.x = (corner & 1) == 0 ? -300f : Screen.width + 300f;
            screen.y = (corner & 2) == 0 ? -300f : Screen.height + 300f;
            tool.position = camera.ScreenToWorldPoint(screen);
            CakeToolScreenBounds.Constrain(tool, camera, renderers);
            foreach (var visual in renderers)
            {
                if (!visual.gameObject.activeInHierarchy) continue;
                Vector3 min = camera.WorldToScreenPoint(visual.bounds.min), max = camera.WorldToScreenPoint(visual.bounds.max);
                if (min.x < -1 || min.y < -1 || max.x > Screen.width + 1 || max.y > Screen.height + 1)
                    throw new Exception("Tool escaped screen: " + tool.name);
            }
        }
        tool.position = position; tool.localScale = scale; tool.localRotation = rotation;
    }

}
