using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Explicit visual review of the presentation without replaying all cake recipes.</summary>
[InitializeOnLoad]
public static class ServingChefPreview
{
    private const string Flag = "KitchenSet.ServingChefPreview";
    private static CakeFinalPresentation presentation;
    private static float captureEnd, nextFrame;
    private static int frame;
    static ServingChefPreview() { EditorApplication.playModeStateChanged += Changed; }

    [MenuItem("Tools/Cake Maker/Preview Serving Presentation")]
    private static void Run()
    {
        if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene("Assets/Cake Maker/Christmas Cake Making/Scenes/CakeMaking.unity");
        SessionState.SetBool(Flag, true);
        EditorApplication.EnterPlaymode();
    }

    private static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag, false)) return;
        SessionState.SetBool(Flag, false);
        EditorApplication.delayCall += Preview;
    }

    private static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    private static void Preview()
    {
        var recipe = Object.FindFirstObjectByType<Recipe4Controller>(FindObjectsInactive.Include);
        SequentialAnimatorController.Instance.StartSequence(recipe.gameObject);
        var decoration = recipe.GetComponent<CakeDecorationController>();
        var knife = Object.FindFirstObjectByType<KnifeDraggable>();
        if (knife != null)
        {
            typeof(KnifeDraggable).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(knife, null);
            ((CakePaintMask)Field(knife, "paint")).Reveal();
            knife.enabled = false;
        }
        var categories = (CakeDecorationController.Category[])Field(decoration, "categories");
        for (int i = 0; i < categories.Length; i++)
        {
            var category = categories[i];
            foreach (SpriteRenderer target in category.targets)
            {
                target.transform.parent.gameObject.SetActive(true);
                target.gameObject.SetActive(true);
                target.sprite = category.options[i % category.options.Length];
            }
        }
        presentation = (CakeFinalPresentation)Field(recipe, "finalPresentation");
        typeof(CakeFinalPresentation).GetMethod("BuildPlatedCake", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(presentation, new[] { Field(recipe, "cakeItems") });
        SequentialAnimatorController.Instance.StartSequence(presentation.gameObject);
        Directory.CreateDirectory("Temp/CodexServingChef/Frames");
        frame = 0; nextFrame = Time.time; captureEnd = Time.time + 3.1f;
        EditorApplication.update -= CaptureFrame;
        EditorApplication.update += CaptureFrame;
        EditorApplication.update -= Capture;
        EditorApplication.update += Capture;
    }

    private static void CaptureFrame()
    {
        if (!EditorApplication.isPlaying || Time.time > captureEnd)
        { EditorApplication.update -= CaptureFrame; return; }
        if (Time.time < nextFrame || Camera.main == null) return;
        nextFrame = Time.time + 1f / 12f;
        Camera camera = Camera.main;
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var rt = RenderTexture.GetTemporary(960, 540, 24);
        var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
            File.WriteAllBytes("Temp/CodexServingChef/Frames/" + (frame++).ToString("D3") + ".png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(image);
        }
    }

    private static void Capture()
    {
        if (!EditorApplication.isPlaying || presentation == null) { EditorApplication.update -= Capture; return; }
        if (presentation.Photograph == null) return;
        Directory.CreateDirectory("Temp/CodexServingChef");
        File.WriteAllBytes("Temp/CodexServingChef/presentation.png", presentation.Photograph.EncodeToPNG());
        var rig = Object.FindFirstObjectByType<ServingChefRig>();
        Debug.Log("Serving preview captured; rig present=" + (rig != null));
        EditorApplication.update -= Capture;
    }
}
