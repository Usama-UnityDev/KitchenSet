using System.Collections;
using UnityEngine;

public class KnifeDraggable : MonoBehaviour
{
    [Header("Cake and bowl")]
    [SerializeField] private Collider2D cakeCollider;
    [SerializeField] private SpriteRenderer cakeLayerRenderer;
    [SerializeField] private Transform bowlCreamTransform;
    [SerializeField] private float minCreamScaleY = 0.2f;
    [SerializeField] private bool useProgressBar = true;
    [Header("Blade contact and movement")]
    [SerializeField] private Vector2 bladeContactInSprite = new Vector2(0.16f, 0.18f);
    [SerializeField, Min(0.02f)] private float followSmoothTime = 0.045f;
    [SerializeField, Min(0.1f)] private float spreadRadius = 0.6f;
    [SerializeField] private bool enableScalePunch = true;
    [SerializeField] private float punchScaleMultiplier = 1.12f;
    [SerializeField] private float scaleLerpSpeed = 15f;
    [SerializeField] private bool clampToScreen = true;
    [SerializeField] private float wallMargin = 0.05f;
    [SerializeField] private int dragSortingOrder = 15;
    private Vector3 home, homeScale, creamScale, creamPosition, grabOffset, velocity;
    private Quaternion homeRotation;
    private SpriteRenderer visual;
    private SpriteRenderer[] visuals;
    private Camera cameraRef;
    private CakePaintMask paint;
    private Coroutine returning;
    private bool dragging, finished, touching;
    private int sortingOrder;
    private Vector3 grabLocal, previousContact;
    private float spreadProgress;

