using System.Collections;
using UnityEngine;
using DG.Tweening; // <-- Yeh line lazmi add karein
public class SpriteClickMover : MonoBehaviour
{
    [Header("Target Position (Bowl / Destination)")]
    public Transform targetPosition;

    [Header("Movement Settings")]
    public float moveSpeed = 10f;
    [SerializeField] private float returnSpeed = 20f;

    [Header("Visual Scale Settings (20% Punch Effect)")]
    [SerializeField] private bool enableScalePunch = true;
    [Tooltip("Click hone par sprite kitna bada ho (1.2 = 20% increase)")]
    [SerializeField] private float punchScaleMultiplier = 1.2f;
    [SerializeField] private float scaleTransitionSpeed = 15f;

    [Header("Screen Boundary Settings")]
    [SerializeField] private bool clampToScreen = true;
    [SerializeField] private float extraMargin = 0.1f;

    [Header("Position Reset Settings")]
    [SerializeField] private bool returnToOriginOnRelease = true;

    [Header("Multi-Item Batch (e.g. 2 Eggs)")]
    [SerializeField] private bool isPartOfBatch = false;
    [SerializeField] private int totalBatchItems = 2;
    private int remainingBatchItems = 0;

    [Header("Child Settings (Pouring / Particles)")]
    public bool isChild;
    public GameObject isChildItem;
    public bool autoPlayChildAnimation = true;
    [Min(0f)] public float childAnimationSpeed = 1f;
    [Range(0f, 1f)] public float childAnimationStopPoint = 0.5f;

    [Header("Parent Animator (Optional - Idle Pause Ke Liye)")]
    public GameObject parentAnimator;

    [Header("Parent Sprite (Optional)")]
    public bool parentSpriteDisable;
    [HideInInspector] public GameObject parentSprite;

    [Header("Item Type")]
    public ItemType itemType;

    // Internal State Variables
    private bool isMoving = false;
    private bool isDragging = false;
    private bool hasTriggeredTarget = false;
    //[Header("Target Drop Settings")]
    //[Tooltip("Bowl ke kitne qareeb aakar mouse release karne par item drop hoga")]
    //[SerializeField] private float dropDistanceThreshold = 2.0f; // Aap Inspector se kam ya zyada kar sakte hain

    private Vector3 originalPosition;
    private Camera mainCamera;
    private SpriteRenderer mySpriteRenderer;
    private Transform visualSpriteTransform;
    private Vector3 visualOriginalScale;
    private Vector3 visualPunchScale;

    private Coroutine returnCoroutine;
    private Coroutine scaleCoroutine;
    private Coroutine childPauseCoroutine;

    [Header("Target Reached Behavior (Generic)")]
    [Tooltip("Tick karein agar target/bowl par aate hi is item ko hide karwana ho (e.g. Eggs, Butter slices)")]
    [SerializeField] private bool hideOnTargetReached = false;

    private bool childAnimationTriggered = false;
    private bool hasSlidOut = false;

    [Header("Optional Target Exit & Bounce (Generic)")]
    [Tooltip("Agar yeh aakhri step hai aur iske baad target pan/bowl ko bhi screen se bahar bhejna hai")]
    [SerializeField] private SlideOutObject targetSlideOut; // Recipe 2 mein yahan 'Bake Pan' assign hoga

    [Tooltip("Moving object (Bowl) ke nikalne ke baad pan par kaam shuru hone ka delay")]
    [SerializeField] private float delayBeforeTargetSlide;

    [Tooltip("Pan/Bowl bahar jane se pehle pop/bounce kare ya nahi")]
    [SerializeField] private bool enableTargetBounce = true;

    [Tooltip("Bounce kitna bada ho (1.25 = 25% expand)")]
    [SerializeField] private float targetBounceScale = 1.25f;

    [Tooltip("Bounce khatam hone ke baad slide out se pehle ka pause (Sparkles enjoy karne ke liye)")]
    [SerializeField] private float targetWaitBeforeSlideOut;

    [Header("Audio Settings")]
    [Tooltip("Is item ka specific animation/pouring sound")]
    [SerializeField] private AudioClip itemActionSound;

