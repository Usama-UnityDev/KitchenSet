using System.Collections;
using UnityEngine;
using DG.Tweening;

public class Recipe4Controller : MonoBehaviour
{
    public static Recipe4Controller Instance { get; private set; }

    [Header("--- Base Cake & Bowl References ---")]
    [SerializeField] private GameObject creamBowlParent;
    [SerializeField] private SlideOutObject creamBowlSlideOut;
    [SerializeField] private Transform completedCakeTransform;
    [SerializeField] private SlideOutObject cakeSlideOut;
    [SerializeField] private Transform cakeItems;
    [SerializeField] private CakeFinalPresentation finalPresentation;

    [Header("--- Polish & Juice Settings ---")]
    [SerializeField] private float cakePopScaleMultiplier = 1.18f;

    //[Header("--- Generic Decoration Panel Settings ---")]
    //[Tooltip("Tick karein agar cake frosting ke baad side decoration panel khulwana ho (Recipe 1 ki tarah toggleable)")]
    //public bool requireDecorationPanel = true;

    //[Tooltip("Side par aane wala decoration selection panel")]
    //public GameObject decorationPanel;

    //[Tooltip("Interactive Cream Bag / Piping Cone Tool")]
    //public CreamBagDraggable creamBagTool;

    //[Tooltip("Generic Topping Dropper (Strawberries, Sprinkles, Clusters)")]
    //public CakeToppingDropper toppingDropper;

    private Vector3 cakeOriginalScale;
    private bool frostingFinished;
    private bool recipeFinished;
    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (completedCakeTransform != null)
        {
            cakeOriginalScale = completedCakeTransform.localScale;
        }

        // Shuru mein decoration panel aur piping bag hide rahein
        //if (decorationPanel != null)
        //    decorationPanel.SetActive(false);

        //if (creamBagTool != null)
        //    creamBagTool.gameObject.SetActive(false);
    }

    public void OnFrostingFinished()
    {
        if (frostingFinished) return;
        frostingFinished = true;
        StartCoroutine(CompleteStageFlowRoutine());
    }

    public void PopCake()
    {
        if (completedCakeTransform == null) return;
        completedCakeTransform.DOKill();
        completedCakeTransform.localScale = cakeOriginalScale;
        DOTween.Sequence().Append(completedCakeTransform.DOScale(cakeOriginalScale * 1.065f, 0.16f).SetEase(Ease.OutSine))
            .Append(completedCakeTransform.DOScale(cakeOriginalScale, 0.24f).SetEase(Ease.OutBack));
    }

    private IEnumerator CompleteStageFlowRoutine()
    {
        // 1. Thoda wait taake knife bowl mein wapis settle ho jaye
        yield return new WaitForSeconds(0.6f);

        // 2. Cream Bowl screen se bahar slide out ho jaye
        if (creamBowlSlideOut != null)
        {
            creamBowlSlideOut.enabled = true;
            yield return new WaitForSeconds(creamBowlSlideOut.duration > 0f ? creamBowlSlideOut.duration : 0.8f);
        }

        // 3. Sparkles cake ke upar play karein
        if (SequentialAnimatorController.Instance != null && completedCakeTransform != null)
        {
            ParticleSystem[] particles = SequentialAnimatorController.Instance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in particles)
            {
                if (ps != null && (ps.name.ToLower().Contains("complete") || ps.name.ToLower().Contains("sparkle")))
                {
                    ps.transform.position = completedCakeTransform.position + new Vector3(0f, 0.4f, 0f);
                }
            }
        }

        // 4. Cake Soft Pop Bounce
        if (completedCakeTransform != null)
        {
            completedCakeTransform.DOKill();
            Sequence popSeq = DOTween.Sequence();
            popSeq.Append(completedCakeTransform.DOScale(cakeOriginalScale * cakePopScaleMultiplier, 0.32f).SetEase(Ease.OutSine));
            popSeq.Append(completedCakeTransform.DOScale(cakeOriginalScale, 0.38f).SetEase(Ease.OutBack));
            popSeq.AppendInterval(0.35f);

            yield return popSeq.WaitForCompletion();
            completedCakeTransform.localScale = cakeOriginalScale;
        }

        GetComponent<CakeDecorationController>()?.BeginDecoration();

        //// 5. Agar Decoration Panel required hai toh open karein
        //if (requireDecorationPanel && decorationPanel != null)
        //{
        //    decorationPanel.SetActive(true);

        //    // Piping tool complete hone par callback
        //    if (creamBagTool != null)
        //    {
        //        creamBagTool.OnCompleted = () =>
        //        {
        //            // Piping complete hone ke baad bhi panel open rehta hai taake toppings select ho sakein
        //        };
        //    }
        //}
        //else
        //{
        //    // Agar Decoration Panel uncheck ho toh direct task complete
        //    FinishWholeRecipe();
        //}
    }

    #region UI Panel Button Hookups

    /// <summary>
    /// Panel se Cream Bag button click karne par piping bag screen mein cake ke paas enter hogi
    /// </summary>
    public void OnClickCreamBagButton()
    {
        //if (creamBagTool != null)
        //{
        //    creamBagTool.EnterScreen();
        //}
    }

    /// <summary>
    /// Panel se kisi bhi topping button (e.g. "strawberry", "sprinkles", "topping1") par click hone par
    /// </summary>
    public void OnClickToppingButton(string toppingId)
    {
        //if (toppingDropper != null)
        //{
        //    toppingDropper.DropTopping(toppingId);
        //}
    }

    /// <summary>
    /// Poori decoration khatam hone par final Next button se call karein
    /// </summary>
    public void FinishWholeRecipe()
    {
        if (recipeFinished) return;
        var decoration = GetComponent<CakeDecorationController>();
        if (decoration != null && !decoration.IsComplete) return;
        recipeFinished = true;
        if (finalPresentation != null)
        {
            StartCoroutine(finalPresentation.PrepareAndPresent(completedCakeTransform, cakeItems, cakeSlideOut));
            return;
        }
        //if (decorationPanel != null)
        //    decorationPanel.SetActive(false);

        if (SequentialAnimatorController.Instance != null)
        {
            SequentialAnimatorController.Instance.UpdateTask();
        }
    }

    #endregion

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
