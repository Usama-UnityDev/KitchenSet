using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class CakeDecorationController : MonoBehaviour
{
    [Serializable]
    public class Category
    {
        public string name;
        public Button button;
        public CakeDecorationTool tool;
        public SpriteRenderer[] targets;
        public Sprite[] options;
        [NonSerialized] public bool complete;
        [NonSerialized] public float progress;
        [NonSerialized] public int selected = -1;
        [NonSerialized] public CakePaintMask[] painters;
        [NonSerialized] public float[] dwell;
    }

    [SerializeField] private RectTransform categoryPanel;
    [SerializeField] private RectTransform optionPanel;
    [SerializeField] private Button[] optionButtons;
    [SerializeField] private Category[] categories;
    [SerializeField] private SpriteRenderer cakeSurface;
    [SerializeField] private Shader fillShader;
    [SerializeField] private Sprite handSprite;
    [SerializeField, Min(0.1f)] private float slideDuration = 0.55f;
    [SerializeField, Min(0.05f)] private float nozzleBrushRadius = 0.48f;
    [SerializeField, Min(0.05f)] private float dollopSeconds = 0.22f;
    private Vector3 categoryHome, optionHome;
    private int current = -1;
    private bool ready, busy;
    private RectTransform hand;
    private bool tutorialComplete;
    [SerializeField] private Vector2 handFingertip = new Vector2(0.10f, 0.92f);
    public bool IsComplete { get; private set; }
    public Shader FillShader => fillShader;
    public Bounds CakeBounds => cakeSurface.bounds;

    private void Awake()
    {
        categoryHome = categoryPanel.localPosition;
        optionHome = optionPanel.localPosition;
        categoryPanel.gameObject.SetActive(false);
        optionPanel.gameObject.SetActive(false);
        for (int i = 0; i < categories.Length; i++)
        {
            int index = i;
            Category category = categories[i];
            category.button.onClick.AddListener(() => SelectCategory(index));
            category.tool.Initialize(this, index);
            category.tool.gameObject.SetActive(false);
            foreach (SpriteRenderer target in category.targets) target.gameObject.SetActive(false);
        }
        for (int i = 0; i < optionButtons.Length; i++)
        {
            int index = i;
            optionButtons[i].onClick.AddListener(() => SelectOption(index));
            optionButtons[i].interactable = false;
        }
        if (handSprite != null)
        {
            var guide = new GameObject("Decoration hand guide", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            hand = guide.GetComponent<RectTransform>();
            hand.SetParent(categoryPanel.GetComponentInParent<Canvas>().transform, false);
            hand.sizeDelta = new Vector2(90f, 90f);
            hand.pivot = handFingertip;
            Image image = guide.GetComponent<Image>();
            image.sprite = handSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            guide.SetActive(false);
        }
    }

    public void BeginDecoration()
    {
        if (!ready && !busy && !IsComplete) StartCoroutine(ShowPanels());
    }

    private IEnumerator ShowPanels()
    {
        busy = true;
        SetCategoryButtons(false);
        categoryPanel.gameObject.SetActive(true);
        optionPanel.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        Canvas canvas = categoryPanel.GetComponentInParent<Canvas>();
        float offset = Screen.width / Mathf.Max(0.01f, canvas.scaleFactor) + categoryPanel.rect.width + optionPanel.rect.width;
        categoryPanel.localPosition = categoryHome + Vector3.right * offset;
        optionPanel.localPosition = optionHome + Vector3.right * offset;
        categoryPanel.DOLocalMove(categoryHome, slideDuration).SetEase(Ease.OutCubic);
        yield return optionPanel.DOLocalMove(optionHome, slideDuration).SetEase(Ease.OutCubic).WaitForCompletion();
        busy = false;
        ready = true;
        SetCategoryButtons(true);
        GuideButton(categories[0].button);
    }

    private void Guide(Vector3 point, bool contextual = false)
    {
        if (hand == null || (tutorialComplete && !contextual)) return;
        HideGuide();
        hand.gameObject.SetActive(true);
        hand.SetAsLastSibling();
        hand.position = point;
        hand.localScale = Vector3.one;
        hand.DOScale(0.88f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
    }

    private void HideGuide()
    {
        if (hand == null) return;
        hand.DOKill();
        hand.gameObject.SetActive(false);
    }

    private void GuideNextCategory()
    {
        foreach (Category category in categories)
            if (!category.complete) { GuideButton(category.button); return; }
    }

    private void GuideButton(Button button)
    {
        RectTransform target = (RectTransform)button.transform;
        foreach (Image image in button.GetComponentsInChildren<Image>())
            if (image.transform != button.transform && image.sprite != null) { target = image.rectTransform; break; }
        Guide(target.TransformPoint(target.rect.center));
    }

    public void GuideTool(CakeDecorationTool tool)
    {
        if (hand == null || busy) return;
        Canvas canvas = hand.GetComponentInParent<Canvas>();
        Vector2 screen = Camera.main.WorldToScreenPoint(tool.GuidePoint);
        RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)hand.parent, screen,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out Vector3 point);
        Guide(point);
    }

    public void GuidePlateDrop(Vector3 item)
    {
        if (hand == null) return;
        Canvas canvas = hand.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)hand.parent,
            Camera.main.WorldToScreenPoint(item), uiCamera, out Vector3 start);
        RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)hand.parent,
            Camera.main.WorldToScreenPoint(CenterDropPoint), uiCamera, out Vector3 end);
        Guide(start, true);
        hand.DOKill();
        DOTween.Sequence().SetTarget(hand).Append(hand.DOMove(end, 1f).SetEase(Ease.InOutSine))
            .AppendInterval(0.3f).AppendCallback(() => hand.position = start).AppendInterval(0.25f).SetLoops(-1);
    }

    public Vector3 CenterDropPoint => categories[3].targets[0].transform.position;

    public bool IsCenterDrop(Vector3 point)
    {
        Bounds cake = CakeBounds;
        Vector2 delta = point - CenterDropPoint;
        return Mathf.Pow(delta.x / Mathf.Max(0.3f, cake.size.x * 0.25f), 2f) +
            Mathf.Pow(delta.y / Mathf.Max(0.3f, cake.size.y * 0.3f), 2f) <= 1f;
    }

    private void SetCategoryButtons(bool enabled)
    {
        foreach (Category category in categories) category.button.interactable = enabled && !category.complete;
    }

    private void SetOptions(bool enabled)
    {
        for (int i = 0; i < optionButtons.Length; i++)
            optionButtons[i].interactable = enabled && current >= 0 && !categories[current].complete && i < categories[current].options.Length;
    }

    public void SelectCategory(int index)
    {
        if (!ready || busy || IsComplete || index < 0 || index >= categories.Length || categories[index].complete) return;
        HideGuide();
        foreach (Category category in categories) category.tool.Hide();
        current = index;
        Category selected = categories[index];
        for (int i = 0; i < optionButtons.Length; i++)
        {
            Image icon = optionButtons[i].transform.GetChild(0).GetComponent<Image>();
            icon.sprite = i < selected.options.Length ? selected.options[i] : null;
            icon.enabled = icon.sprite != null;
            icon.preserveAspect = true;
        }
        SetOptions(true);
        GuideButton(optionButtons[0]);
    }

    public void SelectOption(int index)
    {
        if (!ready || busy || current < 0 || IsComplete) return;
        Category category = categories[current];
        if (category.complete || index < 0 || index >= category.options.Length) return;
        HideGuide();
        if (category.selected == index) { category.tool.Show(index); return; }
        DisposePainters(category);
        category.selected = index;
        category.progress = 0f;
        category.painters = new CakePaintMask[category.targets.Length];
        category.dwell = new float[category.targets.Length];
        for (int i = 0; i < category.targets.Length; i++)
        {
            SpriteRenderer target = category.targets[i];
            if (category.targets.Length > 1) target.transform.parent.gameObject.SetActive(true);
            target.sprite = category.options[index];
            target.color = Color.white;
            target.gameObject.SetActive(current != 3);
            if (current != 3) category.painters[i] = new CakePaintMask(target, fillShader);
        }
        category.tool.Show(index);
    }

    public Bounds GetApplicationBounds(int index)
    {
        if (index == 3) return CakeBounds;
        Bounds bounds = categories[index].targets[0].bounds;
        foreach (SpriteRenderer target in categories[index].targets) bounds.Encapsulate(target.bounds);
        bounds.Expand(new Vector3(0.12f, 0.12f, 1f));
        return bounds;
    }

    public void SetDragging(bool dragging)
    {
        busy = dragging;
        HideGuide();
        SetCategoryButtons(!dragging);
        SetOptions(!dragging);
        if (!dragging && current >= 0 && categories[current].painters != null)
            foreach (CakePaintMask painter in categories[current].painters) painter?.BreakStroke();
    }

    public Color SelectedCreamColor(int index)
    {
        int selected = categories[index].selected;
        if (index == 0) return selected == 0 ? new Color(0.36f, 0.12f, 0.04f) : selected == 1 ? new Color(1f, 0.37f, 0.73f) : new Color(1f, 0.95f, 0.8f);
        if (index == 1) return selected == 1 ? new Color(0.65f, 0.92f, 0.12f) : selected == 2 ? new Color(1f, 0.67f, 0.05f) : new Color(1f, 0.55f, 0.73f);
        return selected == 1 ? new Color(0.8f, 0.22f, 0.32f) : new Color(0.4f, 0.17f, 0.06f);
    }

    public bool Apply(int index, float distance, Vector3 nozzle)
    {
        Category category = categories[index];
        if (category.selected < 0 || category.complete) return false;
        if (index == 3) { StartCoroutine(DropCenter(category)); return true; }
        bool finished;
        if (category.targets.Length == 1)
        {
            finished = category.painters[0].Paint(nozzle, nozzleBrushRadius);
            category.progress = category.painters[0].Coverage;
        }
        else
        {
            int nearest = -1;
            float closest = float.MaxValue;
            for (int i = 0; i < category.targets.Length; i++)
            {
                if (category.dwell[i] >= dollopSeconds) continue;
                float d = Vector2.Distance(category.targets[i].bounds.center, nozzle);
                float reach = Mathf.Max(0.3f, category.targets[i].bounds.size.x * 0.6f);
                if (d < reach && d < closest) { nearest = i; closest = d; }
            }
            if (nearest >= 0)
            {
                category.dwell[nearest] += Time.deltaTime;
                category.painters[nearest].Paint(nozzle, nozzleBrushRadius * 0.65f);
                if (category.dwell[nearest] >= dollopSeconds)
                {
                    category.painters[nearest].Reveal();
                    category.targets[nearest].transform.DOPunchScale(Vector3.one * 0.06f, 0.18f, 1, 0.3f);
                }
            }
            int completed = 0;
            foreach (float time in category.dwell) if (time >= dollopSeconds) completed++;
            category.progress = (float)completed / category.targets.Length;
            finished = completed == category.targets.Length;
        }
        if (!finished) return false;
        foreach (CakePaintMask painter in category.painters) painter.Reveal();
        MarkComplete(category);
        return true;
    }

    private void MarkComplete(Category category)
    {
        tutorialComplete = true;
        HideGuide();
        category.complete = true;
        category.progress = 1f;
        category.button.interactable = false;
        SetOptions(false);
        GetComponent<Recipe4Controller>()?.PopCake();
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySound(SoundManager.Instance.successSparkleSound);
    }

    private IEnumerator DropCenter(Category category)
    {
        SpriteRenderer target = category.targets[0];
        SpriteRenderer source = category.tool.SelectedPlateItem;
        GameObject drop = new GameObject("Selected topping drop");
        SpriteRenderer visual = drop.AddComponent<SpriteRenderer>();
        visual.sprite = target.sprite;
        visual.sortingLayerID = target.sortingLayerID;
        visual.sortingOrder = 50;
        drop.transform.position = source != null ? source.transform.position : category.tool.transform.position;
        drop.transform.localScale = source != null ? source.transform.lossyScale : target.transform.lossyScale;
        if (source != null) source.gameObject.SetActive(false);
        drop.transform.DOScale(target.transform.lossyScale, 0.45f);
        Vector3 landing = target.transform.position;
        Vector3 start = drop.transform.position;
        yield return DOTween.To(() => 0f, t => drop.transform.position = Vector3.Lerp(start, landing, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.65f,
            1f, 0.45f).SetEase(Ease.InOutSine).WaitForCompletion();
        target.gameObject.SetActive(true);
        Destroy(drop);
        MarkComplete(category);
        category.tool.ReturnHome(true);
    }

    public void ToolReturned()
    {
        SetDragging(false);
        foreach (Category category in categories)
            if (!category.complete) { GuideNextCategory(); return; }
        IsComplete = true;
        ready = false;
        SetCategoryButtons(false);
        SetOptions(false);
        HideGuide();
        categoryPanel.gameObject.SetActive(false);
        optionPanel.gameObject.SetActive(false);
        GetComponent<Recipe4Controller>().FinishWholeRecipe();
    }

    private static void DisposePainters(Category category)
    {
        if (category.painters == null) return;
        foreach (CakePaintMask painter in category.painters) painter?.Dispose();
    }

    private void OnDestroy()
    {
        categoryPanel.DOKill();
        optionPanel.DOKill();
        if (hand != null) { hand.DOKill(); Destroy(hand.gameObject); }
        foreach (Category category in categories) DisposePainters(category);
    }
}
