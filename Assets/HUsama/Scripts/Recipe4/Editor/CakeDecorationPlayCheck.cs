using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>Optional Play Mode smoke check for painting, task locks, and the completion photograph.</summary>
public static class CakeDecorationPlayCheck
{
    private static CakeDecorationController controller;
    private static CakeDecorationController.Category[] categories;
    private static int category, sample;
    private static float waitUntil, deadline;
    private static bool awaitingPhoto;
    private const string ResultPath = "Temp/CodexCakeReview/play-check.txt";

    [MenuItem("Tools/Cake Maker/Run Decoration Play Check")]
    private static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("Enter Play Mode in CakeMaking before running the decoration check."); return; }
        EditorApplication.update -= Tick;
        controller = UnityEngine.Object.FindFirstObjectByType<CakeDecorationController>();
        if (controller == null) { Debug.LogError("Recipe 4 must be the active recipe."); return; }
        categories = (CakeDecorationController.Category[])typeof(CakeDecorationController).GetField("categories", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
        KnifeDraggable knife = UnityEngine.Object.FindFirstObjectByType<KnifeDraggable>();
        if (knife != null)
        {
            var paint = (CakePaintMask)typeof(KnifeDraggable).GetField("paint", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(knife);
            paint.Reveal();
            knife.enabled = false;
        }
        Recipe4Controller.Instance.OnFrostingFinished();
        category = sample = 0;
        awaitingPhoto = false;
        waitUntil = Time.realtimeSinceStartup + 4f;
        deadline = Time.realtimeSinceStartup + 100f;
        File.WriteAllText(ResultPath, "RUNNING\n");
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || controller == null) { Stop("FAIL: Play Mode ended."); return; }
        if (Time.realtimeSinceStartup < waitUntil) return;
        if (Time.realtimeSinceStartup > deadline) { Stop("FAIL: Timed out at category " + category); return; }
        try
        {
            if (awaitingPhoto)
            {
                var presentation = UnityEngine.Object.FindFirstObjectByType<CakeFinalPresentation>();
                if (presentation == null || presentation.Photograph == null) return;
                Texture2D photo = presentation.Photograph;
                if (photo.width < 20 || photo.height < 20) throw new Exception("Photograph has invalid dimensions.");
                File.WriteAllBytes("Temp/CodexCakeReview/completed-cake.png", photo.EncodeToPNG());
                Stop("PASS: Nozzle mask coverage, completed-button locks, presentation transition and photograph " + photo.width + "x" + photo.height);
                return;
            }
            var selected = categories[category];
            if (sample == 0)
            {
                controller.SelectCategory(category);
                controller.SelectOption(category % selected.options.Length);
                if (selected.selected < 0) throw new Exception("Selection did not activate.");
                if (category == 0)
                {
                    var painter = selected.painters[0];
                    painter.Paint(selected.targets[0].bounds.center, 0.3f);
                    if (painter.Coverage <= 0 || painter.Coverage >= 0.5f)
                        throw new Exception("One nozzle stamp must reveal a local patch, not the whole sprite.");
                    painter.BreakStroke();
                }
                controller.SetDragging(true);
            }
            Bounds bounds = controller.GetApplicationBounds(category);
            Vector3 point;
            if (selected.targets.Length > 1)
            {
                int target = 0;
                while (target < selected.dwell.Length && selected.dwell[target] >= 0.22f) target++;
                point = selected.targets[Mathf.Min(target, selected.targets.Length - 1)].bounds.center;
            }
            else
            {
                int x = sample % 20, y = (sample / 20) % 16;
                point = new Vector3(Mathf.Lerp(bounds.min.x, bounds.max.x, x / 19f),
                    Mathf.Lerp(bounds.min.y, bounds.max.y, y / 15f), bounds.center.z);
            }
            bool done = controller.Apply(category, 0.05f, point);
            sample++;
            if (!done) return;
            if (category != 3 && selected.button.interactable) throw new Exception("Completed category remained clickable.");
            if (category != 3) selected.tool.ReturnHome(true);
            category++;
            sample = 0;
            waitUntil = Time.realtimeSinceStartup + 0.8f;
            if (category == categories.Length) awaitingPhoto = true;
        }
        catch (Exception exception) { Stop("FAIL: " + exception); }
    }

    private static void Stop(string result)
    {
        EditorApplication.update -= Tick;
        File.WriteAllText(ResultPath, result + "\n");
        if (result.StartsWith("PASS")) Debug.Log(result); else Debug.LogError(result);
    }
}
