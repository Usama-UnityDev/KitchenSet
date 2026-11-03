using System.Collections;
using UnityEngine;

public class KnifeDraggable : MonoBehaviour
{
    [Header("--- Target Collider (Cake) ---")]
    [Tooltip("Cake ka trigger collider (cake backed)")]
    [SerializeField] private Collider2D cakeCollider;
    [SerializeField] private string cakeTag = "Cake";

    [Header("--- Cake Frosting (Alpha Fill) ---")]
    [Tooltip("cakeLayer GameObject par laga SpriteRenderer")]
    [SerializeField] private SpriteRenderer cakeLayerRenderer;
    [Tooltip("Kitna total drag distance chahiye cake complete karne ke liye (Reference standard: 14 to 18)")]
    [SerializeField] private float dragDistanceToComplete = 16f;

    [Header("--- Bowl Cream Level Decrease ---")]
    [Tooltip("creamBowl ke andar mojood 'cream' GameObject ka Transform")]
    [SerializeField] private Transform bowlCreamTransform;
    [Tooltip("Cream 100% complete hone par kitni shrink hogi (Y scale multiplier)")]
    [SerializeField] private float minCreamScaleY = 0.2f;

    [Header("--- Bottom Progress Bar ---")]
    [Tooltip("Kya drag shuru hone par bottom progress bar show karni hai")]
    [SerializeField] private bool useProgressBar = true;

    [Header("--- Visual Scale Punch (20%) ---")]
    [SerializeField] private bool enableScalePunch = true;
    [SerializeField] private float punchScaleMultiplier = 1.2f;
    [SerializeField] private float scaleLerpSpeed = 15f;

    [Header("--- Screen Boundary Settings ---")]
    [SerializeField] private bool clampToScreen = true;
    [Tooltip("Wall ke bilkul sath chipakne ke liye margin (0 = touch wall)")]
    [SerializeField] private float wallMargin = 0.05f;
    [Tooltip("Sprite ke transparent padding ko compensate karne ke liye tightness (0.8 to 0.9)")]
    [SerializeField, Range(0.5f, 1f)] private float clampTightness = 0.85f;

    [Header("--- Return Settings ---")]
    [SerializeField] private float returnSpeed = 22f;

    [Header("--- Sorting Order During Drag ---")]
    [SerializeField] private int dragSortingOrder = 15;
    private int originalSortingOrder;

    // Internal Coordinates & State
    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;
    private Vector3 initialLocalScale;
    private Vector3 punchScale;

    // Bowl Cream Coordinates
    private Vector3 initialCreamLocalScale;
    private Vector3 initialCreamLocalPos;

    private bool isDragging = false;
    private bool isOverCake = false;
    private bool isFrostingDone = false;
    private float currentFrostingProgress = 0f;

    // Track: kya progress bar abhi visible hai (taake baar baar show/hide na ho)
    private bool isProgressBarVisible = false;

    // Boundary Extents
    private float leftExtent;
    private float rightExtent;
    private float topExtent;
    private float bottomExtent;

    private Camera mainCamera;
    private Vector3 dragOffset;
    private Vector3 lastWorldPos;
    private SpriteRenderer myRenderer;
    private Collider2D myCollider;
    private Coroutine returnCoroutine;
    private Coroutine scaleCoroutine;

    // Shader-based cream fill ke liye cached property block
    private MaterialPropertyBlock mpb;

    private void Awake()
    {
        mainCamera = Camera.main;
        myRenderer = GetComponent<SpriteRenderer>();
        myCollider = GetComponent<Collider2D>();

        if (myRenderer != null)
        {
            originalSortingOrder = myRenderer.sortingOrder;
        }

        // Knife local transform capture
        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;
        initialLocalScale = transform.localScale;
        punchScale = initialLocalScale * punchScaleMultiplier;

        // Bowl cream transform capture
        if (bowlCreamTransform != null)
        {
            initialCreamLocalScale = bowlCreamTransform.localScale;
            initialCreamLocalPos = bowlCreamTransform.localPosition;
        }

        // Shader fill ke liye MaterialPropertyBlock cache karein
        mpb = new MaterialPropertyBlock();
    }

