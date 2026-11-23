using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Places the selected straw, lemon and umbrella at their authored glass positions.</summary>
public sealed class JuiceDecorationController : MonoBehaviour
{
    [Serializable]
    public sealed class Category
    {
        public string name;
        public Button button;
        public SpriteRenderer target;
        public Sprite[] options;
        public SlideOutObject.ExitDirection entryDirection;
        [NonSerialized] public bool complete;
        [NonSerialized] public Vector3 position;
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
    [SerializeField] private Sprite handSprite;
    [SerializeField, Min(0.1f)] private float slideDuration = 0.55f;
    private Vector3 categoryHome, optionHome;
    private RectTransform hand;
    private Coroutine optionGuide;
    private int current = -1;
    private bool ready, busy;
    public bool IsComplete { get; private set; }
    public Transform Glass => glass;
    public SpriteRenderer GlassFront => glassFront;

    private void Awake()
    {
        categoryHome = categoryPanel.localPosition;
        optionHome = optionPanel.localPosition;
        // Garnishes travel with the glass during the exit and serving gesture.
        decorationRoot.SetParent(glass, true);
        decorationRoot.gameObject.SetActive(true);
        for (int i = 0; i < categories.Length; i++)
        {
            int index = i;
            Category category = categories[i];
            category.position = category.target.transform.localPosition;
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
            const float handWidth = 80f;
            hand.sizeDelta = new Vector2(handWidth, handWidth * handSprite.rect.height / handSprite.rect.width);
            hand.pivot = new Vector2(0.05f, 0.95f);
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
        HideGuide();
        ready = busy = IsComplete = false;
        current = -1;
        foreach (Category category in categories)
        {
            category.complete = false;
            category.target.transform.DOKill();
            category.target.transform.localPosition = category.position;
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
        categoryPanel.localPosition = categoryHome + Vector3.right * offset;
        optionPanel.localPosition = optionHome + Vector3.right * offset;
        categoryPanel.DOLocalMove(categoryHome, slideDuration).SetEase(Ease.OutCubic);
        yield return optionPanel.DOLocalMove(optionHome, slideDuration).SetEase(Ease.OutCubic).WaitForCompletion();
        busy = false;
        ready = true;
        SetButtons(true);
        if (hand != null)
        {
            // The thin straw sits left of the artwork's center, just below its loop.
            var strawIcon = (RectTransform)categories[0].button.transform.GetChild(0);
            hand.position = strawIcon.TransformPoint(Rect.NormalizedToPoint(strawIcon.rect, new Vector2(0.27f, 0.63f)));
            ShowGuide();
            DOTween.Sequence().SetTarget(hand)
                .Append(TapGuide())
                .AppendInterval(0.55f)
                .SetLoops(-1);
        }
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
        bool guideWasVisible = hand != null && hand.gameObject.activeSelf;
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
        int optionCount = Mathf.Min(optionButtons.Length, categories[index].options.Length);
        if (index == 0 && hand != null && optionCount > 0)
        {
            if (!guideWasVisible)
                hand.position = ButtonCenter(optionButtons[0]);
            ShowGuide();
            optionGuide = StartCoroutine(GuideOptions(optionCount));
        }
    }

    public void SelectOption(int index)
    {
        if (!ready || busy || current < 0 || IsComplete || categories[current].complete) return;
        Category category = categories[current];
        if (index < 0 || index >= category.options.Length) return;
        busy = true;
        SetButtons(false);
        HideGuide();
        Sprite sprite = category.options[index];
        category.target.sprite = sprite;
        float fit = category.original.bounds.size.y / Mathf.Max(0.01f, sprite.bounds.size.y);
        category.target.transform.localScale = category.scale * fit;
        StartCoroutine(PlaceGarnish(category));
    }

    private IEnumerator PlaceGarnish(Category category)
    {
        Transform target = category.target.transform;
        target.gameObject.SetActive(true);
        Camera camera = Camera.main;
        Vector3 home = target.position;
        Vector3 screen = camera.WorldToScreenPoint(home);
        Bounds bounds = category.target.bounds;
        Vector3 visualMin = camera.WorldToScreenPoint(bounds.min);
        Vector3 visualMax = camera.WorldToScreenPoint(bounds.max);
        const float padding = 12f;
        switch (category.entryDirection)
        {
            case SlideOutObject.ExitDirection.Top:
                screen.y += camera.pixelRect.yMax - visualMin.y + padding;
                break;
            case SlideOutObject.ExitDirection.Right:
                screen.x += camera.pixelRect.xMax - visualMin.x + padding;
                break;
            case SlideOutObject.ExitDirection.Left:
                screen.x += camera.pixelRect.xMin - visualMax.x - padding;
                break;
        }

        target.position = camera.ScreenToWorldPoint(screen);
        yield return target.DOLocalMove(category.position, slideDuration).SetEase(Ease.OutCubic).WaitForCompletion();
        category.complete = true;
        current = -1;
        busy = false;
        foreach (Category remaining in categories)
        {
            if (remaining.complete)
                continue;

            SetButtons(true);
            yield break;
        }

        IsComplete = true;
        ready = false;
        SetButtons(false);
        panelRoot.gameObject.SetActive(false);
        mode.ConfirmToppings();
    }

    private IEnumerator GuideOptions(int optionCount)
    {
        int index = 0;
        while (true)
        {
            yield return hand.DOMove(ButtonCenter(optionButtons[index]), 0.28f)
                .SetEase(Ease.InOutSine).WaitForCompletion();
            yield return TapGuide().WaitForCompletion();
            yield return new WaitForSeconds(0.32f);
            index = (index + 1) % optionCount;
        }
    }

    private static Vector3 ButtonCenter(Button button)
    {
        var rect = (RectTransform)button.transform;
        return rect.TransformPoint(rect.rect.center);
    }

    private Tween TapGuide() => hand.DOScale(0.86f, 0.14f).SetEase(Ease.InOutSine).SetLoops(2, LoopType.Yoyo);

    private void ShowGuide()
    {
        hand.localScale = Vector3.one;
        hand.gameObject.SetActive(true);
        hand.SetAsLastSibling();
    }

    private void HideGuide()
    {
        if (optionGuide != null)
        {
            StopCoroutine(optionGuide);
            optionGuide = null;
        }
        if (hand == null) return;
        hand.DOKill();
        hand.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        categoryPanel.DOKill();
        optionPanel.DOKill();
        HideGuide();
        foreach (Category category in categories)
            category.target.transform.DOKill();
        if (hand != null) Destroy(hand.gameObject);
    }
}
