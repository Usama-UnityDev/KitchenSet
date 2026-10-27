using System.Collections;
using UnityEngine;
using DG.Tweening;

public class Recipe3Controller : MonoBehaviour
{
    public static Recipe3Controller Instance;

    [Header("--- 1. Oven References ---")]
    [SerializeField] private Animator ovenAnimator;
    [SerializeField] private BoxCollider2D ovenDoorCollider;
    [SerializeField] private string doorOpenClipName = "DoorOpen";
    [SerializeField] private string doorCloseClipName = "Oven_Door_TapPrompt";
    [SerializeField] private SlideOutObject ovenSlideOut;

    [Header("--- 2. Hand Pointers ---")]
    [SerializeField] private GameObject ovenDoorHand;
    [SerializeField] private GameObject panPointingHand;
    [SerializeField] private GameObject cakeTakeOutHand;

    [Header("--- 3. Bake Pan & Cake Visuals ---")]
    [SerializeField] private BakePanDraggable bakePanDraggable;
    [SerializeField] private SpriteRenderer mixRenderer;
    [SerializeField] private SpriteRenderer bakedRenderer;
    [SerializeField] private ParticleSystem panSteamParticles;
    [SerializeField] private SlideOutObject panSlideOut;

    [Header("--- 4. Clock / Timer ---")]
    [SerializeField] private GameObject clockObject;
    [SerializeField] private Animator clockAnimator;

    [Header("--- 5. Particles & Effects ---")]
    [SerializeField] private ParticleSystem ovenSmokeParticles;

    [Header("--- 6. Motion & Juice Settings ---")]
    [Tooltip("Cake oven se table par aane ka smooth time")]
    [SerializeField] private float cakeExitDuration = 0.9f;
    [Tooltip("Pan table par aane ke baad pop bounce scale")]
    [SerializeField] private float cakeBounceScaleMultiplier = 1.2f;

    public bool isDoorOpened { get; private set; } = false;
    private Vector3 panTablePosition;
    private Vector3 panTableScale;

    private Sequence handDragSequence;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (ovenDoorCollider != null) ovenDoorCollider.enabled = true;
        if (cakeTakeOutHand != null) cakeTakeOutHand.SetActive(false);
        if (panSteamParticles != null) panSteamParticles.Stop();

        if (bakedRenderer != null)
        {
            Color c = bakedRenderer.color;
            c.a = 0f;
            bakedRenderer.color = c;
            bakedRenderer.gameObject.SetActive(true);
        }