    private void Start()
    {
        // 1. Cake Layer ka Animator disable karein — shader fill se control hoga
        if (cakeLayerRenderer != null)
        {
            Animator anim = cakeLayerRenderer.GetComponent<Animator>();
            if (anim != null) anim.enabled = false;

            // Alpha FULL rakhein — shader khud fill control karega
            Color c = cakeLayerRenderer.color;
            c.a = 1f;
            cakeLayerRenderer.color = c;

            // Shader fill zero par start — cream abhi nahi dikhegi
            SetCakeLayerAlpha(0f);
        }

        // 2. Screen Extents mathematically calculate karein
        CalculateExtents();

        // 3. Progress Bar ko start par HIDE rakhein — sirf cake ke trigger mein show hogi
        if (useProgressBar && SequentialAnimatorController.Instance != null)
        {
            SequentialAnimatorController.Instance.HideProgressBar();
        }
        isProgressBarVisible = false;
    }

    private void CalculateExtents()
    {
        if (myRenderer == null) return;

        Bounds b = myRenderer.bounds;
        leftExtent = Mathf.Max(0.2f, (transform.position.x - b.min.x) * clampTightness);
        rightExtent = Mathf.Max(0.2f, (b.max.x - transform.position.x) * clampTightness);
        bottomExtent = Mathf.Max(0.2f, (transform.position.y - b.min.y) * clampTightness);
        topExtent = Mathf.Max(0.2f, (b.max.y - transform.position.y) * clampTightness);
    }

    private void Update()
    {
        if (isFrostingDone) return;

        if (isDragging && Input.GetMouseButtonUp(0))
        {
            ReleaseKnife();
        }
    }

    private void OnMouseDown()
    {
        if (isFrostingDone) return;

        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
            returnCoroutine = null;
        }

        if (myRenderer != null)
        {
            myRenderer.sortingOrder = dragSortingOrder;
        }

        SoundManager.Instance?.PlaySound(SoundManager.Instance.itemPickupSound);

        Vector3 mouseWorldPos = GetMouseWorldPosition();
        dragOffset = transform.position - mouseWorldPos;
        lastWorldPos = transform.position;
        isDragging = true;

        if (enableScalePunch)
        {
            StartVisualScale(punchScale);
        }

