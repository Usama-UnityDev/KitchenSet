using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Three independently selectable garnishes; completion requires an actual drop of each.</summary>
public sealed class JuiceDecorationController : MonoBehaviour
{
    [Serializable]
    public sealed class Category
    {
        public string name;
        public Button button;
        public SpriteRenderer target;
        public Sprite[] options;
        [NonSerialized] public bool complete;
        [NonSerialized] public Vector3 scale;
        [NonSerialized] public Sprite original;
    }

    [SerializeField] private JuiceModeController mode;
    [SerializeField] private Transform glass;
    [SerializeField] private SpriteRenderer glassFront;
    [SerializeField] private Transform decorationRoot;
    [SerializeField] private RectTransform panelRoot, categoryPanel, optionPanel;
    [SerializeField] private Button[] optionButtons;
    [SerializeField] private Category[] categories;
    [SerializeField] private DragToPour2D pour;
    [SerializeField] private Sprite handSprite;
    [SerializeField, Min(0.1f)] private float slideDuration = 0.55f;
    private Vector3 categoryHome, optionHome;
    private JuiceDecorationItem item;
    private RectTransform hand;
    private int current = -1;
    private bool ready, busy;
    public bool IsComplete { get; private set; }
    public Transform Glass => glass;

    private void Awake()
    {
        categoryHome = categoryPanel.localPosition;
        optionHome = optionPanel.localPosition;
        // Garnishes travel with the glass during the final pop, exit and serving gesture.
        decorationRoot.SetParent(glass, true);
        decorationRoot.gameObject.SetActive(true);
        for (int i = 0; i < categories.Length; i++)
        {
            int index = i;
            Category category = categories[i];
            category.scale = category.target.transform.localScale;
            category.original = category.target.sprite;
            category.button.onClick.AddListener(() => SelectCategory(index));
        }
        for (int i = 0; i < optionButtons.Length; i++)
        {
            int index = i;
            optionButtons[i].onClick.AddListener(() => SelectOption(index));
        }
        if (handSprite != null)
        {
            var guide = new GameObject("Juice decoration hand", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            hand = guide.GetComponent<RectTransform>();
            hand.SetParent(panelRoot.parent, false);
            hand.sizeDelta = new Vector2(80f, 80f);
            hand.pivot = new Vector2(0.1f, 0.92f);
            Image image = guide.GetComponent<Image>();
            image.sprite = handSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }
        ResetDecoration();
    }

    public void ResetDecoration()
    {
        StopAllCoroutines();
        categoryPanel.DOKill();
        optionPanel.DOKill();
        categoryPanel.localPosition = categoryHome;
        optionPanel.localPosition = optionHome;
        ClearItem();
        HideGuide();
        ready = busy = IsComplete = false;
        current = -1;
        foreach (Category category in categories)
        {
            category.complete = false;
            category.target.gameObject.SetActive(false);
            category.target.sprite = category.original;
            category.target.transform.localScale = category.scale;
        }
        panelRoot.gameObject.SetActive(false);
    }

    public void BeginDecoration()
    {
        if (!ready && !busy && !IsComplete) StartCoroutine(ShowPanels());
    }

    private IEnumerator ShowPanels()
    {
        busy = true;
        panelRoot.gameObject.SetActive(true);
        categoryPanel.gameObject.SetActive(true);
        optionPanel.gameObject.SetActive(true);
        SetButtons(false);
        Canvas.ForceUpdateCanvases();
        Canvas canvas = panelRoot.GetComponentInParent<Canvas>();
        float offset = Screen.width / Mathf.Max(0.01f, canvas.scaleFactor) + optionPanel.rect.width + 250f;
        categoryPanel.localPosition = categoryHome + Vector3.left * offset;
        optionPanel.localPosition = optionHome + Vector3.left * offset;
        categoryPanel.DOLocalMove(categoryHome, slideDuration).SetEase(Ease.OutCubic);
        yield return optionPanel.DOLocalMove(optionHome, slideDuration).SetEase(Ease.OutCubic).WaitForCompletion();
        busy = false;
        ready = true;
        SetButtons(true);
        Guide(categories[0].button.transform.position);
    }

    private void SetButtons(bool enabled)
    {
        foreach (Category category in categories)
            category.button.interactable = enabled && !category.complete;
        for (int i = 0; i < optionButtons.Length; i++)
            optionButtons[i].interactable = enabled && current >= 0 && !categories[current].complete && i < categories[current].options.Length;
    }

    public void SelectCategory(int index)
    {
        if (!ready || busy || IsComplete || index < 0 || index >= categories.Length || categories[index].complete) return;
        ClearItem();
        HideGuide();
        current = index;
        for (int i = 0; i < optionButtons.Length; i++)
        {
            Image icon = optionButtons[i].transform.GetChild(0).GetComponent<Image>();
            icon.sprite = i < categories[index].options.Length ? categories[index].options[i] : null;
            icon.enabled = icon.sprite != null;
            icon.preserveAspect = true;
        }
        SetButtons(true);
        Guide(optionButtons[0].transform.position);
    }

    public void SelectOption(int index)
    {
        if (!ready || busy || current < 0 || IsComplete || categories[current].complete) return;
        Category category = categories[current];
        if (index < 0 || index >= category.options.Length) return;
        ClearItem();
        HideGuide();
        Sprite sprite = category.options[index];
        category.target.sprite = sprite;
        float fit = category.original.bounds.size.y / Mathf.Max(0.01f, sprite.bounds.size.y);
        category.target.transform.localScale = category.scale * fit;
        var tool = new GameObject("Drag " + category.name);
        SpriteRenderer visual = tool.AddComponent<SpriteRenderer>();
        visual.sprite = sprite;
        visual.sortingLayerID = glassFront.sortingLayerID;
        visual.sortingOrder = 50;
        tool.transform.localScale = category.target.transform.lossyScale;
        Camera camera = Camera.main;
        tool.transform.position = camera.ViewportToWorldPoint(new Vector3(0.30f, 0.53f, camera.WorldToScreenPoint(glass.position).z));
        tool.AddComponent<BoxCollider2D>();
        item = tool.AddComponent<JuiceDecorationItem>();
        item.Initialize(this, visual);
        GuideItem();
    }

    public void SetDragging(bool dragging)
    {
        busy = dragging;
        SetButtons(!dragging);
        HideGuide();
    }

    public bool TryDrop(JuiceDecorationItem source, Vector3 point)
    {
        if (source != item || current < 0 || !ready || IsComplete || categories[current].complete) return false;
        Bounds bounds = CakeToolScreenBounds.VisibleBounds(glassFront);
        // Accept the glass bowl and rim, including the authored straw/umbrella landing points.
        bounds.Encapsulate(categories[current].target.transform.position);
        bounds.Expand(new Vector3(0.65f, 0.7f, 1f));
        if (point.x < bounds.min.x || point.x > bounds.max.x || point.y < bounds.min.y || point.y > bounds.max.y) return false;
        busy = true;
        SetButtons(false);
        StartCoroutine(LandItem(categories[current]));
        return true;
    }

    private IEnumerator LandItem(Category category)
    {
        Transform drop = item.transform;
        Vector3 start = drop.position, landing = category.target.transform.position;
        drop.DOScale(category.target.transform.lossyScale, 0.42f);
        yield return DOTween.To(() => 0f, t => drop.position = Vector3.Lerp(start, landing, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.45f,
            1f, 0.42f).SetEase(Ease.InOutSine).WaitForCompletion();
        category.target.gameObject.SetActive(true);
        category.complete = true;
        ClearItem();
        busy = false;
        current = -1;
        SetButtons(true);
        foreach (Category remaining in categories)
        {
            if (!remaining.complete) { Guide(remaining.button.transform.position); yield break; }
        }
        IsComplete = true;
        ready = false;
        SetButtons(false);
        HideGuide();
        panelRoot.gameObject.SetActive(false);
        // No glass pop or win transition is allowed before all three drops have settled.
        yield return pour.PlayFinalGlassCompletion();
        mode.ConfirmToppings();
    }

    public void GuideItem()
    {
        if (item == null || hand == null) return;
        Canvas canvas = hand.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransform parent = (RectTransform)hand.parent;
        RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, Camera.main.WorldToScreenPoint(item.transform.position), uiCamera, out Vector3 start);
        RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, Camera.main.WorldToScreenPoint(glassFront.bounds.center), uiCamera, out Vector3 end);
        Guide(start);
        hand.DOKill();
        DOTween.Sequence().SetTarget(hand).Append(hand.DOMove(end, 1f).SetEase(Ease.InOutSine))
            .AppendInterval(0.25f).AppendCallback(() => hand.position = start).AppendInterval(0.2f).SetLoops(-1);
    }

    private void Guide(Vector3 point)
    {
        if (hand == null) return;
        HideGuide();
        hand.position = point;
        hand.localScale = Vector3.one;
        hand.gameObject.SetActive(true);
        hand.SetAsLastSibling();
        hand.DOScale(0.88f, 0.5f).SetLoops(-1, LoopType.Yoyo);
    }

    private void HideGuide()
    {
        if (hand == null) return;
        hand.DOKill();
        hand.gameObject.SetActive(false);
    }

    private void ClearItem()
    {
        if (item == null) return;
        item.transform.DOKill();
        Destroy(item.gameObject);
        item = null;
    }

    private void OnDestroy()
    {
        categoryPanel.DOKill();
        optionPanel.DOKill();
        ClearItem();
        HideGuide();
        if (hand != null) Destroy(hand.gameObject);
    }
}