        if (mixRenderer != null)
        {
            Color c = mixRenderer.color;
            c.a = 1f;
            mixRenderer.color = c;
            mixRenderer.gameObject.SetActive(true);
        }
    }

    public void OpenOvenDoor()
    {
        if (isDoorOpened) return;
        isDoorOpened = true;

        SoundManager.Instance?.PlaySound(SoundManager.Instance.itemPickupSound);

        if (ovenDoorCollider != null) ovenDoorCollider.enabled = false;
        if (ovenDoorHand != null) ovenDoorHand.SetActive(false);

        if (ovenAnimator != null && !string.IsNullOrEmpty(doorOpenClipName))
        {
            ovenAnimator.Play(doorOpenClipName, 0, 0f);
        }

        StartCoroutine(SwitchHandToPanRoutine());
    }

    private IEnumerator SwitchHandToPanRoutine()
    {
        yield return new WaitForSeconds(0.45f);

        if (panPointingHand != null)
        {
            panPointingHand.SetActive(true);
            Animator anim = panPointingHand.GetComponent<Animator>();
            if (anim != null) anim.enabled = true;
        }

        if (bakePanDraggable != null)
        {
            Collider2D col = bakePanDraggable.GetComponent<Collider2D>();
            if (col != null) col.enabled = true;
        }
    }

    public void OnBakePanEnteredOven()
    {
        if (bakePanDraggable != null)
        {
            panTablePosition = bakePanDraggable.OriginalTablePosition;
            panTableScale = bakePanDraggable.OriginalScale;
        }

        StartCoroutine(BakingSequenceRoutine());
    }

    private IEnumerator BakingSequenceRoutine()
    {
        if (panPointingHand != null) panPointingHand.SetActive(false);
        yield return new WaitForSeconds(1.2f);

        // 1. Oven door band karein
        if (ovenAnimator != null && !string.IsNullOrEmpty(doorCloseClipName))
        {
            ovenAnimator.Play(doorCloseClipName, 0, 0f);
        }
        yield return new WaitForSeconds(0.3f);

        // 2. Clock Slide In
        if (clockObject != null)
        {
            clockObject.SetActive(true);
            SpriteEnterAnimation enterAnim = clockObject.GetComponent<SpriteEnterAnimation>();
            if (enterAnim != null) enterAnim.enabled = true;
        }
        yield return new WaitForSeconds(1.0f);

        // 3. Baking CrossFade
        if (clockAnimator != null) clockAnimator.enabled = true;

        float bakeTime = 3.0f;
        float elapsed = 0f;

        while (elapsed < bakeTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / bakeTime;

            if (mixRenderer != null)
            {
                Color mixCol = mixRenderer.color;
                mixCol.a = Mathf.Lerp(1f, 0f, t);
                mixRenderer.color = mixCol;
            }

            if (bakedRenderer != null)
            {
                Color bakedCol = bakedRenderer.color;
                bakedCol.a = Mathf.Lerp(0f, 1f, t);
                bakedRenderer.color = bakedCol;
            }

            yield return null;
        }

        if (mixRenderer != null) mixRenderer.gameObject.SetActive(false);
        if (clockAnimator != null) clockAnimator.enabled = false;

        // 4. Timer Bell + Clock Slide Out
        SoundManager.Instance?.PlaySound(SoundManager.Instance.itemPickupSound);

        if (clockObject != null)
        {
            SlideOutObject clockSlide = clockObject.GetComponent<SlideOutObject>();
            if (clockSlide != null) clockSlide.enabled = true;
        }
        yield return new WaitForSeconds(0.9f);

        // 5. Oven Door Re-Open
        if (ovenAnimator != null && !string.IsNullOrEmpty(doorOpenClipName))
        {
            ovenAnimator.Play(doorOpenClipName, 0, 0f);
        }
        yield return new WaitForSeconds(0.35f);

        // 6. Oven Smoke Particles
        if (ovenSmokeParticles != null) ovenSmokeParticles.Play();

        // 7. Hand Drag Guide Start
        PlayTakeOutHandGuide();

        if (bakePanDraggable != null)
        {
            bakePanDraggable.EnableTakeOutMode();
        }
    }

    public void OnCakePanTappedToExit()
    {
        if (handDragSequence != null)
        {
            handDragSequence.Kill();
            handDragSequence = null;
        }

        if (cakeTakeOutHand != null)
        {
            cakeTakeOutHand.transform.DOKill();
            cakeTakeOutHand.SetActive(false);
        }

        StartCoroutine(CakeExitFlowRoutine());
    }

    private IEnumerator CakeExitFlowRoutine()
    {
        if (cakeTakeOutHand != null)
        {
            cakeTakeOutHand.transform.DOKill();
            cakeTakeOutHand.SetActive(false);
        }

        // -------------------------------------------------------------
        // STEP 1: Pan Oven se Table par SMOOTH slide kare (Snap Glitch Fix)
        // -------------------------------------------------------------
        if (bakePanDraggable != null)
        {
            Transform panTransform = bakePanDraggable.transform;

            // Oven ke andar ki actual position capture karein
            Vector3 ovenPos = panTransform.position;
            Vector3 ovenScale = panTransform.localScale;

            // Animator band karein
            Animator panAnim = bakePanDraggable.GetComponent<Animator>();
            if (panAnim != null) panAnim.enabled = false;

            // Mecanim ke sudden reset ko rokne ke liye position re-enforce karein
            panTransform.position = ovenPos;
            panTransform.localScale = ovenScale;
            panTransform.DOKill();

            // Smoothly Oven se Table par lerp karein
            Sequence exitSeq = DOTween.Sequence();
            exitSeq.Append(panTransform.DOMove(panTablePosition, cakeExitDuration).SetEase(Ease.OutCubic));
            exitSeq.Join(panTransform.DOScale(panTableScale, cakeExitDuration).SetEase(Ease.OutCubic));

            yield return exitSeq.WaitForCompletion();

            panTransform.position = panTablePosition;
            panTransform.localScale = panTableScale;
        }

        // STEP 2: Steam start & Smoke stop
        if (panSteamParticles != null) panSteamParticles.Play();
        if (ovenSmokeParticles != null) ovenSmokeParticles.Stop();
        yield return new WaitForSeconds(0.3f);

        // STEP 3: Oven Door Close
        if (ovenAnimator != null && !string.IsNullOrEmpty(doorCloseClipName))
        {
            ovenAnimator.Play(doorCloseClipName, 0, 0f);
        }
        yield return new WaitForSeconds(0.35f);

        // STEP 4: Oven Slide Out karein
        if (ovenSlideOut != null)
        {
            ovenSlideOut.enabled = true;
            ovenSlideOut.gameObject.SetActive(true);
            yield return new WaitForSeconds(ovenSlideOut.duration > 0f ? ovenSlideOut.duration : 0.8f);
        }

        // ----------------------------------------------------------------------------------
        // STEP 5: Oven ja chuka hai! Cake Pan Table par hai -> Sparkles isi par phootengi!
        // ----------------------------------------------------------------------------------
        if (SequentialAnimatorController.Instance != null)
        {
            ParticleSystem[] particles = SequentialAnimatorController.Instance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in particles)
            {
                if (ps != null && ps.name.ToLower().Contains("complete"))
                {
                    ps.transform.position = panTablePosition + new Vector3(0f, 0.4f, 0f);
                }
            }

            // Task update call hoga -> Sparkles cake pan ke upar hi chalengi!
            SequentialAnimatorController.Instance.UpdateTask();
        }

        // ----------------------------------------------------------------------------------
        // STEP 6: Sparkles ke sath Cake Pan ka Soft Pop Bounce
        // ----------------------------------------------------------------------------------
        if (bakePanDraggable != null)
        {
            Transform panTransform = bakePanDraggable.transform;
            panTransform.DOKill();

            Sequence calmPopSeq = DOTween.Sequence();
            calmPopSeq.Append(panTransform.DOScale(panTableScale * cakeBounceScaleMultiplier, 0.3f).SetEase(Ease.OutSine));
            calmPopSeq.Append(panTransform.DOScale(panTableScale, 0.35f).SetEase(Ease.OutBack));
            calmPopSeq.AppendInterval(0.15f);

            yield return calmPopSeq.WaitForCompletion();
            panTransform.localScale = panTableScale;
        }

        // ----------------------------------------------------------------------------------
        // STEP 7: Pan Screen se bahir slide out ho jaye
        // ----------------------------------------------------------------------------------
        if (panSlideOut != null)
        {
            panSlideOut.gameObject.SetActive(true);
            panSlideOut.enabled = true;
        }
     
    }

    private void PlayTakeOutHandGuide()
    {
        if (cakeTakeOutHand == null || bakePanDraggable == null) return;

        Animator anim = cakeTakeOutHand.GetComponent<Animator>();
        if (anim != null) anim.enabled = false;

        cakeTakeOutHand.SetActive(true);
        Transform hTransform = cakeTakeOutHand.transform;
        hTransform.DOKill();

        Vector3 startWorldPos = bakePanDraggable.transform.position + new Vector3(0.3f, 0.2f, 0f);
        Vector3 endWorldPos = panTablePosition + new Vector3(0.3f, 0.4f, 0f);

        Vector3 baseScale = Vector3.one;
        hTransform.position = startWorldPos;
        hTransform.rotation = Quaternion.Euler(0f, 0f, -15f);
        hTransform.localScale = baseScale;

        handDragSequence = DOTween.Sequence();
        handDragSequence.Append(hTransform.DOScale(baseScale * 0.85f, 0.2f).SetEase(Ease.OutQuad));
        handDragSequence.AppendInterval(0.1f);
        handDragSequence.Append(hTransform.DOMove(endWorldPos, 0.65f).SetEase(Ease.InOutSine));
        handDragSequence.Append(hTransform.DOScale(baseScale, 0.15f).SetEase(Ease.OutQuad));
        handDragSequence.AppendInterval(0.25f);
        handDragSequence.AppendCallback(() =>
        {
            hTransform.position = startWorldPos;
        });

        handDragSequence.SetLoops(-1);
    }

    private void OnDestroy()
    {
        if (handDragSequence != null)
        {
            handDragSequence.Kill();
        }
    }
}