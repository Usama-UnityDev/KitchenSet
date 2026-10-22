using UnityEngine;
using UnityEngine.Events;
using DG.Tweening;

public class BrushDrag : MonoBehaviour
{
    public enum BrushState
    {
        OnShelf,
        DraggingToPan,
        BrushingInPan,
        Completed
    }

    [Header("Current State (Read Only)")]
    [SerializeField] private BrushState currentState = BrushState.OnShelf;

    [Header("1. Animators")]
    [Tooltip("BowlOil parent ka Animator")]
    [SerializeField] private Animator parentIdleAnimator;
    [Tooltip("Brush ka apna Animator (Jisme rotation animation hai)")]
    [SerializeField] private Animator brushToolAnimator;
    [Tooltip("Brush ke rotating clip ka exact state name (default: Rotation)")]
    [SerializeField] private string brushAnimStateName = "Rotation";

    [Header("2. Pickup & Scale Settings")]
    [Tooltip("Uthate waqt scale (1.25x bada dikhega)")]
    [SerializeField] private float pickupScaleMultiplier /*= 1.25f*/;
    [SerializeField] private float scaleDuration /*= 0.2f*/;
    [SerializeField] private float returnDuration /*= 0.35f*/;

    [Header("3. Screen Boundary Settings")]
    [SerializeField] private bool clampToScreen = true;
    [SerializeField] private float extraMargin = 0.1f;

    [Header("4. Steel Pan Boundary & Permanent Lock")]
    [Tooltip("Bake pan ka center point (brushOilPoint)")]
    [SerializeField] private Transform panCenterPoint;
    [Tooltip("Pan boundary center offset (X: 0, Y: 0.1 rakhein)")]
    [SerializeField] private Vector2 panBoundaryOffset = new Vector2(0f, 0.1f);
    [Tooltip("Kitne fasle par brush pan ke andar snap/lock ho")]
    [SerializeField] private float entryTriggerRadius = 1.4f;
    [Tooltip("Pan ke andar horizontal (X) hadood - 0.85 rakhein")]
    [SerializeField] private float panLimitRadiusX = 0.85f;
    [Tooltip("Pan ke andar vertical (Y) hadood - 0.35 rakhein")]
    [SerializeField] private float panLimitRadiusY = 0.35f;

    [Header("5. Guidance Arrows (SpriteHold System)")]
    [Tooltip("Pan ki taraf ishara karne wala arrows GameObject")]
    public GameObject arrowsGuide;
    private Sequence arrowSequence;
    private Vector3 initialArrowScale;
    private SpriteRenderer[] arrowRenderers;
    private float[] initialArrowAlphas;

    [Header("Slide Out & Next Task")]
    [SerializeField] private SlideOutObject bowlSlideOut; // Yahan BowlOil assign karein

    [Header("6. Oil Pouring Animation & Progress")]
    [Tooltip("Bake pan -> Items -> oil ka SpriteRenderer")]
    [SerializeField] private SpriteRenderer targetOilSprite;
    [Tooltip("Pan ke andar ka Oil Animator")]
    [SerializeField] private Animator targetOilAnimator;
    [SerializeField] private string oilAnimStateName = "oilPouring";
    [Tooltip("Kitna swirl karne par oil 100% lagay (35 best hai)")]
    [SerializeField] private float totalDragDistanceRequired = 35f;

    //[Header("7. Particles & Completion")]
    //[Tooltip("Drag karte waqt chalne wala Splashing particle object")]
    //[SerializeField] private GameObject brushingParticles;
    //[Tooltip("100% complete hone par final celebration sparkles")]
    //[SerializeField] private GameObject completionSparkles;
    //public UnityEvent onBrushingComplete;

    [Header("Audio Settings")]
    [Tooltip("Pan ke andar brush pherne ki continuous swish / stroke aawaz")]
    [SerializeField] private AudioClip brushStrokeLoopSound;

    // Internal Variables
    private Vector3 originalLocalPos;
    private Quaternion originalLocalRot;
    private Vector3 originalLocalScale;
    private Vector3 dragOffset;
    private Vector3 lastBrushPos;
    private float currentProgress = 0f;
    private bool isDragging = false;
    private bool hasEnteredPanPermanently = false;