    private void Awake()
    {
        cameraRef = Camera.main;
        visual = GetComponent<SpriteRenderer>();
        visuals = new[] { visual };
        sortingOrder = visual.sortingOrder;
        home = transform.localPosition;
        homeScale = transform.localScale;
        homeRotation = transform.localRotation;
        if (bowlCreamTransform != null)
        {
            creamScale = bowlCreamTransform.localScale;
            creamPosition = bowlCreamTransform.localPosition;
        }
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null) { body.bodyType = RigidbodyType2D.Kinematic; body.linearVelocity = Vector2.zero; body.angularVelocity = 0f; }
    }

    private void Start()
    {
        if (cakeLayerRenderer != null)
        {
            Animator animator = cakeLayerRenderer.GetComponent<Animator>();
            if (animator != null) animator.enabled = false;
            cakeLayerRenderer.gameObject.SetActive(true);
            cakeLayerRenderer.color = Color.white;
            var decoration = GetComponentInParent<CakeDecorationController>();
            Shader shader = decoration != null ? decoration.FillShader : Shader.Find("HUsama/DecorationFillSprite");
            paint = new CakePaintMask(cakeLayerRenderer, shader);
        }
        if (useProgressBar) SequentialAnimatorController.Instance?.HideProgressBar();
    }

    private Vector3 PointerWorld()
    {
        Vector3 screen = Input.mousePosition;
        if (Input.touchCount > 0) screen = Input.GetTouch(0).position;
        screen.z = Mathf.Abs(cameraRef.transform.position.z - transform.position.z);
        Vector3 point = cameraRef.ScreenToWorldPoint(screen);
        point.z = transform.position.z;
        return point;
    }

    private Vector3 BladeLocal
    {
        get
        {
            Bounds b = visual.sprite.bounds;
            return new Vector3(Mathf.Lerp(b.min.x, b.max.x, bladeContactInSprite.x), Mathf.Lerp(b.min.y, b.max.y, bladeContactInSprite.y), 0f);
        }
    }

    private void OnMouseDown()
    {
        if (finished || cameraRef == null) return;
        if (returning != null) { StopCoroutine(returning); returning = null; }
        visual.sortingOrder = dragSortingOrder;
        grabOffset = transform.position - PointerWorld();
        grabLocal = transform.InverseTransformPoint(PointerWorld());
        previousContact = transform.TransformPoint(BladeLocal);
        velocity = Vector3.zero;
        dragging = true;
        paint?.BreakStroke();
        SoundManager.Instance?.PlaySound(SoundManager.Instance.itemPickupSound);
    }

    private void LateUpdate()
    {
        if (!dragging || finished) return;
        if (Input.GetMouseButtonUp(0) || (Input.touchCount == 0 && !Input.GetMouseButton(0))) { ReleaseKnife(); return; }
        float blend = 1f - Mathf.Exp(-scaleLerpSpeed * Time.deltaTime);
        transform.localScale = Vector3.Lerp(transform.localScale, homeScale * (enableScalePunch ? punchScaleMultiplier : 1f), blend);
        transform.localRotation = homeRotation;
        Vector3 old = transform.position;
        // Keep the exact grabbed point under the finger through the pickup scale change.
        transform.position = PointerWorld() - transform.TransformVector(grabLocal);
        if (clampToScreen) CakeToolScreenBounds.Constrain(transform, cameraRef, visuals, 0.002f);
        Vector3 contact = transform.TransformPoint(BladeLocal);
        float strokeDistance = Vector3.Distance(previousContact, contact);
        previousContact = contact;
        bool contactNow = cakeCollider != null && cakeCollider.OverlapPoint(contact);
        if (contactNow != touching && useProgressBar)
        {
            if (contactNow) SequentialAnimatorController.Instance?.ShowProgressBar(paint != null ? paint.Coverage : 0f);
            else SequentialAnimatorController.Instance?.HideProgressBar();
        }
        touching = contactNow;
        if (!touching) { paint?.BreakStroke(); return; }
        if (paint == null || Vector3.Distance(old, transform.position) < 0.001f) return;
        spreadProgress = Mathf.Clamp01(spreadProgress + Mathf.Min(strokeDistance, 0.35f) / Mathf.Max(1f, cakeCollider.bounds.size.x * 3f));
        bool done = paint.SpreadFromCenter(spreadProgress);
        float progress = paint.Coverage;
        if (useProgressBar) SequentialAnimatorController.Instance?.UpdateProgressBar(progress);
        UpdateBowl(progress);
        if (!done) return;
        finished = true;
        dragging = false;
        paint.Reveal();
        UpdateBowl(1f);
        SequentialAnimatorController.Instance?.HideProgressBar();
        returning = StartCoroutine(ReturnToBowl(true));
    }

    private void UpdateBowl(float progress)
    {
        if (bowlCreamTransform == null) return;
        bowlCreamTransform.localScale = new Vector3(Mathf.Lerp(creamScale.x, creamScale.x * 0.85f, progress),
            Mathf.Lerp(creamScale.y, creamScale.y * minCreamScaleY, progress), creamScale.z);
        bowlCreamTransform.localPosition = creamPosition + Vector3.down * (0.15f * progress);
    }

    private void OnMouseUp() { ReleaseKnife(); }
    private void ReleaseKnife()
    {
        if (!dragging) return;
        dragging = touching = false;
        paint?.BreakStroke();
        SequentialAnimatorController.Instance?.HideProgressBar();
        SoundManager.Instance?.PlaySound(SoundManager.Instance.itemReleaseSound);
        returning = StartCoroutine(ReturnToBowl(false));
    }

    private IEnumerator ReturnToBowl(bool notify)
    {
        Vector3 start = transform.localPosition, size = transform.localScale;
        Quaternion rotation = transform.localRotation;
        float duration = 0.42f;
        for (float time = 0f; time < duration; time += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, time / duration);
            transform.localPosition = Vector3.Lerp(start, home, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.35f);
            transform.localRotation = Quaternion.Slerp(rotation, homeRotation, t);
            transform.localScale = Vector3.Lerp(size, homeScale, t);
            if (clampToScreen) CakeToolScreenBounds.Constrain(transform, cameraRef, visuals, wallMargin);
            yield return null;
        }
        transform.localPosition = home;
        transform.localRotation = homeRotation;
        transform.localScale = homeScale;
        if (clampToScreen) CakeToolScreenBounds.Constrain(transform, cameraRef, visuals, wallMargin);
        visual.sortingOrder = sortingOrder;
        returning = null;
        if (notify) Recipe4Controller.Instance?.OnFrostingFinished();
    }

    private void OnApplicationFocus(bool focus) { if (!focus) ReleaseKnife(); }
    private void OnDestroy() { paint?.Dispose(); }
}
