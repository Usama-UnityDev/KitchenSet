using System.Collections;
using UnityEngine;

public class BakePanDraggable : MonoBehaviour
{
    [Header("--- Target & Snapping ---")]
    [SerializeField] private Transform ovenTargetPoint;
    [SerializeField] private float snapDistance = 1.6f;
    [SerializeField] private string panIntoOvenClip = "CakeBowl";

    [Header("--- Scale Punch Settings (Pop Effect) ---")]
    [SerializeField] private bool enableScalePunch = true;
    [SerializeField] private float punchScaleMultiplier = 1.2f;
    [SerializeField] private float scaleTransitionSpeed = 15f;

    [Header("--- Screen Boundary Settings ---")]
    [SerializeField] private bool clampToScreen = true;
    [SerializeField] private float extraMargin = 0.15f;

    [Header("--- Smooth Return Settings ---")]
    [SerializeField] private float returnSpeed = 22f;

    [Header("--- Hand Guide ---")]
    [SerializeField] private GameObject pointingHand;

    // Public properties taake Recipe3Controller inhein read kar sake
    public Vector3 OriginalTablePosition => originalTablePosition;
    public Vector3 OriginalScale => originalScale;

    private Vector3 originalTablePosition;
    private bool isOriginSaved = false;

    private Vector3 originalScale;
    private Vector3 punchScale;
    private bool isDragging = false;
    private bool isPlaced = false;
    private bool isWaitingForTakeOut = false;

    private Camera mainCamera;
    private Vector3 offset;
    private Collider2D panCollider;
    private Animator panAnimator;

    private Coroutine returnCoroutine;
    private Coroutine scaleCoroutine;

    private void Awake()
    {
        mainCamera = Camera.main;
        panCollider = GetComponent<Collider2D>();
        panAnimator = GetComponent<Animator>();

        originalScale = transform.localScale;
        punchScale = originalScale * punchScaleMultiplier;
    }

    private void Start()
    {
        if (panAnimator != null)
        {
            panAnimator.enabled = false;
        }
    }

    private void Update()
    {
        // FIX: Jab tak pan oven mein snap nahi hota, animator band rahega
        // Chahe SequentialController isay on bhi kare, yeh foran off ho jayega
        if (!isPlaced && panAnimator != null && panAnimator.enabled)
        {
            panAnimator.enabled = false;
        }

        if (isDragging && Input.GetMouseButtonUp(0))
        {
            ReleasePan();
        }

        // Baked cake par tap detect karna
        if (isWaitingForTakeOut && Input.GetMouseButtonDown(0))
        {
            Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            Collider2D hit = Physics2D.OverlapPoint(mousePos);
            if (hit != null && (hit.transform == transform || hit.transform.IsChildOf(transform)))
            {
                TriggerTakeOut();
            }
        }
    }

    // Oven se cake nikalne ke liye mode on karna
    public void EnableTakeOutMode()
    {
        isPlaced = false;
        isDragging = false;
        isWaitingForTakeOut = true;
        if (panCollider != null) panCollider.enabled = true;
    }

    public void TriggerTakeOut()
    {
        if (!isWaitingForTakeOut) return;
        isWaitingForTakeOut = false;

        if (panCollider != null) panCollider.enabled = false;
        SoundManager.Instance?.PlaySound(SoundManager.Instance.itemPickupSound);

        if (Recipe3Controller.Instance != null)
        {
            Recipe3Controller.Instance.OnCakePanTappedToExit();
        }
    }

    private void OnMouseDown()
    {
        if (isWaitingForTakeOut)
        {
            TriggerTakeOut();
            return;
        }

        // Agar oven door abhi nahi khula to drag start na ho
        if (Recipe3Controller.Instance != null && !Recipe3Controller.Instance.isDoorOpened)
            return;

        if (isPlaced) return;

        //// FIX: Dragging ke dauran Animator lazmi disable hona chahiye taake transform lock na ho
        //if (panAnimator != null)
        //{
        //    panAnimator.enabled = false;
        //}

        // Table par pan ki position capture karein
        if (!isOriginSaved)
        {
            originalTablePosition = transform.position;
            isOriginSaved = true;
        }

        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
            returnCoroutine = null;
        }

        //if (pointingHand != null) pointingHand.SetActive(false);

        SoundManager.Instance?.PlaySound(SoundManager.Instance.itemPickupSound);

        Vector3 mouseWorldPos = GetMouseWorldPosition();
        offset = transform.position - mouseWorldPos;
        isDragging = true;