    private Camera mainCamera;
    private SpriteRenderer mySpriteRenderer;
    private DraggableSortingOrder sortingOrderScript;
    private ParticleSystem brushParticleSystem;
    private Tween moveTween;
    private Tween scaleTween;
    private Tween rotTween;

    private void Awake()
    {
        mainCamera = Camera.main;
        mySpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        sortingOrderScript = GetComponent<DraggableSortingOrder>();

        if (brushToolAnimator == null)
            brushToolAnimator = GetComponent<Animator>();

        if (brushToolAnimator != null)
            brushToolAnimator.enabled = false;

        //if (brushingParticles != null)
        //    brushParticleSystem = brushingParticles.GetComponent<ParticleSystem>();

        originalLocalPos = transform.localPosition;
        originalLocalRot = transform.localRotation;
        originalLocalScale = transform.localScale;

        // Arrows Guide System Setup (Same as SpriteHoldAndMove)
        if (arrowsGuide != null)
        {
            initialArrowScale = arrowsGuide.transform.localScale;
            arrowRenderers = arrowsGuide.GetComponentsInChildren<SpriteRenderer>(true);

            if (arrowRenderers != null && arrowRenderers.Length > 0)
            {
                initialArrowAlphas = new float[arrowRenderers.Length];
                for (int i = 0; i < arrowRenderers.Length; i++)
                {
                    if (arrowRenderers[i] != null)
                        initialArrowAlphas[i] = arrowRenderers[i].color.a;
                }
            }
        }
    }

    private void OnEnable()
    {
        if (arrowsGuide != null)
            arrowsGuide.SetActive(false);
    }

    private void Start()
    {
        if (targetOilSprite != null)
        {
            targetOilSprite.gameObject.SetActive(true);
            Color c = targetOilSprite.color;
            c.a = 0f;
            targetOilSprite.color = c;
        }

        if (targetOilAnimator != null)
            targetOilAnimator.enabled = false;

        //if (brushingParticles != null)
        //    brushingParticles.SetActive(false);

        //if (completionSparkles != null)
        //    completionSparkles.SetActive(false);
    }

    private void OnMouseDown()
    {
        if (currentState == BrushState.Completed) return;

        // --- SOUND HOOK ---
        SoundManager.Instance?.PlaySound(SoundManager.Instance.itemPickupSound);
        // ------------------

        KillActiveTweens();
        isDragging = true;

        if (parentIdleAnimator != null)
            parentIdleAnimator.enabled = false;

        Vector3 mouseWorld = GetMouseWorldPos();
        dragOffset = transform.position - mouseWorld;
        lastBrushPos = transform.position;

        // Shelf se pehli dafa uthate waqt: Big scale punch + Arrows ON
        if (!hasEnteredPanPermanently)
        {
            currentState = BrushState.DraggingToPan;

            if (sortingOrderScript != null)
            {
                sortingOrderScript.BringToFront();
            }

            if (brushToolAnimator != null)
                brushToolAnimator.enabled = false;

            float effectiveMultiplier = /*(pickupScaleMultiplier <= 1.05f) ? pickupScaleMultiplier :*/ pickupScaleMultiplier;
            scaleTween = transform.DOScale(originalLocalScale * effectiveMultiplier, scaleDuration).SetEase(Ease.OutBack);
            rotTween = transform.DOLocalRotateQuaternion(originalLocalRot, 0.15f);

            StartArrowGuidance();
        }
        else
        {
            // Pan ke andar normal scale
            currentState = BrushState.BrushingInPan;
            transform.localScale = originalLocalScale;
        }
    }

