using System;
using System.Collections;
using UnityEngine;
using DG.Tweening;

public class CreamBagDraggable : MonoBehaviour
{
    [Header("--- Target Collider (Cake) ---")]
    [SerializeField] private Collider2D cakeCollider;
    [SerializeField] private string cakeTag = "Cake";

    [Header("--- Upper Cream Decorative Layer ---")]
    [Tooltip("Cake par lagne wali decorative upper cream layer")]
    [SerializeField] private SpriteRenderer upperCreamRenderer;
    [Tooltip("Kitna drag distance chahiye 100% complete karne ke liye")]
    [SerializeField] private float dragDistanceToComplete = 12f;

    [Header("--- Slide In / Out Positions ---")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform readyPointNearCake;
    [SerializeField] private float slideDuration = 0.6f;

    [Header("--- Visual & Drag Polish ---")]
    [SerializeField] private float dragScaleMultiplier = 1.15f;
    [SerializeField] private int dragSortingOrder = 20;
    [SerializeField] private AudioClip pipingSound;

    [Header("--- Screen Boundary Settings ---")]
    [SerializeField] private bool clampToScreen = true;
    [SerializeField] private float wallMargin = 0.05f;

    // Events
    public Action OnCompleted;

    private Vector3 originalLocalPos;
    private Quaternion originalLocalRot;
    private Vector3 originalLocalScale;
    private int originalSortingOrder;

    private bool isDragging = false;
    private bool isOverCake = false;
    private bool isDone = false;
    private float currentProgress = 0f;

    private Camera mainCamera;
    private Vector3 dragOffset;
    private SpriteRenderer myRenderer;
    private Collider2D myCollider;
    private Tween moveTween;
    private Tween scaleTween;

    private void Awake()
    {
        mainCamera = Camera.main;
        myRenderer = GetComponentInChildren<SpriteRenderer>();
        myCollider = GetComponent<Collider2D>();

        if (myRenderer != null) originalSortingOrder = myRenderer.sortingOrder;
        originalLocalPos = transform.localPosition;
        originalLocalRot = transform.localRotation;
        originalLocalScale = transform.localScale;

        if (myCollider != null) myCollider.enabled = false;
    }

    private void Start()
    {
        if (upperCreamRenderer != null)
        {
            Color c = upperCreamRenderer.color;
            c.a = 0f;
            upperCreamRenderer.color = c;
            upperCreamRenderer.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// UI Button click hone par piping bag screen mein cake ke paas enter hogi
    /// </summary>
    public void EnterScreen()
    {
        gameObject.SetActive(true);
        isDone = false;
        currentProgress = 0f;

        if (spawnPoint != null)
            transform.position = spawnPoint.position;

        transform.localScale = originalLocalScale * 0.8f;

        Vector3 targetPos = readyPointNearCake != null ? readyPointNearCake.position : originalLocalPos;

        moveTween?.Kill();
        scaleTween?.Kill();

        scaleTween = transform.DOScale(originalLocalScale, slideDuration).SetEase(Ease.OutBack);
        moveTween = transform.DOMove(targetPos, slideDuration).SetEase(Ease.OutCubic).OnComplete(() =>
        {
            if (myCollider != null) myCollider.enabled = true;
            // Chota sa idle wobble taake user ko pata chale draggable hai
            transform.DOShakePosition(0.4f, 0.15f, 10, 90f, false, true);
        });
    }

    private void OnMouseDown()
    {
        if (isDone) return;

        SoundManager.Instance?.PlaySound(SoundManager.Instance.itemPickupSound);

        if (myRenderer != null) myRenderer.sortingOrder = dragSortingOrder;
        transform.DOScale(originalLocalScale * dragScaleMultiplier, 0.2f).SetEase(Ease.OutBack);

        Vector3 mouseWorldPos = GetMouseWorldPosition();
        dragOffset = transform.position - mouseWorldPos;
        isDragging = true;
    }

    private void OnMouseDrag()
    {
        if (!isDragging || isDone) return;

        Vector3 mouseWorldPos = GetMouseWorldPosition();
        Vector3 targetPos = mouseWorldPos + dragOffset;

        if (clampToScreen && mainCamera != null)
        {
            float camDist = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
            Vector3 bottomLeft = mainCamera.ViewportToWorldPoint(new Vector3(0f, 0f, camDist));
            Vector3 topRight = mainCamera.ViewportToWorldPoint(new Vector3(1f, 1f, camDist));

            targetPos.x = Mathf.Clamp(targetPos.x, bottomLeft.x + 0.4f, topRight.x - 0.4f);
            targetPos.y = Mathf.Clamp(targetPos.y, bottomLeft.y + 0.4f, topRight.y - 0.4f);
        }

        Vector3 prevPos = transform.position;
        transform.position = new Vector3(targetPos.x, targetPos.y, transform.position.z);

        CheckCakeContact();

        if (isOverCake)
        {
            float delta = Vector3.Distance(transform.position, prevPos);
            delta = Mathf.Min(delta, 0.08f);

            if (delta > 0.0005f)
            {
                if (pipingSound != null)
                    SoundManager.Instance?.PlayLoopingSound(pipingSound, 0.6f);

                currentProgress += (delta / dragDistanceToComplete);
                currentProgress = Mathf.Clamp01(currentProgress);

                UpdateUpperCreamVisual(currentProgress);

                if (currentProgress >= 1f)
                {
                    CompletePiping();
                }
            }
        }
        else
        {
            SoundManager.Instance?.StopLoopingSound();
        }
    }

    private void OnMouseUp()
    {
        SoundManager.Instance?.StopLoopingSound();
        if (!isDragging || isDone) return;
        isDragging = false;
        isOverCake = false;

        SoundManager.Instance?.PlaySound(SoundManager.Instance.itemReleaseSound);
        transform.DOScale(originalLocalScale, 0.25f);
    }

    private void CheckCakeContact()
    {
        if (cakeCollider == null) return;
        if (myCollider != null)
            isOverCake = myCollider.IsTouching(cakeCollider);
        else
            isOverCake = cakeCollider.OverlapPoint(transform.position);
    }

    private void UpdateUpperCreamVisual(float progress)
    {
        if (upperCreamRenderer != null)
        {
            Color c = upperCreamRenderer.color;
            c.a = progress;
            upperCreamRenderer.color = c;
        }
    }

    private void CompletePiping()
    {
        isDone = true;
        isDragging = false;
        isOverCake = false;

        SoundManager.Instance?.StopLoopingSound();
        if (myCollider != null) myCollider.enabled = false;

        UpdateUpperCreamVisual(1f);

        // Success celebration sparkle sound
        if (SoundManager.Instance != null && SoundManager.Instance.successSparkleSound != null)
            SoundManager.Instance.PlaySound(SoundManager.Instance.successSparkleSound);

        // Slide out offscreen
        Vector3 exitTarget = spawnPoint != null ? spawnPoint.position : transform.position + new Vector3(8f, 0f, 0f);
        transform.DOMove(exitTarget, 0.65f).SetEase(Ease.InCubic).OnComplete(() =>
        {
            gameObject.SetActive(false);
            OnCompleted?.Invoke();
        });
    }

    private Vector3 GetMouseWorldPosition()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(mouseScreenPos);
        worldPos.z = transform.position.z;
        return worldPos;
    }

    private void OnDisable()
    {
        SoundManager.Instance?.StopLoopingSound();
        moveTween?.Kill();
        scaleTween?.Kill();
    }
}
