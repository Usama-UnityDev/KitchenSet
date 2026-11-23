using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Explicit scene setup and Play Mode verification; never modifies scenes on editor startup.</summary>
[InitializeOnLoad]
public static class JuiceRecipeReview
{
    internal const string ScenePath = "Assets/Cake Maker/Juice/Juice.unity";
    private const string Flag = "KitchenSet.JuiceReview";
    static JuiceRecipeReview()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Flag, false))
            {
                SessionState.SetBool(Flag, false);
                new GameObject("Juice recipe review").AddComponent<JuiceRecipeReviewRunner>();
            }
        };
    }

    [MenuItem("Tools/Juice Mode/Configure Recipe 3")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before configuring the juice scene.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
        // Preserve the user's current scene edits, including an existing win panel.
        EditorSceneManager.SaveScene(scene);
        Transform recipes = Root(scene, "Recipes");
        Transform recipe = recipes.Find("3rd Recipe");
        Transform glass = recipe.Find("Glass 1");
        Transform canvas = Root(scene, "UICanvas");
        var mode = recipes.GetComponent<JuiceModeController>();
        var decoration = recipes.GetComponent<JuiceDecorationController>() ?? recipes.gameObject.AddComponent<JuiceDecorationController>();
        var presentation = recipes.GetComponent<JuiceFinalPresentation>() ?? recipes.gameObject.AddComponent<JuiceFinalPresentation>();
        var exit = glass.GetComponent<SlideOutObject>() ?? glass.gameObject.AddComponent<SlideOutObject>();
        exit.enabled = false;
        exit.direction = SlideOutObject.ExitDirection.Right;
        exit.duration = 0.65f;

        Scene cake = EditorSceneManager.OpenScene("Assets/Cake Maker/Christmas Cake Making/Scenes/CakeMaking.unity", OpenSceneMode.Additive);
        var cakePresentation = cake.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<CakeFinalPresentation>(true)).Single();
        var cakeData = new SerializedObject(cakePresentation);
        GameObject sourcePanel = (GameObject)cakeData.FindProperty("winPanel").objectReferenceValue;
        Transform existing = canvas.Cast<Transform>().FirstOrDefault(child => child.name.Replace(" ", "").Equals("winpanel", StringComparison.OrdinalIgnoreCase));
        GameObject win = existing != null ? existing.gameObject : Object.Instantiate(sourcePanel, canvas, false);
        win.name = "WIN PANEL";
        foreach (Text label in win.GetComponentsInChildren<Text>(true))
            label.text = label.text.Replace("Cake Mode", "Juice Mode");
        var presentData = new SerializedObject(presentation);
        Set(presentData, "winPanel", win);
        foreach (string field in new[] { "chefSprite", "chefModelShader", "presentationSpriteShader" })
            Set(presentData, field, cakeData.FindProperty(field).objectReferenceValue);
        foreach (string field in new[] { "winCard", "winPhotograph", "winPhotoAspect", "nextModeButton" })
        {
            Component original = (Component)cakeData.FindProperty(field).objectReferenceValue;
            string path = AnimationUtility.CalculateTransformPath(original.transform, sourcePanel.transform);
            Transform target = string.IsNullOrEmpty(path) ? win.transform : win.transform.Find(path);
            if (target == null) throw new InvalidOperationException("Missing win panel child: " + path);
            Set(presentData, field, target.GetComponent(original.GetType()));
        }
        presentData.FindProperty("nextModeRequested").FindPropertyRelative("m_PersistentCalls.m_Calls").ClearArray();
        presentData.ApplyModifiedPropertiesWithoutUndo();
        var next = (Button)presentData.FindProperty("nextModeButton").objectReferenceValue;
        next.onClick = new Button.ButtonClickedEvent();
        UnityEventTools.AddPersistentListener(next.onClick, presentation.NextMode);
        Sprite hand = cake.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<CakeDecorationController>(true))
            .Select(c => (Sprite)new SerializedObject(c).FindProperty("handSprite").objectReferenceValue).FirstOrDefault();

        Transform panels = canvas.Find("TroppingSelection");
        var data = new SerializedObject(decoration);
        Set(data, "mode", mode);
        Set(data, "glass", glass);
        Set(data, "glassFront", glass.Find("front").GetComponent<SpriteRenderer>());
        Set(data, "decorationRoot", recipe.Find("GlassDecor"));
        Set(data, "panelRoot", panels);
        Set(data, "categoryPanel", panels.Find("Guide"));
        Set(data, "optionPanel", panels.Find("itemSelection"));
        Set(data, "handSprite", hand);
        var buttons = data.FindProperty("optionButtons");
        buttons.arraySize = 3;
        for (int i = 0; i < 3; i++) buttons.GetArrayElementAtIndex(i).objectReferenceValue = panels.Find("itemSelection/b" + (i + 1)).GetComponent<Button>();
        var categories = data.FindProperty("categories");
        categories.arraySize = 3;
        string[] names = { "Straws", "Lemon", "Umb" }, targets = { "s", "l", "u" }, folders = { "Straw", "Lemons", "Umberallah" };
        var directions = new[] { SlideOutObject.ExitDirection.Top, SlideOutObject.ExitDirection.Right, SlideOutObject.ExitDirection.Left };
        for (int i = 0; i < 3; i++)
        {
            var category = categories.GetArrayElementAtIndex(i);
            category.FindPropertyRelative("name").stringValue = names[i];
            category.FindPropertyRelative("entryDirection").enumValueIndex = (int)directions[i];
            category.FindPropertyRelative("button").objectReferenceValue = panels.Find("Guide/" + names[i]).GetComponent<Button>();
            category.FindPropertyRelative("target").objectReferenceValue = recipe.Find("GlassDecor/" + targets[i]).GetComponent<SpriteRenderer>();
            var options = category.FindPropertyRelative("options");
            options.arraySize = 3;
            for (int j = 0; j < 3; j++)
            {
                string path = "Assets/Cake Maker/Juice/UI/" + folders[i] + "/" + j + (i == 0 && j == 0 ? ".asset" : ".png");
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) throw new InvalidOperationException("Missing garnish sprite: " + path);
                options.GetArrayElementAtIndex(j).objectReferenceValue = sprite;
            }
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        var modeData = new SerializedObject(mode);
        Set(modeData, "toppingSelectionPanel", panels.gameObject);
        Set(modeData, "winPanel", win);
        Set(modeData, "finalDecoration", decoration);
        Set(modeData, "finalPresentation", presentation);
        Set(modeData, "finalGlassExit", exit);
        modeData.ApplyModifiedPropertiesWithoutUndo();
        panels.gameObject.SetActive(false);
        win.SetActive(false);
        EditorSceneManager.CloseScene(cake, true);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        const string homeScene = "Assets/Common/Home.unity";
        var buildScenes = EditorBuildSettings.scenes.ToList();
        var home = buildScenes.FirstOrDefault(item => item.path == homeScene);
        if (home == null) buildScenes.Add(new EditorBuildSettingsScene(homeScene, true));
        else home.enabled = true;
        EditorBuildSettings.scenes = buildScenes.ToArray();
        Debug.Log("Juice Recipe 3 configured: three garnishes, right glass exit, two-hand serving and scene win panel.");
    }

    private static Transform Root(Scene scene, string name) => scene.GetRootGameObjects().Single(go => go.name == name).transform;
    private static void Set(SerializedObject data, string name, Object value) => data.FindProperty(name).objectReferenceValue = value;

    [MenuItem("Tools/Juice Mode/Validate Recipe 3 (Play Mode)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Directory.CreateDirectory("Temp/JuiceReview");
        SessionState.SetBool(Flag, true);
        EditorApplication.EnterPlaymode();
    }

    [MenuItem("Tools/Juice Mode/Configure and Validate Recipe 3")]
    public static void ConfigureAndRun() { Configure(); Run(); }
}