    private void OnMouseDrag()
    {
        if (!isDragging || currentState == BrushState.Completed) return;

        Vector3 mouseWorld = GetMouseWorldPos();
        Vector3 desiredPos = mouseWorld + dragOffset;

        // 1. FREE DRAG (Pan ke bahar)
        if (!hasEnteredPanPermanently)
        {
            if (clampToScreen)
                desiredPos = ClampToScreen(desiredPos);

            transform.position = desiredPos;

            if (panCenterPoint != null)
            {
                Vector3 panCenter = panCenterPoint.position + (Vector3)panBoundaryOffset;
                float distToPan = Vector2.Distance(transform.position, panCenter);
                if (distToPan <= entryTriggerRadius)
                {
                    EnterPanPermanently();
                }
            }
        }
        // 2. LOCKED IN PAN (Pan ki hadood ke andar qaid)
        else
        {
            transform.position = ClampInsidePanBounds(desiredPos);

            float frameMoveDist = Vector2.Distance(transform.position, lastBrushPos);

            if (frameMoveDist > 0.0015f)
            {
                // --- SOUND PLAY ---
                if (brushStrokeLoopSound != null)
                {
                    SoundManager.Instance?.PlayLoopingSound(brushStrokeLoopSound, 0.7f);
                }

                PlayBrushRotationAnimation();
                PlayBrushingParticles();

                currentProgress += frameMoveDist / Mathf.Max(1f, totalDragDistanceRequired);
                currentProgress = Mathf.Clamp01(currentProgress);

                UpdateOilVisualProgress(currentProgress);
                lastBrushPos = transform.position;

                if (currentProgress >= 1f)
                {
                    CompleteBrushingTask();
                }
            }
            else
            {
                // --- SOUND PAUSE ---
                SoundManager.Instance?.StopLoopingSound();
                PausePanFeedback();
            }
        }
    }

    private void OnMouseUp()
    {
        // Brushing sound band karein
        SoundManager.Instance?.StopLoopingSound();

        //if (!isInsidePan)
        //{
        //    // Shelf / Bowl wapis return sound
        //    SoundManager.Instance?.PlaySound(SoundManager.Instance.itemReleaseSound);
        //}

        if (!isDragging || currentState == BrushState.Completed) return;
        isDragging = false;

        PausePanFeedback();

        // Pan mein pohnchne se pehle chhora toh shelf par wapis
        if (!hasEnteredPanPermanently)
        {
            // Shelf / Bowl wapis return sound
            SoundManager.Instance?.PlaySound(SoundManager.Instance.itemReleaseSound);
            ReturnToOilBowl();
        }
        else
        {
            // Pan ke andar chhorne par brush pan mein hi ruka rahega
            transform.localScale = originalLocalScale;
        }
    }

    private void EnterPanPermanently()
    {
        hasEnteredPanPermanently = true;
        currentState = BrushState.BrushingInPan;
        lastBrushPos = transform.position;

        scaleTween = transform.DOScale(originalLocalScale, 0.15f).SetEase(Ease.OutQuad);

        // Pan mein enter hotay hi arrows cleanly reset aur hide
        ResetAndHideArrows();

        if (sortingOrderScript != null)
            sortingOrderScript.ResetSorting();

        if (targetOilAnimator != null)
        {
            targetOilAnimator.enabled = true;
            targetOilAnimator.speed = 0f;
        }
    }

    #region Arrow Guidance System (SpriteHold Equivalent)