        if (enableScalePunch)
        {
            StartVisualScale(punchScale);
        }
    }

    private void OnMouseDrag()
    {
        if (!isDragging || isPlaced || isWaitingForTakeOut) return;

        Vector3 mouseWorldPos = GetMouseWorldPosition();
        Vector3 targetPos = mouseWorldPos + offset;

        if (clampToScreen && mainCamera != null)
        {
            float horizontalPadding = 0.6f;
            float verticalPadding = 0.6f;

            Bounds combinedBounds = GetCombinedChildBounds();
            if (combinedBounds.size != Vector3.zero)
            {
                horizontalPadding = combinedBounds.extents.x + extraMargin;
                verticalPadding = combinedBounds.extents.y + extraMargin;
            }

            float camDistance = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
            Vector3 bottomLeft = mainCamera.ViewportToWorldPoint(new Vector3(0, 0, camDistance));
            Vector3 topRight = mainCamera.ViewportToWorldPoint(new Vector3(1, 1, camDistance));

            targetPos.x = Mathf.Clamp(targetPos.x, bottomLeft.x + horizontalPadding, topRight.x - horizontalPadding);
            targetPos.y = Mathf.Clamp(targetPos.y, bottomLeft.y + verticalPadding, topRight.y - verticalPadding);
        }

        transform.position = new Vector3(targetPos.x, targetPos.y, originalTablePosition.z);

        if (ovenTargetPoint != null && Vector2.Distance(transform.position, ovenTargetPoint.position) <= snapDistance)
        {
            SnapIntoOven();
        }
    }

    private void OnMouseUp()
    {
        ReleasePan();
    }

    private void ReleasePan()
    {
        if (!isDragging || isPlaced || isWaitingForTakeOut) return;
        isDragging = false;

        if (enableScalePunch)
        {
            StartVisualScale(originalScale);
        }

        SoundManager.Instance?.PlaySound(SoundManager.Instance.itemReleaseSound);

        if (returnCoroutine != null) StopCoroutine(returnCoroutine);
        returnCoroutine = StartCoroutine(ReturnToInitialPositionRoutine());
    }

    private IEnumerator ReturnToInitialPositionRoutine()
    {
        while (Vector3.Distance(transform.position, originalTablePosition) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                originalTablePosition,
                returnSpeed * Time.deltaTime
            );
            yield return null;
        }

        transform.position = originalTablePosition;
        returnCoroutine = null;

        if (pointingHand != null) pointingHand.SetActive(true);
    }

    private void SnapIntoOven()
    {
        isPlaced = true;
        isDragging = false;

        if (returnCoroutine != null) StopCoroutine(returnCoroutine);
        if (panCollider != null) panCollider.enabled = false;
        if (pointingHand != null) pointingHand.SetActive(false);

        if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
        transform.localScale = originalScale;

        SoundManager.Instance?.PlaySound(SoundManager.Instance.itemPickupSound);

        if (panAnimator != null)
        {
            panAnimator.enabled = true;
            if (!string.IsNullOrEmpty(panIntoOvenClip))
            {
                panAnimator.Play(panIntoOvenClip, 0, 0f);
            }
        }
        else if (ovenTargetPoint != null)
        {
            transform.position = ovenTargetPoint.position;
        }

        if (Recipe3Controller.Instance != null)
        {
            Recipe3Controller.Instance.OnBakePanEnteredOven();
        }
    }

    private Bounds GetCombinedChildBounds()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        if (renderers.Length == 0) return new Bounds(transform.position, Vector3.zero);

        Bounds combined = new Bounds(transform.position, Vector3.zero);
        bool hasFirst = false;

        foreach (var r in renderers)
        {
            if (r.enabled && r.gameObject.activeInHierarchy)
            {
                if (!hasFirst)
                {
                    combined = r.bounds;
                    hasFirst = true;
                }
                else
                {
                    combined.Encapsulate(r.bounds);
                }
            }
        }
        return combined;
    }

    private void StartVisualScale(Vector3 targetScale)
    {
        if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
        scaleCoroutine = StartCoroutine(AnimateScaleRoutine(targetScale));
    }

    private IEnumerator AnimateScaleRoutine(Vector3 targetScale)
    {
        while (Vector3.Distance(transform.localScale, targetScale) > 0.01f)
        {
            transform.localScale = Vector3.Lerp(
                transform.localScale,
                targetScale,
                scaleTransitionSpeed * Time.deltaTime
            );
            yield return null;
        }

        transform.localScale = targetScale;
        scaleCoroutine = null;
    }

    private Vector3 GetMouseWorldPosition()
    {
        float camDistance = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = camDistance;
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(mouseScreenPos);
        worldPos.z = transform.position.z;
        return worldPos;
    }

    public void DisableAnimatorCompletely()
    {
        if (panAnimator != null)
        {
            panAnimator.enabled = false;
            panAnimator.runtimeAnimatorController = null; // Controller detach kar diya taake glitch na aaye
        }
    }
}