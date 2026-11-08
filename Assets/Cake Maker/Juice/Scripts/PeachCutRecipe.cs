using System.Collections;
using UnityEngine;

public sealed class PeachCutRecipe : MonoBehaviour
{
    [SerializeField] private JuiceModeController mode;
    [SerializeField] private SpriteEnterAnimation entrance;
    [SerializeField] private SpriteRenderer knife;
    [SerializeField] private SpriteRenderer peach;
    [SerializeField] private Transform leftHalf;
    [SerializeField] private Transform rightHalf;
    [SerializeField] private SpriteRenderer pointer;
    [SerializeField] private SpriteRenderer gripRipple;
    [SerializeField, Tooltip("Fingertip target in the knife sprite's local coordinates.")]
    private Vector2 handlePoint = new Vector2(0.95f, -0.69f);
    [SerializeField, Range(1f, 1.2f)] private float pickupScale = 1.10f;
    [SerializeField, Min(0)] private float resultHold = 0.95f;

    [Header("Audio")]
    [SerializeField] private AudioClip cutSound;
    [SerializeField] private AudioClip completeSound;

    [Header("Cutting Animation Settings")]
    [SerializeField, Min(0.01f)] private float prepDuration = 0.28f;
    [SerializeField, Min(0.01f)] private float sliceDuration = 0.50f;
    [SerializeField, Min(0.01f)] private float splitDuration = 0.44f;

    [Header("Final Halves Placement")]
    [SerializeField] private float finalSpread = 1.25f;
    [SerializeField] private float finalDrop = 0.06f;
    [SerializeField] private float finalTilt = 14f;

    private const float PickupDuration = 0.14f;
    private const float HintDuration = 2.6f;
    private static readonly Vector3 BladeTip = new Vector3(-2.22f, 1.93f, 0f);
    private static readonly Vector3 BladeHeel = new Vector3(-0.32f, -0.27f, 0f);

    private Transform knifeTransform;
    private Vector2[] knifeOutline;
    private Vector3 restPosition;
    private Quaternion restRotation;
    private Vector3 restScale;
    private Vector3 peachScale;
    private Vector3 peachHomePosition;
    private Color peachColor;

    private Vector3 pointerScale;
    private Quaternion pointerRotation;
    private Vector3 pointerTip;
    private Camera gameplayCamera;
    private AudioSource audioSource;
    private Coroutine motion;
    private Vector3 grabPoint;
    private float pickupTime;
    private int touchId;
    private bool dragging;
    private bool finished;
    private float hintTime;

    private Vector3 leftHalfHomePos;
    private Vector3 rightHalfHomePos;
    private Quaternion leftHalfHomeRot;
    private Quaternion rightHalfHomeRot;

    private void Awake()
    {
        knifeTransform = knife.transform;
        knifeOutline = knife.sprite.vertices;
        restPosition = knifeTransform.localPosition;
        restRotation = knifeTransform.localRotation;
        restScale = knifeTransform.localScale;
        peachScale = peach.transform.localScale;
        peachHomePosition = peach.transform.localPosition;
        peachColor = peach.color;

        pointerScale = pointer.transform.localScale;
        pointerRotation = pointer.transform.localRotation;
        Bounds sprite = pointer.sprite.bounds;
        pointerTip = new Vector3(sprite.min.x + sprite.size.x * 0.04f, sprite.max.y - sprite.size.y * 0.04f, 0f);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        leftHalfHomePos = leftHalf.localPosition;
        rightHalfHomePos = rightHalf.localPosition;
        leftHalfHomeRot = leftHalf.localRotation;
        rightHalfHomeRot = rightHalf.localRotation;
    }

    private void OnEnable()
    {
        gameplayCamera = Camera.main;
        ResetComponents();
    }

    private void ResetComponents()
    {
        knife.flipX = false;
        knifeTransform.localPosition = restPosition;
        knifeTransform.localRotation = restRotation;
        knifeTransform.localScale = restScale;

        ResetPeach();
        peach.gameObject.SetActive(true);
        leftHalf.gameObject.SetActive(false);
        rightHalf.gameObject.SetActive(false);

        leftHalf.localPosition = leftHalfHomePos;
        rightHalf.localPosition = rightHalfHomePos;
        leftHalf.localRotation = leftHalfHomeRot;
        rightHalf.localRotation = rightHalfHomeRot;
        leftHalf.localScale = Vector3.one;
        rightHalf.localScale = Vector3.one;

        HideHint();
        dragging = false;
        finished = false;
        hintTime = 0;
    }