        // NOTE: Progress bar yahan show NAHI hogi
        // Sirf jab knife cake ke trigger mein enter kare tab show hogi (OnTriggerEnter2D / CheckCakeContact)
    }

    private void OnMouseDrag()
    {
        if (!isDragging || isFrostingDone) return;

        Vector3 mouseWorldPos = GetMouseWorldPosition();
        Vector3 targetPos = mouseWorldPos + dragOffset;

        // -------------------------------------------------------------
        // 1. Screen Boundary Clamping (Wall ke sath chipakne ke liye)
        // -------------------------------------------------------------
        if (clampToScreen && mainCamera != null)
        {
            float camDist = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
            Vector3 bottomLeft = mainCamera.ViewportToWorldPoint(new Vector3(0f, 0f, camDist));
            Vector3 topRight = mainCamera.ViewportToWorldPoint(new Vector3(1f, 1f, camDist));

            float minX = bottomLeft.x + leftExtent + wallMargin;
            float maxX = topRight.x - rightExtent - wallMargin;
            float minY = bottomLeft.y + bottomExtent + wallMargin;
            float maxY = topRight.y - topExtent - wallMargin;

            targetPos.x = Mathf.Clamp(targetPos.x, minX, maxX);
            targetPos.y = Mathf.Clamp(targetPos.y, minY, maxY);
        }

        Vector3 previousFramePos = transform.position;
        transform.position = new Vector3(targetPos.x, targetPos.y, transform.position.z);

        // -------------------------------------------------------------
        // 2. Cake Contact Check
        // -------------------------------------------------------------
        bool wasOverCake = isOverCake;
        CheckCakeContact();

        // -------------------------------------------------------------
        // 2b. Progress Bar Show/Hide based on cake contact
        // -------------------------------------------------------------
        if (useProgressBar && SequentialAnimatorController.Instance != null)
        {
            // Jab knife PEHLI DAFA cake ke trigger mein aaye → bar SHOW karo
            if (isOverCake && !wasOverCake)
            {
                SequentialAnimatorController.Instance.ShowProgressBar(currentFrostingProgress);
                isProgressBarVisible = true;
            }
            // Jab knife cake se BAHAR nikle → bar HIDE karo
            else if (!isOverCake && wasOverCake)
            {
                SequentialAnimatorController.Instance.HideProgressBar();
                isProgressBarVisible = false;
            }
        }

        // -------------------------------------------------------------
        // 3. Smooth Progressive Frosting + Progress Bar + Bowl Cream Drop
        // -------------------------------------------------------------
        if (isOverCake && !isFrostingDone)
        {
            float rawDelta = Vector3.Distance(transform.position, previousFramePos);

            // Per-frame delta ko clamp karein taake fast mouse flick se instant fill na ho
            float clampedDelta = Mathf.Min(rawDelta, 0.08f);

            if (clampedDelta > 0.0005f)
            {
                currentFrostingProgress += (clampedDelta / dragDistanceToComplete);
                currentFrostingProgress = Mathf.Clamp01(currentFrostingProgress);

                // A. Cake Layer Alpha update
                SetCakeLayerAlpha(currentFrostingProgress);

                // B. Central Progress Bar fill update
                if (useProgressBar && SequentialAnimatorController.Instance != null)
                {
                    SequentialAnimatorController.Instance.UpdateProgressBar(currentFrostingProgress);
                }

                // C. Bowl ke andar cream level decrease
                UpdateBowlCreamLevel(currentFrostingProgress);

                // D. 100% Task Complete
                if (currentFrostingProgress >= 1.0f)
                {
                    OnFrostingCompleted();
                }
            }
        }

        lastWorldPos = transform.position;
    }

    private void CheckCakeContact()
    {
        if (cakeCollider == null) return;

        bool touching = false;
        if (myCollider != null)
        {
            touching = myCollider.IsTouching(cakeCollider);
        }
        else
        {
            touching = cakeCollider.OverlapPoint(transform.position);
        }

        isOverCake = touching;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(cakeTag) || other == cakeCollider)
        {
            isOverCake = true;

            // Jab knife cake ke trigger mein enter kare → progress bar SHOW karo
            if (useProgressBar && SequentialAnimatorController.Instance != null && isDragging && !isFrostingDone)
            {
                SequentialAnimatorController.Instance.ShowProgressBar(currentFrostingProgress);
                isProgressBarVisible = true;
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag(cakeTag) || other == cakeCollider)
        {
            isOverCake = false;

            // Jab knife cake se bahar nikle → progress bar HIDE karo
            if (useProgressBar && SequentialAnimatorController.Instance != null && isDragging && !isFrostingDone)
            {
                SequentialAnimatorController.Instance.HideProgressBar();
                isProgressBarVisible = false;
            }
        }
    }

    private void SetCakeLayerAlpha(float alpha)
    {
        if (cakeLayerRenderer != null)
        {
            if (mpb == null) mpb = new MaterialPropertyBlock();
            cakeLayerRenderer.GetPropertyBlock(mpb);
            mpb.SetFloat("_FillAmount", alpha);
            cakeLayerRenderer.SetPropertyBlock(mpb);

            // Safety: agar custom shader na laga ho to normal alpha fade chalay
            if (cakeLayerRenderer.sharedMaterial == null || !cakeLayerRenderer.sharedMaterial.HasProperty("_FillAmount"))
            {
                Color c = cakeLayerRenderer.color;
                c.a = alpha;
                cakeLayerRenderer.color = c;
            }
        }
    }

    private void UpdateBowlCreamLevel(float progress)
    {
        if (bowlCreamTransform == null) return;

        // Y scale shrink karein (Cream neeche baith rahi hai)
        float targetScaleY = Mathf.Lerp(initialCreamLocalScale.y, initialCreamLocalScale.y * minCreamScaleY, progress);
        float targetScaleX = Mathf.Lerp(initialCreamLocalScale.x, initialCreamLocalScale.x * 0.85f, progress);

        bowlCreamTransform.localScale = new Vector3(targetScaleX, targetScaleY, initialCreamLocalScale.z);

        // Cream ko slight neeche shift karein taake bowl ki bottom mein depth feel ho
        float targetPosY = Mathf.Lerp(initialCreamLocalPos.y, initialCreamLocalPos.y - 0.15f, progress);
        bowlCreamTransform.localPosition = new Vector3(initialCreamLocalPos.x, targetPosY, initialCreamLocalPos.z);
    }

    private void ReleaseKnife()
    {
        if (!isDragging) return;
        isDragging = false;
        isOverCake = false;

        SoundManager.Instance?.PlaySound(SoundManager.Instance.itemReleaseSound);

        if (enableScalePunch)
        {
            StartVisualScale(initialLocalScale);
        }

        // Progress bar HIDE karo jab user drag chhoray
        if (useProgressBar && SequentialAnimatorController.Instance != null && isProgressBarVisible)
        {
            SequentialAnimatorController.Instance.HideProgressBar();
            isProgressBarVisible = false;
        }

        // Return to Bowl (Progress wahi freeze rahegi)
        if (returnCoroutine != null) StopCoroutine(returnCoroutine);
        returnCoroutine = StartCoroutine(ReturnToBowlRoutine());
    }

    private IEnumerator ReturnToBowlRoutine()
    {
        while (Vector3.Distance(transform.localPosition, initialLocalPosition) > 0.05f)
        {
            transform.localPosition = Vector3.MoveTowards(
                transform.localPosition,
                initialLocalPosition,
                returnSpeed * Time.deltaTime
            );

            transform.localRotation = Quaternion.RotateTowards(
                transform.localRotation,
                initialLocalRotation,
                returnSpeed * 15f * Time.deltaTime
            );

            yield return null;
        }

        transform.localPosition = initialLocalPosition;
        transform.localRotation = initialLocalRotation;
        transform.localScale = initialLocalScale;

        if (myRenderer != null)
        {
            myRenderer.sortingOrder = originalSortingOrder;
        }

        returnCoroutine = null;
    }

    private void OnFrostingCompleted()
    {
        if (isFrostingDone) return;
        isFrostingDone = true;
        isDragging = false;
        isOverCake = false;

        SetCakeLayerAlpha(1.0f);
        UpdateBowlCreamLevel(1.0f);

        // Progress bar hide karein
        if (useProgressBar && SequentialAnimatorController.Instance != null)
        {
            SequentialAnimatorController.Instance.HideProgressBar();
            isProgressBarVisible = false;
        }

        // Knife wapis bowl mein return ho
        if (returnCoroutine != null) StopCoroutine(returnCoroutine);
        returnCoroutine = StartCoroutine(ReturnToBowlRoutine());

        // Controller ko stage completion trigger karein
        if (Recipe4Controller.Instance != null)
        {
            Recipe4Controller.Instance.OnFrostingFinished();
        }
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
                scaleLerpSpeed * Time.deltaTime
            );
            yield return null;
        }

        transform.localScale = targetScale;
        scaleCoroutine = null;
    }

    private Vector3 GetMouseWorldPosition()
    {
        float camDist = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = camDist;
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(mouseScreenPos);
        worldPos.z = transform.position.z;
        return worldPos;
    }
}
