using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public sealed class JuiceModeController : MonoBehaviour
{
    [Tooltip("Recipe roots in build play order. Each root must be a separate child of Recipes.")]
    [SerializeField] private GameObject[] recipes = Array.Empty<GameObject>();
    [Tooltip("Editor and development builds only. -1 uses the full sequence. 0 previews the first recipe.")]
    [SerializeField, Min(-1)] private int previewRecipeIndex = -1;

    [Header("Recipe Loading")]
    [SerializeField] private GameObject loadingPanel;
    [Tooltip("Default seconds after a recipe exits before showing Loading.")]
    [SerializeField, Min(0f)] private float loadingDelay;
    [Tooltip("Default seconds to show Loading before continuing.")]
    [SerializeField, Min(0f)] private float loadingDuration = 0.65f;
    [Tooltip("Override Loading delay and duration after the assigned recipe exits.")]
    [SerializeField] private RecipeLoadingTiming[] recipeLoadingTimings = Array.Empty<RecipeLoadingTiming>();

    [Header("Final Recipe")]
    [SerializeField] private GameObject toppingSelectionPanel;
    [SerializeField] private GameObject winPanel;
    [SerializeField] private UnityEvent<int> toppingSelected;

    [Serializable]
    private struct RecipeLoadingTiming
    {
        public GameObject recipe;
        [Min(0f)] public float delay;
        [Min(0f)] public float duration;
    }

    private enum Phase { Playing, Toppings, Exiting, Loading, Won, PreviewComplete }
    private Phase phase;
    private int recipeIndex = -1;
    private bool previewing;
    private SlideOutObject exitAnimation;
    private Coroutine loadingTransition;

    private void Start()
    {
        RestartSequence();
    }

    public void RestartSequence()
    {
        ClearExitAnimation();
        ClearLoading();
        recipeIndex = -1;
        Hide(toppingSelectionPanel);
        Hide(winPanel);
        foreach (GameObject recipe in recipes)
            Hide(recipe);

        previewing = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        previewing = previewRecipeIndex >= 0 && previewRecipeIndex < recipes.Length &&
            recipes[previewRecipeIndex] != null;
        if (previewing)
        {
            ActivateRecipe(previewRecipeIndex);
            return;
        }
#endif
        int first = NextRecipeIndex(-1);
        if (first >= 0)
            ActivateRecipe(first);
    }

    // Each recipe reports completion with its own root.
    public void CompleteRecipe(GameObject recipe)
    {
        if (phase != Phase.Playing || recipeIndex < 0 || recipes[recipeIndex] != recipe)
            return;

        if (recipe.activeInHierarchy && recipe.TryGetComponent(out SlideOutObject slideOut))
        {
            phase = Phase.Exiting;
            exitAnimation = slideOut;
            exitAnimation.Completed += OnRecipeExited;
            exitAnimation.enabled = true;
            return;
        }

        BeginLoading();
    }

    private void OnRecipeExited()
    {
        ClearExitAnimation();
        BeginLoading();
    }

    private void OnDisable()
    {
        ClearExitAnimation();
        ClearLoading();
    }

    private void ClearExitAnimation()
    {
        if (exitAnimation == null)
            return;

        exitAnimation.Completed -= OnRecipeExited;
        exitAnimation.enabled = false;
        exitAnimation = null;
    }

    private void AdvanceRecipe()
    {
        if (previewing)
        {
            phase = Phase.PreviewComplete;
            return;
        }

        int next = NextRecipeIndex(recipeIndex);
        if (next >= 0)
        {
            GameObject recipe = recipes[recipeIndex];
            recipeIndex = -1;
            recipe.SetActive(false);
            ActivateRecipe(next);
            return;
        }

        phase = Phase.Won;
        if (winPanel != null)
            winPanel.SetActive(true);
    }

    private void BeginLoading()
    {
        if (loadingPanel == null)
        {
            AdvanceRecipe();
            return;
        }

        float delay = loadingDelay;
        float duration = loadingDuration;
        foreach (RecipeLoadingTiming timing in recipeLoadingTimings)
        {
            if (timing.recipe != recipes[recipeIndex])
                continue;

            delay = timing.delay;
            duration = timing.duration;
            break;
        }

        phase = Phase.Loading;
        loadingTransition = StartCoroutine(ShowLoading(delay, duration));
    }

    private IEnumerator ShowLoading(float delay, float duration)
    {
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        loadingPanel.SetActive(true);
        if (duration > 0f)
            yield return new WaitForSecondsRealtime(duration);
        else
            yield return null;

        loadingTransition = null;
        Hide(loadingPanel);
        AdvanceRecipe();
    }

    private void ClearLoading()
    {
        if (loadingTransition != null)
            StopCoroutine(loadingTransition);
        loadingTransition = null;
        Hide(loadingPanel);
    }

    // The final recipe calls this when its drink is ready for optional decoration.
    public void OpenFinalToppings(GameObject recipe)
    {
        if (phase != Phase.Playing || recipeIndex < 0 || recipes[recipeIndex] != recipe ||
            NextRecipeIndex(recipeIndex) >= 0)
            return;

        phase = Phase.Toppings;
        if (toppingSelectionPanel != null)
            toppingSelectionPanel.SetActive(true);
        else
            ConfirmToppings();
    }

    public void SelectTopping(int toppingIndex)
    {
        if (phase == Phase.Toppings)
            toppingSelected?.Invoke(toppingIndex);
    }

    public void ConfirmToppings()
    {
        if (phase != Phase.Toppings)
            return;

        Hide(toppingSelectionPanel);
        phase = Phase.Playing;
        CompleteRecipe(recipes[recipeIndex]);
    }

    private void ActivateRecipe(int index)
    {
        recipeIndex = index;
        phase = Phase.Playing;
        recipes[index].SetActive(true);
    }

    private int NextRecipeIndex(int after)
    {
        for (int index = after + 1; index < recipes.Length; index++)
        {
            if (recipes[index] != null)
                return index;
        }

        return -1;
    }

    private static void Hide(GameObject panel)
    {
        if (panel != null)
            panel.SetActive(false);
    }
}