    private void Awake()
    {
        mainCamera = Camera.main;

        // Visual SpriteRenderer detect karna (khud par ya child par)
        mySpriteRenderer = GetComponent<SpriteRenderer>();
        if (mySpriteRenderer == null)
            mySpriteRenderer = GetComponentInChildren<SpriteRenderer>();

        // Sirf visual GameObject ka transform target banana
        if (mySpriteRenderer != null)
        {
            visualSpriteTransform = mySpriteRenderer.transform;
            visualOriginalScale = visualSpriteTransform.localScale;
        }
        else
        {
            visualSpriteTransform = this.transform;
            visualOriginalScale = transform.localScale;
        }

        // 20% bada scale calculate karna
        visualPunchScale = visualOriginalScale * punchScaleMultiplier;
    }

    private void OnEnable()
    {
        originalPosition = transform.position;
        hasTriggeredTarget = false;
        isMoving = false;
        isDragging = false;

        // Scale ko normal par reset karna
        if (visualSpriteTransform != null)
            visualSpriteTransform.localScale = visualOriginalScale;

        // Batch counter reset
        if (isPartOfBatch)
        {
            remainingBatchItems = totalBatchItems;
        }

        childAnimationTriggered = false;
        hasSlidOut = false;
    }

    public void SetOriginPosition(Vector3 pos)
    {
        originalPosition = pos;
    }

    private void OnMouseDown()
    {
        SoundManager.Instance?.PlaySound(SoundManager.Instance.itemPickupSound);

        if (itemType == ItemType.InplaceItem || hasTriggeredTarget || isMoving)
        {
            if (itemType == ItemType.InplaceItem)
                ToggleChildAnimator();
            return;
        }

        if (originalPosition == Vector3.zero || Vector3.Distance(originalPosition, transform.position) > 10f)
        {
            originalPosition = transform.position;
        }

        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
            returnCoroutine = null;
        }

        GetComponent<DraggableSortingOrder>()?.BringToFront(); // Items ko front pr lana.

        isDragging = true;

        // Normal items (jaise Flour) ke shelf idle animator ko drag ke waqt pause karna
        //if (!isPartOfBatch && parentAnimator != null)
        //{
        //    Animator parentAnim = parentAnimator.GetComponent<Animator>();
        //    if (parentAnim != null) parentAnim.enabled = false;
        //}

        //// Single items (jaise Flour) ke liye normal behaviour
        if (parentAnimator != null)
        {
            Animator parentAnim = parentAnimator.GetComponent<Animator>();
            if (parentAnim != null) parentAnim.enabled = false;
        }