    private void OnDisable()
    {
        HideHint();
        if (motion != null)
            StopCoroutine(motion);
        motion = null;
    }

    private void Update()
    {
        if (entrance != null && !entrance.IsComplete)
            return;
        if (gameplayCamera == null || motion != null || finished)
            return;

        if (dragging)
        {
            UpdateDrag();
            return;
        }

        UpdateHint();
        if (TryPress(out Vector2 screenPosition) && TryProject(screenPosition, out Vector3 worldPosition))
        {
            Vector3 local = knifeTransform.InverseTransformPoint(worldPosition);
            local.z = 0;
            Bounds pickup = knife.sprite.bounds;
            pickup.Expand(new Vector3(0.5f, 0.5f, 0));
            if (!pickup.Contains(local))
                return;

            grabPoint = local;
            pickupTime = 0;
            dragging = true;
            HideHint();

            if (SoundManager.Instance != null && SoundManager.Instance.itemPickupSound != null)
                SoundManager.Instance.PlaySound(SoundManager.Instance.itemPickupSound);
        }
    }

    private bool TryPress(out Vector2 position)
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Began)
                continue;

            touchId = touch.fingerId;
            position = touch.position;
            return true;
        }

        touchId = -1;
        position = Input.mousePosition;
        return Input.touchCount == 0 && Input.GetMouseButtonDown(0);
    }

    private bool TryDragPosition(out Vector2 position, out bool held)
    {
        if (touchId < 0)
        {
            position = Input.mousePosition;
            held = Input.GetMouseButton(0);
            return held || Input.GetMouseButtonUp(0);
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.fingerId != touchId || touch.phase == TouchPhase.Canceled)
                continue;

            position = touch.position;
            held = touch.phase != TouchPhase.Ended;
            return true;
        }

        position = default;
        held = false;
        return false;
    }

    private bool TryProject(Vector2 screen, out Vector3 world)
    {
        Ray ray = gameplayCamera.ScreenPointToRay(screen);
        Plane boardPlane = new Plane(Vector3.forward, knifeTransform.position);
        bool hit = boardPlane.Raycast(ray, out float distance);
        world = ray.GetPoint(distance);
        return hit;
    }

    private void UpdateDrag()
    {
        if (TryDragPosition(out Vector2 screen, out bool held) && TryProject(screen, out Vector3 world))
        {
            pickupTime += Time.deltaTime;
            float lift = Mathf.SmoothStep(0, 1, pickupTime / PickupDuration);
            knifeTransform.localScale = restScale * Mathf.Lerp(1f, pickupScale, lift);

            Vector3 target = world - knifeTransform.TransformVector(grabPoint);
            knifeTransform.position = ClampKnifeToViewport(target);

            if (IsKnifeTriggeredOnPeach(peach.bounds))
            {
                dragging = false;
                motion = StartCoroutine(CutSequence());
                return;
            }

            if (held)
                return;
        }

        dragging = false;
        motion = StartCoroutine(ReturnKnife());
    }

    private bool IsKnifeTriggeredOnPeach(Bounds fruit)
    {
        Vector2 radii = (Vector2)fruit.extents + new Vector2(0.25f, 0.22f);
        Vector2 tip = ((Vector2)(knifeTransform.TransformPoint(BladeTip) - fruit.center)) / radii;
        Vector2 heel = ((Vector2)(knifeTransform.TransformPoint(BladeHeel) - fruit.center)) / radii;
        Vector2 blade = heel - tip;
        float nearest = Mathf.Clamp01(-Vector2.Dot(tip, blade) / blade.sqrMagnitude);
        return (tip + nearest * blade).sqrMagnitude <= 1f;
    }

    private IEnumerator ReturnKnife()
    {
        yield return MoveKnifeHome(0.25f);
        hintTime = 0;
        motion = null;
    }

    private IEnumerator MoveKnifeHome(float duration, float liftHeight = 0f)
    {
        Vector3 fromPos = knifeTransform.position;
        Vector3 fromScale = knifeTransform.localScale;

        Vector3 targetPos = knifeTransform.parent != null ? knifeTransform.parent.TransformPoint(restPosition) : restPosition;
        for (float t = 0; t < duration; t += Time.deltaTime)
        {
            float progress = t / duration;
            float p = Mathf.SmoothStep(0f, 1f, progress);
            float lift = Mathf.Sin(progress * Mathf.PI) * liftHeight;
            knifeTransform.position = Vector3.Lerp(fromPos, targetPos, p) + Vector3.up * lift;
            knifeTransform.localScale = Vector3.Lerp(fromScale, restScale, p);
            yield return null;
        }

        knife.flipX = false;
        knifeTransform.localPosition = restPosition;
        knifeTransform.localRotation = restRotation;
        knifeTransform.localScale = restScale;
    }

    private IEnumerator CutSequence()
    {
        Bounds fruit = peach.bounds;
        Vector3 fromPosition = knifeTransform.position;
        Vector3 fromScale = knifeTransform.localScale;

        // The blade edge sits just left of the sprite pivot at the authored knife angle.
        Vector3 cutStart = new Vector3(fruit.center.x + 0.05f, fruit.max.y + 0.14f, fromPosition.z);
        Vector3 cutEnd = new Vector3(cutStart.x, fruit.center.y - fruit.extents.y * 0.48f, cutStart.z);
        for (float t = 0f; t < prepDuration; t += Time.deltaTime)
        {
            float progress = Mathf.SmoothStep(0f, 1f, t / prepDuration);
            knifeTransform.localScale = Vector3.Lerp(fromScale, restScale, progress);
            knifeTransform.position = Vector3.Lerp(fromPosition, cutStart, progress)
                + Vector3.up * (Mathf.Sin(progress * Mathf.PI) * 0.25f);
            yield return null;
        }

        knifeTransform.localScale = restScale;
        float contactY = fruit.max.y - knifeTransform.TransformVector(BladeHeel).y;
        const float contactProgress = 0.20f;
        bool madeContact = false;
        for (float t = 0f; t < sliceDuration; t += Time.deltaTime)
        {
            float progress = t / sliceDuration;
            float throughFruit = Mathf.InverseLerp(contactProgress, 1f, progress);
            // Ease into the skin, build pressure, then accelerate through the flesh and ease to a stop.
            float depth = throughFruit * throughFruit * throughFruit * (4f - 3f * throughFruit);
            Vector3 cutPosition = cutStart;
            cutPosition.y = progress < contactProgress
                ? Mathf.SmoothStep(cutStart.y, contactY, progress / contactProgress)
                : Mathf.Lerp(contactY, cutEnd.y, depth);
            knifeTransform.position = cutPosition;

            if (!madeContact && progress >= contactProgress)
            {
                madeContact = true;
                PlayCutSound();
            }

            float compression = Mathf.SmoothStep(0f, 1f, throughFruit / 0.32f);
            SetPeachPressure(fruit, compression);
            yield return null;
        }

        knifeTransform.position = cutEnd;
        SetPeachPressure(fruit, 1f);
        if (!madeContact)
            PlayCutSound();

        // The halves open around the knife while it holds the final cut pose.
        SetHalvesPose(fruit, 0f);
        leftHalf.gameObject.SetActive(true);
        rightHalf.gameObject.SetActive(true);

        for (float t = 0f; t < splitDuration; t += Time.deltaTime)
        {
            float progress = t / splitDuration;
            float opening = Mathf.SmoothStep(0f, 1f, progress);
            SetHalvesPose(fruit, opening);

            Color color = peachColor;
            color.a *= 1f - Mathf.SmoothStep(0f, 1f, progress / 0.34f);
            peach.color = color;

            yield return null;
        }

        peach.gameObject.SetActive(false);
        ResetPeach();
        SetHalvesPose(fruit, 1f);
        PlayCompleteSound();

        yield return MoveKnifeHome(0.45f, liftHeight: 0.85f);
        yield return new WaitForSeconds(resultHold);
        finished = true;
        motion = null;

        if (mode != null)
            mode.CompleteRecipe(gameObject);
    }

    private void SetPeachPressure(Bounds fruit, float pressure)
    {
        float squash = 0.045f * pressure;
        peach.transform.localScale = Vector3.Scale(peachScale, new Vector3(1f + squash * 0.5f, 1f - squash, 1f));
        peach.transform.position = fruit.center - Vector3.up * (fruit.extents.y * squash);
    }

    private void ResetPeach()
    {
        peach.transform.localPosition = peachHomePosition;
        peach.transform.localScale = peachScale;
        peach.color = peachColor;
    }

    private void SetHalvesPose(Bounds fruit, float opening)
    {
        float spread = Mathf.Lerp(fruit.extents.x * 0.42f, finalSpread, opening);
        float drop = Mathf.Lerp(0f, finalDrop, opening);
        float tilt = Mathf.Lerp(-5f, finalTilt, opening);
        float width = Mathf.Lerp(0.55f, 0.88f, opening);
        float height = Mathf.Lerp(0.94f, 1f, opening);

        leftHalf.position = fruit.center + new Vector3(-spread, -drop, 0f);
        rightHalf.position = fruit.center + new Vector3(spread, -drop, 0f);
        leftHalf.rotation = Quaternion.Euler(0f, 0f, -tilt);
        rightHalf.rotation = Quaternion.Euler(0f, 0f, tilt);
        leftHalf.localScale = new Vector3(width, height, 1f);
        rightHalf.localScale = leftHalf.localScale;
    }

    private void PlayCutSound()
    {
        if (cutSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(cutSound, 0.95f);
        }
        else if (SoundManager.Instance != null && SoundManager.Instance.slideWhooshSound != null)
        {
            SoundManager.Instance.PlaySound(SoundManager.Instance.slideWhooshSound, 0.85f);
        }
    }

    private void PlayCompleteSound()
    {
        if (completeSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(completeSound, 1f);
        }
        else if (SoundManager.Instance != null && SoundManager.Instance.successSparkleSound != null)
        {
            SoundManager.Instance.PlaySound(SoundManager.Instance.successSparkleSound, 1f);
        }
    }

    private void UpdateHint()
    {
        hintTime = Mathf.Repeat(hintTime + Time.deltaTime, HintDuration);
        Vector3 handle = knifeTransform.TransformPoint(handlePoint);
        Bounds fruit = peach.bounds;
        Vector3 cutStart = fruit.center + Vector3.up * (fruit.extents.y * 0.90f);
        Vector3 cutEnd = fruit.center - Vector3.up * (fruit.extents.y * 0.70f);

        float approach = HintEase(0.08f, 0.30f);
        float grip = HintEase(0.26f, 0.36f) * (1f - HintEase(0.40f, 0.58f));
        float travel = HintEase(0.50f, 1.20f);
        float slice = HintEase(1.25f, 1.95f);
        float recoil = HintEase(1.95f, 2.25f);

        Vector3 hover = handle + new Vector3(0.20f, -0.25f, 0);
        Vector3 handTarget = Vector3.Lerp(Vector3.Lerp(hover, handle, approach), cutStart, travel);
        handTarget.y += Mathf.Sin(travel * Mathf.PI) * 0.30f;

        handTarget = Vector3.Lerp(handTarget, cutEnd, slice);
        handTarget.y += fruit.extents.y * (0.10f * recoil);

        pointer.transform.localScale = pointerScale * (1f - 0.18f * grip);
        pointer.transform.localRotation = pointerRotation * Quaternion.Euler(0, 0, -8f * grip);
        pointer.transform.position = handTarget - pointer.transform.TransformVector(pointerTip);
        SetOpacity(pointer, HintEase(0, 0.15f) * (1f - HintEase(2.20f, 2.50f)));
        pointer.enabled = true;

        if (gripRipple != null)
        {
            float ripple = Mathf.InverseLerp(0.30f, 0.62f, hintTime);
            gripRipple.enabled = ripple > 0f && ripple < 1f;
            if (gripRipple.enabled)
            {
                gripRipple.transform.position = handle;
                gripRipple.transform.localScale = Vector3.one * Mathf.Lerp(0.30f, 0.85f, ripple);
                SetOpacity(gripRipple, (1f - ripple) * 0.9f);
            }
        }
    }

    private float HintEase(float start, float end)
    {
        return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(start, end, hintTime));
    }

    private void HideHint()
    {
        pointer.enabled = false;
        if (gripRipple != null)
            gripRipple.enabled = false;
    }

    private static void SetOpacity(SpriteRenderer sprite, float opacity)
    {
        Color color = sprite.color;
        color.a = opacity;
        sprite.color = color;
    }

    private Vector3 ClampKnifeToViewport(Vector3 target)
    {
        Vector3 viewport = gameplayCamera.WorldToViewportPoint(target);
        Vector2 min = viewport;
        Vector2 max = viewport;

        foreach (Vector2 vertex in knifeOutline)
        {
            Vector2 point = gameplayCamera.WorldToViewportPoint(target + knifeTransform.TransformVector(vertex));
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        viewport.x = ClampViewportAxis(viewport.x, viewport.x - min.x, 1f + viewport.x - max.x);
        viewport.y = ClampViewportAxis(viewport.y, viewport.y - min.y, 1f + viewport.y - max.y);
        return gameplayCamera.ViewportToWorldPoint(viewport);
    }

    private static float ClampViewportAxis(float value, float min, float max)
    {
        return min <= max ? Mathf.Clamp(value, min, max) : (min + max) * 0.5f;
    }
}