public sealed class JuiceRecipeReviewRunner : MonoBehaviour
{
    private float deadline;
    private bool finished;
    private void Awake() => deadline = Time.realtimeSinceStartup + 70f;
    private void Update() { if (!finished && Time.realtimeSinceStartup > deadline) Finish(false, "Timed out"); }
    private static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private IEnumerator Start()
    {
        // Load the saved recipe only in Play Mode, preserving any unsaved editor scene.
        DontDestroyOnLoad(gameObject);
        Scene scene = EditorSceneManager.LoadSceneInPlayMode(JuiceRecipeReview.ScenePath,
            new LoadSceneParameters(LoadSceneMode.Single));
        while (!scene.isLoaded)
            yield return null;
        var mode = Object.FindFirstObjectByType<JuiceModeController>();
        var modeData = new SerializedObject(mode);
        modeData.FindProperty("previewRecipeIndex").intValue = 2;
        modeData.ApplyModifiedPropertiesWithoutUndo();
        mode.RestartSequence();
        yield return new WaitForSeconds(1.3f);
        var work = Check();
        while (true)
        {
            object current;
            try { if (!work.MoveNext()) break; current = work.Current; }
            catch (Exception error) { Finish(false, error.ToString()); yield break; }
            yield return current;
        }
    }

    private IEnumerator Check()
    {
        var mode = Object.FindFirstObjectByType<JuiceModeController>();
        var controller = Object.FindFirstObjectByType<JuiceDecorationController>();
        var pour = Object.FindFirstObjectByType<DragToPour2D>();
        var panel = (RectTransform)Field(controller, "panelRoot");
        var win = (GameObject)Field(mode, "winPanel");
        Require(!panel.gameObject.activeSelf && !win.activeSelf, "Panels visible before pouring");
        Vector3 glassPosition = controller.Glass.position;
        Vector3 glassScale = controller.Glass.localScale;
        // Invoke the real pour routine, including the jug's exit callback.
        pour.StartCoroutine((IEnumerator)typeof(DragToPour2D).GetMethod("Pour", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pour, null));
        float until = Time.realtimeSinceStartup + 12f;
        while (!panel.gameObject.activeSelf && Time.realtimeSinceStartup < until)
        {
            Require(Vector3.Distance(controller.Glass.position, glassPosition) < 0.01f && Vector3.Distance(controller.Glass.localScale, glassScale) < 0.01f, "Glass popped before decoration");
            yield return null;
        }
        Require(panel.gameObject.activeSelf, "Pour did not open garnish panels");
        var categoryPanel = (RectTransform)Field(controller, "categoryPanel");
        var optionPanel = (RectTransform)Field(controller, "optionPanel");
        Require(categoryPanel.localPosition.x > ((Vector3)Field(controller, "categoryHome")).x &&
                optionPanel.localPosition.x > ((Vector3)Field(controller, "optionHome")).x,
            "The garnish panels did not enter from the right");
        yield return new WaitForSeconds(0.7f);
        ScreenCapture.CaptureScreenshot("Temp/JuiceReview/panels.png");
        mode.ConfirmToppings();
        Require(!win.activeSelf && controller.Glass.gameObject.activeSelf, "Early confirmation bypassed garnishes");
        var categories = (JuiceDecorationController.Category[])Field(controller, "categories");
        var options = (Button[])Field(controller, "optionButtons");
        var hand = (RectTransform)Field(controller, "hand");
        var strawIcon = (RectTransform)categories[0].button.transform.GetChild(0);
        Vector3 strawShaft = strawIcon.TransformPoint(Rect.NormalizedToPoint(strawIcon.rect, new Vector2(0.27f, 0.63f)));
        Require(hand.gameObject.activeSelf && Vector3.Distance(hand.position, strawShaft) < 0.5f,
            "The pointer missed the straw artwork");
        int[] order = { 0, 2, 1 };
        Camera camera = Camera.main;
        for (int step = 0; step < order.Length; step++)
        {
            int index = order[step];
            var category = categories[index];
            Vector3 home = category.target.transform.position;
            category.button.onClick.Invoke();
            Require(hand.gameObject.activeSelf == (index == 0), "The pointer must only guide the straw");
            if (index == 0)
            {
                foreach (Button reference in options)
                {
                    float guideDeadline = Time.time + 1.5f;
                    while (!PointsAt(hand, reference) && Time.time < guideDeadline)
                        yield return null;
                    Require(PointsAt(hand, reference), "The pointer skipped a straw option");
                }
            }
            int option = (step + 1) % 3;
            options[option].onClick.Invoke();
            Require(category.target.gameObject.activeSelf && !hand.gameObject.activeSelf, "Option click did not start placement");
            Vector3 entry = camera.WorldToViewportPoint(category.target.transform.position);
            Require(index == 0 ? entry.y > 1f : index == 1 ? entry.x > 1f : entry.x < 0f, "Garnish entered from the wrong edge");
            yield return new WaitForSeconds(0.65f);
            Require(category.complete && !category.button.interactable, "Completed category was not locked");
            Require(category.target.sprite == category.options[option] && Vector3.Distance(category.target.transform.position, home) < 0.01f,
                "Garnish did not land at its authored position");
            Require(!win.activeSelf && controller.Glass.gameObject.activeSelf, "Glass finalized before the end of decoration");
        }
        Require(controller.IsComplete, "All three placements did not finalize the glass");
        float end = Time.realtimeSinceStartup + 15f;
        bool slidRight = false;
        float slideStarted = -1f;
        float glassCleared = -1f;
        while (controller.Glass.gameObject.activeSelf && Time.realtimeSinceStartup < end)
        {
            if (slideStarted < 0f && controller.Glass.position.x > glassPosition.x + 0.01f)
                slideStarted = Time.time;
            if (glassCleared < 0f && camera.WorldToViewportPoint(controller.GlassFront.bounds.min).x > 1f)
                glassCleared = Time.time;
            slidRight |= camera.WorldToViewportPoint(controller.Glass.position).x > 1f;
            yield return null;
        }
        Require(slidRight && !controller.Glass.gameObject.activeSelf, "Glass did not exit right");
        Require(slideStarted >= 0f && glassCleared - slideStarted > 0.7f, "The glass crossed the screen too quickly");
        var presentation = Object.FindFirstObjectByType<JuiceFinalPresentation>();
        Transform display = null;
        while (display == null && Time.realtimeSinceStartup < end)
        {
            display = presentation.transform.Find("Chef carrying the juice");
            if (display == null) yield return null;
        }
        Require(display != null && camera.WorldToViewportPoint(display.position).x > 1f && !win.activeSelf,
            "The chef did not enter from the right after the glass exit");
        while (!win.activeSelf && Time.realtimeSinceStartup < end) yield return null;
        Require(win.activeSelf && presentation.Photograph != null, "Serving did not produce the win photograph");
        Require(!controller.Glass.gameObject.activeSelf, "Original glass failed to slide out");
        Require(Object.FindFirstObjectByType<ServingChefRig>() != null, "Two-hand chef rig missing");
        Transform served = presentation.transform.Find("Chef carrying the juice/Held decorated glass/Player decorated juice glass");
        Require(served != null && served.GetComponentsInChildren<SpriteRenderer>().Length >= 6, "Serving copy lost juice or decorations");
        File.WriteAllBytes("Temp/JuiceReview/served-drink.png", presentation.Photograph.EncodeToPNG());
        yield return new WaitForSeconds(0.5f);
        ScreenCapture.CaptureScreenshot("Temp/JuiceReview/win.png");
        yield return new WaitForSeconds(0.4f);
        Finish(true, "Straw pointer touches the category and all three options in order. The glass clears the right edge at the slower pace before chef and win.");
    }

    private static bool PointsAt(RectTransform hand, Button button)
    {
        var rect = (RectTransform)button.transform;
        Vector3 center = rect.TransformPoint(rect.rect.center);
        return hand.gameObject.activeSelf && Vector3.Distance(hand.position, center) < 0.5f;
    }

    private void Finish(bool pass, string message)
    {
        if (finished) return;
        finished = true;
        string result = (pass ? "PASS: " : "FAIL: ") + message;
        File.WriteAllText("Temp/JuiceReview/result.txt", result);
        if (pass) Debug.Log(result); else Debug.LogError(result);
        if (Application.isBatchMode) EditorApplication.Exit(pass ? 0 : 1);
        else EditorApplication.ExitPlaymode();
    }
}