        // Sirf visual sprite ka scale 20% barhana
        if (enableScalePunch && visualSpriteTransform != null)
        {
            StartVisualScale(visualPunchScale);
        }
    }

    private void OnMouseDrag()
    {
        if (!isDragging || isMoving || hasTriggeredTarget)
            return;

        if (mainCamera == null)
            mainCamera = Camera.main;

        Vector3 mousePosition = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = originalPosition.z;

        // Screen boundary clamp
        if (clampToScreen && mainCamera != null)
        {
            float horizontalPadding = 0.5f;
            float verticalPadding = 0.5f;

            if (mySpriteRenderer != null)
            {
                horizontalPadding = mySpriteRenderer.bounds.extents.x + extraMargin;
                verticalPadding = mySpriteRenderer.bounds.extents.y + extraMargin;
            }

            float camDistance = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
            Vector3 bottomLeft = mainCamera.ViewportToWorldPoint(new Vector3(0, 0, camDistance));
            Vector3 topRight = mainCamera.ViewportToWorldPoint(new Vector3(1, 1, camDistance));

            mousePosition.x = Mathf.Clamp(mousePosition.x, bottomLeft.x + horizontalPadding, topRight.x - horizontalPadding);
            mousePosition.y = Mathf.Clamp(mousePosition.y, bottomLeft.y + verticalPadding, topRight.y - verticalPadding);
        }

        transform.position = mousePosition;


    }
    //private Vector3 returnTargetPosition;

    //private void LateUpdate()
    //{
    //    if (!isPartOfBatch)
    //        return;

    //    // Animator ne is frame mein apni normal animated position
    //    // already calculate kar di hai.

    //    if (isDragging)
    //    {
    //        transform.position = dragPosition;
    //        return;
    //    }

    //    if (isReturningToAnimator)
    //    {
    //        Vector3 animatorPosition = transform.position;

    //        returnTargetPosition = animatorPosition;

    //        transform.position = Vector3.MoveTowards(
    //            transform.position,
    //            returnTargetPosition,
    //            returnSpeed * Time.deltaTime
    //        );
    //    }
    //}
    private void OnMouseUp()
    {
        if (itemType == ItemType.InplaceItem || !isDragging)
            return;

        isDragging = false;

        // Chhorhne par sprite ka scale wapis normal karna
        if (enableScalePunch && visualSpriteTransform != null)
        {
            StartVisualScale(visualOriginalScale);
        }

        if (!hasTriggeredTarget)
        {
            SoundManager.Instance?.PlaySound(SoundManager.Instance.itemReleaseSound);

            if (returnToOriginOnRelease)
            {
                if (returnCoroutine != null) StopCoroutine(returnCoroutine);
                returnCoroutine = StartCoroutine(ReturnToOriginalPosition());
            }
            else
            {
                GetComponent<DraggableSortingOrder>()?.ResetSorting();
                ResumeIdleAnimator();
            }
        }

        //// CHECK: Kya mouse chhodte waqt item Bowl (targetPosition) ke qareeb tha?
        //bool isNearBowl = false;
        //if (targetPosition != null)
        //{
        //    float distanceToBowl = Vector2.Distance(transform.position, targetPosition.position);
        //    if (distanceToBowl <= dropDistanceThreshold)
        //    {
        //        isNearBowl = true;
        //    }
        //}

        //// Agar bowl ke qareeb laa kar chhora hai:
        //if (isNearBowl && !hasTriggeredTarget)
        //{
        //    hasTriggeredTarget = true;

        //    if (returnCoroutine != null)
        //    {
        //        StopCoroutine(returnCoroutine);
        //        returnCoroutine = null;
        //    }

        //    Collider2D[] cols = GetComponents<Collider2D>();
        //    foreach (Collider2D c in cols) c.enabled = false;

        //    StartCoroutine(MoveToTarget());
        //}
        //else
        //{
        //    // Agar bowl se door raste mein chhora hai toh wapis apni shelf par jaye:
        //    if (returnToOriginOnRelease)
        //    {
        //        if (returnCoroutine != null) StopCoroutine(returnCoroutine);
        //        returnCoroutine = StartCoroutine(ReturnToOriginalPosition());
        //    }
        //    else
        //    {
        //        ResumeIdleAnimator();
        //    }
        //}
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // FIX: Item sirf tab trigger ho jab player usko pakad kar drag kar raha ho!
        if (!isDragging) return;

        if (itemType == ItemType.InplaceItem || hasTriggeredTarget)
            return;

        if (other.CompareTag("Kitchen"))
        {
            hasTriggeredTarget = true;
            isDragging = false;

            if (returnCoroutine != null)
            {
                StopCoroutine(returnCoroutine);
                returnCoroutine = null;
            }

            // Target ke upar 20% punch scale maintain rakhna
            if (enableScalePunch && visualSpriteTransform != null)
            {
                StartVisualScale(visualPunchScale);
            }

            BoxCollider2D boxCol = GetComponent<BoxCollider2D>();
            if (boxCol != null) boxCol.enabled = false;

            CapsuleCollider2D capCol = GetComponent<CapsuleCollider2D>();
            if (capCol != null) capCol.enabled = false;

            StartCoroutine(MoveToTarget());
        }
    }

    private IEnumerator MoveToTarget()
    {
        isMoving = true;
        if (targetPosition != null)
        {
            while (Vector3.Distance(transform.position, targetPosition.position) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    targetPosition.position,
                    moveSpeed * Time.deltaTime
                );
                yield return null;
            }

            transform.position = targetPosition.position;
        }

        // --- YAHAN ADD KAREIN ---
        // Target par reach hote hi sorting order wapis original drop ho jaye
        GetComponent<DraggableSortingOrder>()?.ResetSorting();
        // -------------------------


        if (parentSpriteDisable && parentSprite != null)
        {
            parentSprite.SetActive(false);
        }

        Animator animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.enabled = true;
        }

        //if (itemType == ItemType.Egg)
        //{
        //    if (mySpriteRenderer != null)
        //        mySpriteRenderer.enabled = false;
        //}
        // [GENERIC HIDE LOGIC]
        // Ab enum check karne ki zaroorat nahi!
        if (hideOnTargetReached)
        {
            if (mySpriteRenderer != null)
            {
                mySpriteRenderer.enabled = false;
            }

        }

        if (autoPlayChildAnimation)
        {
            PlayChildItemAnimation();
        }

        isMoving = false;
    }

    private IEnumerator ReturnToOriginalPosition()
    {
        while (Vector3.Distance(transform.position, originalPosition) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                originalPosition,
                returnSpeed * Time.deltaTime
            );
            yield return null;
        }

        transform.position = originalPosition;
        returnCoroutine = null;

        // Item poori tarah shelf par pohnch gaya hai, ab sorting order wapis original ho jaye:
        GetComponent<DraggableSortingOrder>()?.ResetSorting();

        {
            // Sirf single items (Flour wagera) ke liye parent animator wapis on ho
            ResumeIdleAnimator();
        }
    }

    private void ResumeIdleAnimator()
    {
        //if (!isPartOfBatch && parentAnimator != null)
        //{
        //    Animator parentAnim = parentAnimator.GetComponent<Animator>();
        //    if (parentAnim != null) parentAnim.enabled = true;
        //}
        if (parentAnimator != null)
        {
            Animator parentAnim = parentAnimator.GetComponent<Animator>();
            if (parentAnim != null) parentAnim.enabled = true;
        }

        //Animator selfAnim = GetComponent<Animator>();
        //if (selfAnim != null) selfAnim.enabled = true;
    }

    private void StartVisualScale(Vector3 targetScale)
    {
        if (scaleCoroutine != null)
            StopCoroutine(scaleCoroutine);

        scaleCoroutine = StartCoroutine(AnimateVisualScale(targetScale));
    }

    private IEnumerator AnimateVisualScale(Vector3 targetScale)
    {
        if (visualSpriteTransform == null) yield break;

        while (Vector3.Distance(visualSpriteTransform.localScale, targetScale) > 0.01f)
        {
            visualSpriteTransform.localScale = Vector3.Lerp(
                visualSpriteTransform.localScale,
                targetScale,
                scaleTransitionSpeed * Time.deltaTime
            );
            yield return null;
        }

        visualSpriteTransform.localScale = targetScale;
        scaleCoroutine = null;
    }

    // =========================================
    // SLIDE OUT & TASK COMPLETION
    // =========================================

    public void SlideOutOfScreen()
    {
        //if (hasSlidOut) return; // Agar ek dafa call ho chuka hai toh ignore karein

        //hasSlidOut = true;

        //if (isPartOfBatch)
        //{
        //    remainingBatchItems--;

        //    // Agar abhi doosra anda plate par bacha hai, agla step load nahi hoga
        //    if (remainingBatchItems > 0)
        //    {
        //        gameObject.SetActive(false);
        //        return;
        //    }
        //}

        //// Slide out trigger
        //SlideOutObject slideOut = GetComponent<SlideOutObject>();
        //if (slideOut == null)
        //    slideOut = GetComponentInParent<SlideOutObject>();

        //if (slideOut != null)
        //{
        //    slideOut.enabled = true;
        //    //StartCoroutine(WaitAndCallNextTask(slideOut.duration));
        //    float waitTime = slideOut.duration > 0f ? slideOut.duration : 1f;

        //    // Controller par chalane se object disable hone par bhi next task 100% trigger hoga
        //    if (SequentialAnimatorController.Instance != null)
        //    {
        //        SequentialAnimatorController.Instance.StartCoroutine(WaitAndCallNextTask(waitTime));
        //    }
        //}
        //else
        //{
        //    if (SequentialAnimatorController.Instance != null)
        //    {
        //        SequentialAnimatorController.Instance.UpdateTask();
        //    }
        //}

        if (hasSlidOut) return;
        hasSlidOut = true;

        if (isPartOfBatch)
        {
            remainingBatchItems--;
            if (remainingBatchItems > 0)
            {
                gameObject.SetActive(false);
                return;
            }
        }

        SlideOutObject slideOut = GetComponent<SlideOutObject>();
        if (slideOut == null)
            slideOut = GetComponentInParent<SlideOutObject>();

        if (slideOut != null)
        {
            slideOut.enabled = true; // Sirf slide out script on hogi, yahan se task update NAHI hoga!
        }
    }

    private IEnumerator WaitAndCallNextTask(float waitDuration)
    {
        yield return new WaitForSeconds(waitDuration > 0f ? waitDuration : 0.6f);

        if (SequentialAnimatorController.Instance != null)
        {
            SequentialAnimatorController.Instance.UpdateTask();
        }
    }

    // =========================================
    // CHILD ANIMATION CONTROLS
    // =========================================

    public void PlayChildItemAnimation()
    {
        if (!isChild || isChildItem == null) return;

        // Agar pehle chal chuki hai toh dubara frame 0 se restart na kare
        if (childAnimationTriggered) return;
        childAnimationTriggered = true;

        // Action sound play karein (Flour pour, Milk stream, Egg crack)
        if (itemActionSound != null)
        {
            SoundManager.Instance?.PlaySound(itemActionSound);
        }

        isChildItem.SetActive(true);
        Animator childAnimator = isChildItem.GetComponent<Animator>();
        if (childAnimator != null)
        {
            childAnimator.enabled = true;
            childAnimator.speed = childAnimationSpeed;
            childAnimator.Play(0, 0, 0f);
        }
    }

    public void ToggleChildAnimator()
    {
        bool childWasInactive = isChildItem != null && !isChildItem.activeSelf;
        Animator childAnimator = GetChildAnimator(true);
        if (isChildItem != null && isChildItem.GetComponent<ChildItem>())
            isChildItem.GetComponent<ChildItem>().ChildItemsAnimator.Invoke();

        if (childAnimator == null) return;

        if (childWasInactive || !childAnimator.enabled || Mathf.Approximately(childAnimator.speed, 0f))
            PlayChildAnimator();
        else
            PauseChildAnimator();
    }

    public void PlayChildAnimator()
    {
        Animator childAnimator = GetChildAnimator(true);
        if (childAnimator == null) return;
        CancelChildPause();
        childAnimator.enabled = true;
        childAnimator.speed = childAnimationSpeed;
    }

    public void PauseChildAnimator()
    {
        Animator childAnimator = GetChildAnimator(false);
        if (childAnimator == null) return;
        CancelChildPause();
        childAnimator.speed = 0f;
    }

    public void PauseChildAnimatorAtPoint(float normalizedTime)
    {
        Animator childAnimator = GetChildAnimator(true);
        if (childAnimator == null) return;
        CancelChildPause();
        AnimatorStateInfo state = childAnimator.GetCurrentAnimatorStateInfo(0);
        childAnimator.Play(state.fullPathHash, 0, Mathf.Clamp01(normalizedTime));
        childAnimator.Update(0f);
        childAnimator.speed = 0f;
    }

    public void PauseChildAnimatorAtConfiguredPoint()
    {
        PauseChildAnimatorAtPoint(childAnimationStopPoint);
    }

    public void PlayChildAnimatorUntilPoint(float normalizedTime)
    {
        Animator childAnimator = GetChildAnimator(true);
        if (childAnimator == null) return;
        CancelChildPause();
        childAnimator.enabled = true;
        childAnimator.speed = childAnimationSpeed;
        childPauseCoroutine = StartCoroutine(PauseChildWhenPointIsReached(childAnimator, Mathf.Clamp01(normalizedTime)));
    }

    public void PlayChildAnimatorUntilConfiguredPoint()
    {
        PlayChildAnimatorUntilPoint(childAnimationStopPoint);
    }

    private IEnumerator PauseChildWhenPointIsReached(Animator childAnimator, float normalizedTime)
    {
        int stateHash = childAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash;
        while (childAnimator != null && childAnimator.enabled)
        {
            AnimatorStateInfo state = childAnimator.GetCurrentAnimatorStateInfo(0);
            if (state.fullPathHash != stateHash) break;
            if (state.normalizedTime >= normalizedTime)
            {
                childAnimator.Play(state.fullPathHash, 0, normalizedTime);
                childAnimator.Update(0f);
                childAnimator.speed = 0f;
                break;
            }
            yield return null;
        }
        childPauseCoroutine = null;
    }

    private Animator GetChildAnimator(bool activateChild)
    {
        if (!isChild || isChildItem == null) return null;
        if (activateChild && !isChildItem.activeSelf) isChildItem.SetActive(true);
        return isChildItem.GetComponent<Animator>();
    }

    private void CancelChildPause()
    {
        if (childPauseCoroutine == null) return;
        StopCoroutine(childPauseCoroutine);
        childPauseCoroutine = null;
    }

    public void ChildAnimatorDisable()
    {
        if (isChildItem != null)
        {
            Animator animator = isChildItem.GetComponent<Animator>();
            if (animator != null)
            {
                CancelChildPause();
                animator.enabled = false;
            }
        }
    }

    public void NextTask()
    {
        // GENERIC CHECK: Agar inspector mein Pan assign hai (Recipe 2 ka aakhri step)
        if (targetSlideOut != null)
        {
            // Controller par chalane se agar Bowl disable bhi ho jaye tab bhi Pan slide out aur loading screen 100% trigger hogi
            if (SequentialAnimatorController.Instance != null)
            {
                SequentialAnimatorController.Instance.StartCoroutine(SlideTargetAndNextTaskRoutine());
            }
            else
            {
                StartCoroutine(SlideTargetAndNextTaskRoutine());
            }
        }
        else
        {
            // Recipe 1 (Flour, Sugar, Eggs wagera): Purana normal flow bilkul untouched chalega
            if (SequentialAnimatorController.Instance != null)
            {
                SequentialAnimatorController.Instance.UpdateTask();
            }
        }
    }

    private IEnumerator SlideTargetAndNextTaskRoutine()
    {
        // 1. Thoda wait karein taake mixture bowl screen se nikalna shuru ho chuka ho
        if (delayBeforeTargetSlide > 0f)
            yield return new WaitForSeconds(delayBeforeTargetSlide);

        // 2. CONTROLLER KO YAHAN TRIGGER KAREIN (Bina Pan ke bahar jaane ka wait kiye):
        // Is se sparkles isi waqt Pan ke upar chalenge jab Pan table par maujood hai!
        if (SequentialAnimatorController.Instance != null)
        {
            SequentialAnimatorController.Instance.UpdateTask();
        }

        if (targetSlideOut != null)
        {
            Transform targetTransform = targetSlideOut.transform;
            Vector3 originalTargetScale = targetTransform.localScale;

            // 3. Soft & Satisfying Bounce (Sparkles ke sath sath pop karega)
            if (enableTargetBounce)
            {
                targetTransform.DOKill();

                Sequence calmPopSeq = DOTween.Sequence();

                // Soft Pop-Up: Expand hoga
                calmPopSeq.Append(targetTransform.DOScale(originalTargetScale * targetBounceScale, 0.35f).SetEase(Ease.OutSine));

                // Gentle Settle: Softly table par wapis aayega
                calmPopSeq.Append(targetTransform.DOScale(originalTargetScale, 0.45f).SetEase(Ease.OutBack));

                // Completed look aur sparkles enjoy karne ke liye chhota sa pause
                calmPopSeq.AppendInterval(targetWaitBeforeSlideOut);

                // Sequence complete hone tak wait karein
                yield return calmPopSeq.WaitForCompletion();

                targetTransform.localScale = originalTargetScale;
            }

            // 4. Ab Pan screen se bahar slide out ho jaye
            targetSlideOut.enabled = true;
            float panDuration = targetSlideOut.duration > 0f ? targetSlideOut.duration : 0.8f;
            yield return new WaitForSeconds(panDuration);
        }
    }

}
public enum ItemType
{
    None = 0,
    Egg = 1,
    InplaceItem = 2
}