    private void StartArrowGuidance()
    {
        if (arrowsGuide == null || hasEnteredPanPermanently) return;

        arrowsGuide.SetActive(true);
        KillArrowTweens();

        arrowsGuide.transform.localScale = initialArrowScale;
        ResetArrowAlphasImmediate();

        // Scale breathing sequence
        arrowSequence = DOTween.Sequence();
        arrowSequence.Append(arrowsGuide.transform.DOScale(initialArrowScale * 1.15f, 0.7f).SetEase(Ease.InOutSine))
                     .Append(arrowsGuide.transform.DOScale(initialArrowScale, 0.7f).SetEase(Ease.InOutSine))
                     .SetLoops(-1);

        // Alpha pulsing loop
        if (arrowRenderers != null)
        {
            for (int i = 0; i < arrowRenderers.Length; i++)
            {
                SpriteRenderer sr = arrowRenderers[i];
                if (sr != null)
                {
                    sr.DOKill();
                    float baseAlpha = initialArrowAlphas != null && i < initialArrowAlphas.Length ? initialArrowAlphas[i] : 1f;
                    sr.DOFade(baseAlpha * 0.4f, 0.7f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
                }
            }
        }
    }

    private void ResetAndHideArrows()
    {
        KillArrowTweens();
        if (arrowsGuide != null)
        {
            arrowsGuide.transform.localScale = initialArrowScale;
            ResetArrowAlphasImmediate();
            arrowsGuide.SetActive(false);
        }
    }

    private void KillArrowTweens()
    {
        if (arrowSequence != null && arrowSequence.IsActive()) arrowSequence.Kill();
        if (arrowsGuide != null) arrowsGuide.transform.DOKill();

        if (arrowRenderers != null)
        {
            foreach (var sr in arrowRenderers)
            {
                if (sr != null) sr.DOKill();
            }
        }
    }

    private void ResetArrowAlphasImmediate()
    {
        if (arrowRenderers == null) return;
        for (int i = 0; i < arrowRenderers.Length; i++)
        {
            SpriteRenderer sr = arrowRenderers[i];
            if (sr != null)
            {
                Color c = sr.color;
                c.a = initialArrowAlphas != null && i < initialArrowAlphas.Length ? initialArrowAlphas[i] : 1f;
                sr.color = c;
            }
        }
    }

    #endregion

    private void PlayBrushRotationAnimation()
    {
        if (brushToolAnimator == null) return;

        if (!brushToolAnimator.enabled)
        {
            brushToolAnimator.enabled = true;
            if (!string.IsNullOrEmpty(brushAnimStateName))
                brushToolAnimator.Play(brushAnimStateName, 0, 0f);
        }

        brushToolAnimator.speed = 1f;

        if (!string.IsNullOrEmpty(brushAnimStateName))
        {
            AnimatorStateInfo info = brushToolAnimator.GetCurrentAnimatorStateInfo(0);
            if (info.IsName(brushAnimStateName) && info.normalizedTime >= 1f)
            {
                brushToolAnimator.Play(brushAnimStateName, 0, 0f);
            }
        }
    }

    private void PlayBrushingParticles()
    {
        //if (brushingParticles == null) return;

        //if (!brushingParticles.activeSelf)
        //    brushingParticles.SetActive(true);

        //if (brushParticleSystem != null && !brushParticleSystem.isPlaying)
        //    brushParticleSystem.Play();
    }

    private void PausePanFeedback()
    {
        if (brushToolAnimator != null)
            brushToolAnimator.speed = 0f;

        if (brushParticleSystem != null && brushParticleSystem.isPlaying)
            brushParticleSystem.Pause();
    }

    private Vector3 ClampInsidePanBounds(Vector3 targetPos)
    {
        if (panCenterPoint == null) return targetPos;

        Vector3 center = panCenterPoint.position + (Vector3)panBoundaryOffset;
        float dx = targetPos.x - center.x;
        float dy = targetPos.y - center.y;

        float rx = Mathf.Max(0.1f, panLimitRadiusX);
        float ry = Mathf.Max(0.1f, panLimitRadiusY);

        float normalizedDist = (dx * dx) / (rx * rx) + (dy * dy) / (ry * ry);

        if (normalizedDist > 1f)
        {
            float scale = 1f / Mathf.Sqrt(normalizedDist);
            dx *= scale;
            dy *= scale;
        }

        return new Vector3(center.x + dx, center.y + dy, targetPos.z);
    }

    private void UpdateOilVisualProgress(float progress)
    {
        if (targetOilSprite != null)
        {
            Color c = targetOilSprite.color;
            c.a = progress;
            targetOilSprite.color = c;
        }

        if (targetOilAnimator != null && !string.IsNullOrEmpty(oilAnimStateName))
        {
            targetOilAnimator.Play(oilAnimStateName, 0, progress);
        }
    }

    private void CompleteBrushingTask()
    {
        currentState = BrushState.Completed;

        SoundManager.Instance?.StopLoopingSound();

        isDragging = false;

        PausePanFeedback();
        if (brushToolAnimator != null) brushToolAnimator.enabled = false;

        UpdateOilVisualProgress(1f);

        //if (brushingParticles != null)
        //    brushingParticles.SetActive(false);

        //if (completionSparkles != null)
        //    completionSparkles.SetActive(true);

        KillActiveTweens();
        DOVirtual.DelayedCall(0.5f, () =>
        {
            ReturnToOilBowlFinal();
            //onBrushingComplete?.Invoke();
        });
    }

    private void ReturnToOilBowl()
    {
        currentState = BrushState.OnShelf;
        KillActiveTweens();
        ResetAndHideArrows();

        if (brushToolAnimator != null)
            brushToolAnimator.enabled = false;

        scaleTween = transform.DOScale(originalLocalScale, returnDuration).SetEase(Ease.OutQuad);
        rotTween = transform.DOLocalRotateQuaternion(originalLocalRot, returnDuration);

        moveTween = transform.DOLocalMove(originalLocalPos, returnDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                transform.localScale = originalLocalScale;
                transform.localPosition = originalLocalPos;
                transform.localRotation = originalLocalRot;

                if (sortingOrderScript != null)
                    sortingOrderScript.ResetSorting();

                if (parentIdleAnimator != null)
                    parentIdleAnimator.enabled = true;
            });
    }

