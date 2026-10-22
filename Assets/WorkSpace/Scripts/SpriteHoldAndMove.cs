using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class SpriteHoldAndMove : MonoBehaviour
{
    public enum MixerState
    {
        OnShelf,
        DraggingToBowl,
        InBowl,
        Completed
    }

    [Header("Current Interaction State")]
    public MixerState currentState = MixerState.OnShelf;

    [Header("Tool Animators")]
    public Animator shelfAnimator;
    public Animator mixerToolAnimator;

    [Header("Generic Animation Names")]
    [Tooltip("Is Tool ki mixing animation clip ka naam")]
    public string toolAnimStateName = "HandMixing";
    [Tooltip("Bowl batter ki mixing animation clip ka naam")]
    public string bowlAnimStateName = "Mixing";

    [Header("Dynamic Tool Sprites System (Optional)")]
    [Tooltip("Agar is tool ki multiple sprites hon (Outside vs Submerged)")]
    public bool useDynamicToolSprites = false;
    public List<Sprite> toolSprites = new List<Sprite>();

    [Header("Bowl References")]
    [Tooltip("Bowl ke andar ka mixture GameObject (Bowl -> Items -> Mixing)")]
    public GameObject bowlMixingObject;
    [Tooltip("Bowl ke andar mixture ka Animator component")]
    public Animator bowlMixingAnimator;
    [Tooltip("Task finish hone par final batter sprite (Sprite 8)")]
    public Sprite finalMixtureSprite;

    [Header("Bowl Positioning & Boundaries")]
    public Transform bowlTargetPoint;
    public float bowlTriggerRadius = 1.3f;
    [Tooltip("Bowl ke andar horizontal movement limit (Left/Right)")]
    public float mixingRadiusX = 0.55f;
    [Tooltip("Bowl ke andar vertical movement limit (Up/Down)")]
    public float mixingRadiusY = 0.2f;
    [Tooltip("Tool ko bowl ke center se kitna vertical offset dena hai")]
    public float mixerYOffsetInsideBowl = 0f;

    [Header("Progress Settings")]
    public float totalMixingDuration = 4.0f;

    [Header("Visual Effects & Guides")]
    public ParticleSystem splashingParticles;
    public GameObject arrowsGuide;
    private Sequence arrowSequence;
    private Vector3 initialArrowScale;
    private SpriteRenderer[] arrowRenderers;
    private float[] initialArrowAlphas;

    [Header("Slide Out")]
    public SlideOutObject slideOutScript;
    public SlideOutObject bowlSlideOutScript;
    [Tooltip("Mixer tool ki idle / default animation state ka naam (jo shelf par hoti hai)")]
    public string toolIdleStateName = "Idle";

    [Tooltip("Task finish hone par bowl kitna bounce / pop kare")]
    public float bowlBounceDuration = 1f;
    [Tooltip("Bounce khatam hone ke baad slide out se pehle ka wait")]
    public float bowlWaitBeforeSlideOut = 0.4f;

    // Internal Variables
    private Camera mainCam;
    private SpriteRenderer spriteRenderer;
    private Vector3 dragOffset;
    private bool isTouching = false;
    private Vector3 lastMousePos;
    private float currentMixingProgress = 0f;
    private Tweener returnTween;
    private Tweener scaleTween;

    private Vector3 shelfRestPos;
    private Vector3 originalScale;
    private int currentToolSpriteIndex = -1;

    [Header("Audio Settings")]
    [SerializeField] private AudioClip mixingLoopSound;

    private void Awake()
    {
        mainCam = Camera.main;
        // spriteRenderer = GetComponent<SpriteRenderer>();
        // Parent ya kisi bhi child se SpriteRenderer auto dhoondein (Null check fix)
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

        if (mixerToolAnimator == null)
            mixerToolAnimator = GetComponent<Animator>();

        if (slideOutScript == null)
            slideOutScript = GetComponent<SlideOutObject>();

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
        currentState = MixerState.OnShelf;
        currentMixingProgress = 0f;
        isTouching = false;
        originalScale = transform.localScale;

        SetToolSpriteByIndex(0);

        if (shelfAnimator != null)
            shelfAnimator.enabled = true;

        // Shelf par child animator STRICTLY DISABLED rahega
        if (mixerToolAnimator != null)
        {
            mixerToolAnimator.enabled = false;
        }

        if (splashingParticles != null)
            splashingParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (arrowsGuide != null)
            arrowsGuide.SetActive(false);

        if (bowlMixingObject != null)
            bowlMixingObject.SetActive(false);

        if (bowlMixingAnimator != null)
        {
            bowlMixingAnimator.enabled = false;
        }
    }

    private void OnMouseDown()
    {
        if (currentState == MixerState.Completed) return;

        // --- YAHAN ADD KAREIN ---
        // Sirf tab pickup sound play ho jab tool shelf par ho aur player usay uthaye
        if (currentState == MixerState.OnShelf)
        {
            SoundManager.Instance?.PlaySound(SoundManager.Instance.itemPickupSound);
        }
        // ------------------------

        if (returnTween != null && returnTween.IsActive()) returnTween.Kill();
        if (scaleTween != null && scaleTween.IsActive()) scaleTween.Kill();

        isTouching = true;
        lastMousePos = GetMouseWorldPos();

        if (currentState == MixerState.OnShelf)
        {
            shelfRestPos = transform.position;
            currentState = MixerState.DraggingToBowl;

            if (shelfAnimator != null)
                shelfAnimator.enabled = false;

            StartArrowGuidance();
        }

        scaleTween = transform.DOScale(originalScale * 1.1f, 0.2f).SetEase(Ease.OutBack);
        dragOffset = transform.position - lastMousePos;

        GetComponent<DraggableSortingOrder>()?.BringToFront(); // Items ko front pr lana.

        // Click hold karne par animation freeze rahegi
        if (currentState == MixerState.InBowl)
        {
            SetFeedback(false);
        }
    }

    private void OnMouseDrag()
    {
        if (!isTouching) return;

        Vector3 currentMousePos = GetMouseWorldPos();

        // 1. SHELF SE BOWL TAK DRAG
        if (currentState == MixerState.DraggingToBowl)
        {
            Vector3 targetPos = currentMousePos + dragOffset;
            transform.position = ClampToScreenBoundsGeneric(targetPos);
            SetToolSpriteByIndex(0);

            if (bowlTargetPoint != null)
            {
                float dist = Vector2.Distance(transform.position, bowlTargetPoint.position);
                if (dist <= bowlTriggerRadius)
                {
                    SnapToBowl();
                }
            }
        }
        // 2. MIXING INSIDE BOWL
        else if (currentState == MixerState.InBowl)
        {
            if (arrowsGuide != null && arrowsGuide.activeSelf)
            {
                ResetAndHideArrows();
            }

            float moveDelta = Vector3.Distance(currentMousePos, lastMousePos);

            Vector3 baseCenter = bowlTargetPoint.position + new Vector3(0f, mixerYOffsetInsideBowl, 0f);
            Vector3 mouseOffset = currentMousePos - baseCenter;

            float clampedX = Mathf.Clamp(mouseOffset.x, -mixingRadiusX, mixingRadiusX);
            float clampedY = Mathf.Clamp(mouseOffset.y, -mixingRadiusY, mixingRadiusY);
            transform.position = baseCenter + new Vector3(clampedX, clampedY, 0f);

            // Sirf mouse/finger movement par progress aur animation chalegi
            if (moveDelta > 0.003f)
            {
                if (bowlMixingObject != null && !bowlMixingObject.activeSelf)
                {
                    bowlMixingObject.SetActive(true);
                }

                SetToolSpriteByIndex(1);

                currentMixingProgress += Time.deltaTime;

                if (SequentialAnimatorController.Instance != null)
                {
                    SequentialAnimatorController.Instance.UpdateProgressBar(currentMixingProgress / totalMixingDuration);
                }

                SetFeedback(true);

                if (currentMixingProgress >= totalMixingDuration)
                {
                    CompleteMixingSequence();
                }
            }
            else
            {
                // Touch ruka hua hai: Freeze
                SetFeedback(false);
            }

            lastMousePos = currentMousePos;
        }
    }

    private void OnMouseUp()
    {
        isTouching = false;

        if (currentState == MixerState.DraggingToBowl)
        {
            ReturnToShelf();
        }
        else if (currentState == MixerState.InBowl)
        {
            SetFeedback(false);
        }
    }

    private void ReturnToShelf()
    {
        currentState = MixerState.OnShelf;
        ResetAndHideArrows();
        SetToolSpriteByIndex(0);

        // --- YAHAN ADD KAREIN ---
        SoundManager.Instance?.PlaySound(SoundManager.Instance.itemReleaseSound);
        // ------------------------

        returnTween = transform.DOMove(shelfRestPos, 0.45f).SetEase(Ease.OutQuad).OnComplete(() =>
        {
            if (shelfAnimator != null)
                shelfAnimator.enabled = true;

            // Shelf par aate hi child animator wapis OFF
            if (mixerToolAnimator != null)
                mixerToolAnimator.enabled = false;

            // Item poori tarah shelf par pohnch gaya hai, ab sorting order wapis original ho jaye:
            GetComponent<DraggableSortingOrder>()?.ResetSorting();
        });

        if (scaleTween != null && scaleTween.IsActive())
            scaleTween.Kill();

        

        scaleTween = transform.DOScale(originalScale, 0.35f).SetEase(Ease.OutQuad);
    }

    private void SnapToBowl()
    {
        currentState = MixerState.InBowl;
        isTouching = false;

        ResetAndHideArrows();

        Vector3 snapPos = bowlTargetPoint.position + new Vector3(0f, mixerYOffsetInsideBowl, 0f);
        transform.DOMove(snapPos, 0.25f).SetEase(Ease.OutBack);

        if (SequentialAnimatorController.Instance != null)
        {
            SequentialAnimatorController.Instance.ShowProgressBar(0f);
        }

        // Bowl mein snap hone par child animator ENABLE hoga, magar speed 0 rahegi jab tak drag na ho
        if (mixerToolAnimator != null)
        {
            mixerToolAnimator.enabled = true;
            mixerToolAnimator.speed = 0f;
        }

        SetFeedback(false);
    }

    private void SetToolSpriteByIndex(int index)
    {
        if (!useDynamicToolSprites || toolSprites == null || toolSprites.Count == 0) return;
        if (index < 0 || index >= toolSprites.Count) return;

        if (currentToolSpriteIndex != index && toolSprites[index] != null)
        {
            currentToolSpriteIndex = index;
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = toolSprites[index];
            }
        }
    }

    private void SetFeedback(bool isStirring)
    {
        float speed = isStirring ? 1f : 0f;

        if (mixerToolAnimator != null && mixerToolAnimator.enabled)
        {
            mixerToolAnimator.speed = speed;

            if (isStirring && !string.IsNullOrEmpty(toolAnimStateName))
            {
                AnimatorStateInfo info = mixerToolAnimator.GetCurrentAnimatorStateInfo(0);
                if (!info.IsName(toolAnimStateName))
                {
                    mixerToolAnimator.Play(toolAnimStateName, 0);
                }
            }
        }

        if (bowlMixingAnimator != null && bowlMixingObject != null && bowlMixingObject.activeSelf)
        {
            bowlMixingAnimator.enabled = true;
            bowlMixingAnimator.speed = speed;

            if (isStirring && !string.IsNullOrEmpty(bowlAnimStateName))
            {
                AnimatorStateInfo info = bowlMixingAnimator.GetCurrentAnimatorStateInfo(0);
                if (!info.IsName(bowlAnimStateName))
                {
                    bowlMixingAnimator.Play(bowlAnimStateName, 0);
                }
            }
        }

        if (splashingParticles != null)
        {
            if (isStirring)
            {
                if (mixingLoopSound != null)
                    SoundManager.Instance?.PlayLoopingSound(mixingLoopSound, 0.8f);

                if (!splashingParticles.isPlaying) splashingParticles.Play();
            }
            else
            {
                SoundManager.Instance?.StopLoopingSound();
                if (splashingParticles.isPlaying) splashingParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    private void CompleteMixingSequence()
    {
        SoundManager.Instance?.StopLoopingSound();

        currentState = MixerState.Completed;
        isTouching = false;

        SetFeedback(false);

        // Slide out se pehle child sprites ko hide karke clean full sprite restore karein
        RestoreCleanExitVisual();

        ResetAndHideArrows();
        SetToolSpriteByIndex(0);

        if (bowlMixingAnimator != null)
        {
            bowlMixingAnimator.enabled = false;
        }

        // Direct final sprite (Sprite 8) display lock
        if (bowlMixingObject != null && finalMixtureSprite != null)
        {
            SpriteRenderer batterRenderer = bowlMixingObject.GetComponentInChildren<SpriteRenderer>();
            if (batterRenderer != null)
            {
                batterRenderer.sprite = finalMixtureSprite;
            }
        }

        if (SequentialAnimatorController.Instance != null)
        {
            SequentialAnimatorController.Instance.HideProgressBar();
        }

        transform.DOScale(originalScale, 0.25f);

        if (slideOutScript != null)
        {
            slideOutScript.enabled = true;
        }

        // Soft, Satisfying & Gentle Pop (Eyes ke liye bilkul relaxing aur clean)
        if (bowlSlideOutScript != null)
        {
            Transform bowlTransform = bowlSlideOutScript.transform;
            Vector3 originalScale = bowlTransform.localScale;

            bowlTransform.DOKill();

            Sequence calmPopSeq = DOTween.Sequence();

            // 1. Soft Pop-Up: Thoda sa aaraam se 1.1x expand hoga (0.22s)
            calmPopSeq.Append(bowlTransform.DOScale(originalScale * 1.30f, 0.5f).SetEase(Ease.OutSine));

            // 2. Gentle Settle: Bina kisi hard jerk ke softly table par wapis aayega (0.28s)
            calmPopSeq.Append(bowlTransform.DOScale(originalScale, 0.7f).SetEase(Ease.OutBack));

            // 3. Sparkles aur completed look enjoy karne ke liye pause, phir smooth slide out
            calmPopSeq.AppendInterval(bowlWaitBeforeSlideOut);
            calmPopSeq.OnComplete(() =>
            {
                bowlTransform.localScale = originalScale;
                bowlSlideOutScript.enabled = true;
            });
        }

        if (SequentialAnimatorController.Instance != null)
        {
            SequentialAnimatorController.Instance.UpdateTask();
        }
    }
    /// <summary>
    /// Slide-out ke waqt tool ke child sprites ko hide karta hai aur main body par full sprite restore karta hai
    /// </summary>
    private void RestoreCleanExitVisual()
    {
        // 1. Tool animator ko band karein taake clip frames overwrite na karein
        if (mixerToolAnimator != null)
        {
            mixerToolAnimator.enabled = false;
        }

        // 2. Main object ka SpriteRenderer dhoondein
        SpriteRenderer mainSr = GetComponent<SpriteRenderer>();
        if (mainSr == null)
            mainSr = GetComponentInChildren<SpriteRenderer>(true);

        // 3. Main object ke ilawa sabhi child SpriteRenderers ko disable karein
        SpriteRenderer[] allRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer sr in allRenderers)
        {
            if (sr != null && sr != mainSr)
            {
                sr.enabled = false; // Sirf child sprites disable honge, particles safe rahenge
            }
        }

        // 4. Main body par Default Full Sprite (Index 0) apply karein
        if (mainSr != null)
        {
            mainSr.enabled = true;
            if (toolSprites != null && toolSprites.Count > 0 && toolSprites[0] != null)
            {
                mainSr.sprite = toolSprites[0];
            }
        }
    }
    private void StartArrowGuidance()
    {
        if (arrowsGuide == null) return;

        arrowsGuide.SetActive(true);
        KillArrowTweens();

        arrowsGuide.transform.localScale = initialArrowScale;
        ResetArrowAlphasImmediate();

        arrowSequence = DOTween.Sequence();
        arrowSequence.Append(arrowsGuide.transform.DOScale(initialArrowScale * 1.15f, 0.7f).SetEase(Ease.InOutSine))
                     .Append(arrowsGuide.transform.DOScale(initialArrowScale, 0.7f).SetEase(Ease.InOutSine))
                     .SetLoops(-1);

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

    private Vector3 ClampToScreenBoundsGeneric(Vector3 targetPos)
    {
        if (mainCam == null) mainCam = Camera.main;

        // 1. Poore object hierarchy (Parent + Children) ke combined bounds nikalein
        Bounds combinedBounds = new Bounds(transform.position, Vector3.zero);
        bool hasValidBounds = false;

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers != null && renderers.Length > 0)
        {
            foreach (SpriteRenderer sr in renderers)
            {
                if (sr != null && sr.enabled && sr.sprite != null)
                {
                    if (!hasValidBounds)
                    {
                        combinedBounds = sr.bounds;
                        hasValidBounds = true;
                    }
                    else
                    {
                        combinedBounds.Encapsulate(sr.bounds);
                    }
                }
            }
        }

        // Agar SpriteRenderer se na mile toh Colliders check karein
        if (!hasValidBounds)
        {
            Collider2D[] cols = GetComponentsInChildren<Collider2D>(true);
            foreach (Collider2D col in cols)
            {
                if (col != null && col.enabled)
                {
                    if (!hasValidBounds)
                    {
                        combinedBounds = col.bounds;
                        hasValidBounds = true;
                    }
                    else
                    {
                        combinedBounds.Encapsulate(col.bounds);
                    }
                }
            }
        }

        // 2. Pivot aur Visual Bounds ke darmiyan ka offset nikalein
        Vector3 boundsOffsetFromPivot = hasValidBounds ? (combinedBounds.center - transform.position) : Vector3.zero;
        Vector3 extents = hasValidBounds ? combinedBounds.extents : new Vector3(0.5f, 0.5f, 0f);

        // 3. Camera screen world limits
        float zDistance = Mathf.Abs(mainCam.transform.position.z - transform.position.z);
        Vector3 bottomLeft = mainCam.ViewportToWorldPoint(new Vector3(0f, 0f, zDistance));
        Vector3 topRight = mainCam.ViewportToWorldPoint(new Vector3(1f, 1f, zDistance));

        // 4. Combined Extents + Pivot Offset Clamping (Taake top ya bottom ka koi bhi hissa baahar na jaye)
        float minX = bottomLeft.x + extents.x - boundsOffsetFromPivot.x;
        float maxX = topRight.x - extents.x - boundsOffsetFromPivot.x;
        float minY = bottomLeft.y + extents.y - boundsOffsetFromPivot.y;
        float maxY = topRight.y - extents.y - boundsOffsetFromPivot.y;

        // Safety fallback agar scale bohot bara ho
        if (minX > maxX) minX = maxX = (bottomLeft.x + topRight.x) * 0.5f;
        if (minY > maxY) minY = maxY = (bottomLeft.y + topRight.y) * 0.5f;

        return new Vector3(
            Mathf.Clamp(targetPos.x, minX, maxX),
            Mathf.Clamp(targetPos.y, minY, maxY),
            transform.position.z
        );
    }

    private Vector3 GetMouseWorldPos()
    {
        if (mainCam == null) mainCam = Camera.main;
        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = Mathf.Abs(mainCam.transform.position.z - transform.position.z);
        Vector3 worldPos = mainCam.ScreenToWorldPoint(mouseScreenPos);
        worldPos.z = transform.position.z;
        return worldPos;
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

    private void OnDisable()
    {
        KillArrowTweens();
        if (returnTween != null) returnTween.Kill();
        if (scaleTween != null) scaleTween.Kill();
    }
}