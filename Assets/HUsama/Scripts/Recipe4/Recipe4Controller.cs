using System.Collections;
using UnityEngine;
using DG.Tweening;

public class Recipe4Controller : MonoBehaviour
{
    public static Recipe4Controller Instance { get; private set; }

    [Header("--- References ---")]
    [SerializeField] private GameObject creamBowlParent;
    [SerializeField] private SlideOutObject creamBowlSlideOut;
    [SerializeField] private Transform completedCakeTransform;
    [SerializeField] private SlideOutObject cakeSlideOut;

    [Header("--- Polish & Juice Settings ---")]
    [SerializeField] private float cakePopScaleMultiplier = 1.18f;

    private Vector3 cakeOriginalScale;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (completedCakeTransform != null)
        {
            cakeOriginalScale = completedCakeTransform.localScale;
        }
    }

    public void OnFrostingFinished()
    {
        StartCoroutine(CompleteStageFlowRoutine());
    }

    private IEnumerator CompleteStageFlowRoutine()
    {
        // 1. Thoda wait taake knife bowl mein wapis settle ho jaye
        yield return new WaitForSeconds(0.6f);

        // 2. Cream Bowl screen se bahar slide out ho jaye
        if (creamBowlSlideOut != null)
        {
            creamBowlSlideOut.enabled = true;
            yield return new WaitForSeconds(creamBowlSlideOut.duration > 0f ? creamBowlSlideOut.duration : 0.8f);
        }

        // 3. Sparkles aur Success Chime cake ke theek upar play karein
        if (SequentialAnimatorController.Instance != null && completedCakeTransform != null)
        {
            ParticleSystem[] particles = SequentialAnimatorController.Instance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in particles)
            {
                if (ps != null && (ps.name.ToLower().Contains("complete") || ps.name.ToLower().Contains("sparkle")))
                {
                    ps.transform.position = completedCakeTransform.position + new Vector3(0f, 0.4f, 0f);
                }
            }
        }

        // 4. Finished Frosted Cake ka Soft Pop Bounce (Squash & Stretch Juice)
        if (completedCakeTransform != null)
        {
            completedCakeTransform.DOKill();
            Sequence popSeq = DOTween.Sequence();
            popSeq.Append(completedCakeTransform.DOScale(cakeOriginalScale * cakePopScaleMultiplier, 0.32f).SetEase(Ease.OutSine));
            popSeq.Append(completedCakeTransform.DOScale(cakeOriginalScale, 0.38f).SetEase(Ease.OutBack));
            popSeq.AppendInterval(0.35f);

            yield return popSeq.WaitForCompletion();
            completedCakeTransform.localScale = cakeOriginalScale;
        }

        // 5. Cake Slide Out karein (agar cake bahar bhejna ho)
        if (cakeSlideOut != null)
        {
            cakeSlideOut.enabled = true;
            yield return new WaitForSeconds(cakeSlideOut.duration > 0f ? cakeSlideOut.duration : 0.8f);
        }

        // 6. Central Controller UpdateTask trigger karein
        if (SequentialAnimatorController.Instance != null)
        {
            SequentialAnimatorController.Instance.UpdateTask();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}