    private void ReturnToOilBowlFinal()
    {
        KillActiveTweens();

        scaleTween = transform.DOScale(originalLocalScale, returnDuration).SetEase(Ease.OutQuad);
        rotTween = transform.DOLocalRotateQuaternion(originalLocalRot, returnDuration);

        moveTween = transform.DOLocalMove(originalLocalPos, returnDuration)
            .SetEase(Ease.InOutQuad)
            .OnComplete(() =>
            {
                transform.localScale = originalLocalScale;
                transform.localPosition = originalLocalPos;
                transform.localRotation = originalLocalRot;

                if (sortingOrderScript != null)
                    sortingOrderScript.ResetSorting();

                // --- YAHAN SE ADD KAREIN ---
                // 1. BowlOil ko screen se slide out karein
                if (bowlSlideOut != null)
                {
                    bowlSlideOut.enabled = true;

                    // 2. Slide out animation khatam hote hi particles + next task trigger karein
                    float delay = bowlSlideOut.duration > 0f ? bowlSlideOut.duration : 0.8f;
                    DOVirtual.DelayedCall(delay, () =>
                    {
                        SequentialAnimatorController.Instance?.UpdateTask();
                    });
                }
                else
                {
                    // Safety: Agar bowlSlideOut assign na ho to direct next task chal jaye
                    SequentialAnimatorController.Instance?.UpdateTask();
                }
                // ---------------------------

            });
    }

    private Vector3 GetMouseWorldPos()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
        return mainCamera.ScreenToWorldPoint(mousePos);
    }

    private Vector3 ClampToScreen(Vector3 targetPos)
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return targetPos;

        float horizontalPadding = 0.5f;
        float verticalPadding = 0.5f;

        if (mySpriteRenderer != null)
        {
            horizontalPadding = mySpriteRenderer.bounds.extents.x + extraMargin;
            verticalPadding = mySpriteRenderer.bounds.extents.y + extraMargin;
        }

        float camDistance = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
        Vector3 min = mainCamera.ViewportToWorldPoint(new Vector3(0f, 0f, camDistance));
        Vector3 max = mainCamera.ViewportToWorldPoint(new Vector3(1f, 1f, camDistance));

        targetPos.x = Mathf.Clamp(targetPos.x, min.x + horizontalPadding, max.x - horizontalPadding);
        targetPos.y = Mathf.Clamp(targetPos.y, min.y + verticalPadding, max.y - verticalPadding);

        return targetPos;
    }

    private void KillActiveTweens()
    {
        KillArrowTweens();
        if (moveTween != null && moveTween.IsActive()) moveTween.Kill();
        if (scaleTween != null && scaleTween.IsActive()) scaleTween.Kill();
        if (rotTween != null && rotTween.IsActive()) rotTween.Kill();
    }

    private void OnDisable()
    {
        KillArrowTweens();
        if (moveTween != null && moveTween.IsActive()) moveTween.Kill();
        if (scaleTween != null && scaleTween.IsActive()) scaleTween.Kill();
        if (rotTween != null && rotTween.IsActive()) rotTween.Kill();
    }

    private void OnDrawGizmos()
    {
        if (panCenterPoint == null) return;

        Vector3 center = panCenterPoint.position + (Vector3)panBoundaryOffset;

        Gizmos.color = Color.green;
        int segments = 32;
        Vector3 prevPt = center + new Vector3(panLimitRadiusX, 0, 0);
        for (int i = 1; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 nextPt = center + new Vector3(Mathf.Cos(angle) * panLimitRadiusX, Mathf.Sin(angle) * panLimitRadiusY, 0);
            Gizmos.DrawLine(prevPt, nextPt);
            prevPt = nextPt;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(center, entryTriggerRadius);
    }
}