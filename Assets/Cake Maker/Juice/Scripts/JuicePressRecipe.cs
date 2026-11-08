using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class JuicePressRecipe : MonoBehaviour
{
    [SerializeField] private JuiceModeController mode;
    [SerializeField] private Transform machine;
    [SerializeField] private Transform jug;
    [SerializeField] private Transform tray;
    [SerializeField] private Transform fruit;
    [SerializeField] private SpriteRenderer[] pieces;
    [SerializeField] private Vector2 hopperPoint = new Vector2(0f, 2.48f);
    [SerializeField] private SpriteRenderer buttonOff;
    [SerializeField] private SpriteRenderer buttonOn;
    [SerializeField] private Transform fruitGuide;
    [SerializeField] private Transform buttonGuide;

    [Header("Entrances and Exits")]
    [SerializeField] private SpriteEnterAnimation machineEnter;
    [SerializeField] private SpriteEnterAnimation jugEnter;
    [SerializeField] private SpriteEnterAnimation trayEnter;
    [SerializeField] private SlideOutObject plateExit;
    [SerializeField] private SlideOutObject machineExit;
    [SerializeField] private SlideOutObject jugExit;

    [Header("Pour")]
    [SerializeField] private Transform outlet;
    [SerializeField] private SpriteRenderer stream;
    [SerializeField] private SpriteRenderer liquid;
    [SerializeField] private ParticleSystem splash;
    [SerializeField] private Vector2 fillRange = new Vector2(-2.12f, 1.50f);
    [SerializeField, Min(0.1f)] private float fillDuration = 2.3f;
    [SerializeField] private AudioClip dropSound;
    [SerializeField] private AudioClip buttonSound;
    [SerializeField] private AudioClip motorSound;
    [SerializeField] private AudioClip completeSound;

    private enum Phase { Busy, Fruit, Dragging, Button }
    private Phase phase;
    private Camera view;
    private AudioSource motor;
    private Vector3 stageScale;
    private Vector3 machineHome;
    private Vector3 jugHome;
    private Vector3 jugScale;
    private Vector3 trayHome;
    private Vector3 plateHome;
    private Vector3 fruitHome;
    private Vector3 fruitScale;
    private Vector3 buttonScale;
    private Vector3[] piecePositions;
    private Vector3[] pieceScales;
    private Quaternion[] pieceRotations;
    private int[] pieceOrders;
    private int fingerId;
    private Vector3 grabPoint;
    private float pickupTime;
    private SpriteRenderer grabbedPiece;
    private float hintTime;

    private void Awake()
    {
        stageScale = transform.localScale;
        machineHome = machine.localPosition;
        jugHome = jug.localPosition;
        jugScale = jug.localScale;
        trayHome = tray.localPosition;
        plateHome = plateExit.transform.localPosition;
        fruitHome = fruit.localPosition;
        fruitScale = fruit.localScale;
        buttonScale = buttonOn.transform.localScale;
        piecePositions = new Vector3[pieces.Length];
        pieceScales = new Vector3[pieces.Length];
        pieceRotations = new Quaternion[pieces.Length];
        pieceOrders = new int[pieces.Length];
        for (int i = 0; i < pieces.Length; i++)
        {
            Transform piece = pieces[i].transform;
            piecePositions[i] = piece.localPosition;
            pieceScales[i] = piece.localScale;
            pieceRotations[i] = piece.localRotation;
            pieceOrders[i] = pieces[i].sortingOrder;
        }

        motor = gameObject.AddComponent<AudioSource>();
        motor.playOnAwake = false;
        motor.loop = true;
        motor.clip = motorSound;
        motor.volume = 0.55f;

        ParticleSystem.MainModule droplets = splash.main;
        droplets.prewarm = false;
        droplets.startColor = Color.Lerp(liquid.color, Color.white, 0.25f);
        ParticleSystem.EmissionModule emission = splash.emission;
        emission.rateOverTime = 22f;
        emission.rateOverDistance = 0f;
    }

    private void OnEnable()
    {
        view = Camera.main;
        if (view == null)
            return;

        phase = Phase.Busy;
        plateExit.enabled = false;
        machineExit.enabled = false;
        jugExit.enabled = false;
        plateExit.transform.localPosition = plateHome;
        plateExit.gameObject.SetActive(true);
        machine.gameObject.SetActive(true);
        jug.gameObject.SetActive(true);
        jug.localScale = jugScale;
        fruit.localPosition = fruitHome;
        fruit.localScale = fruitScale;
        for (int i = 0; i < pieces.Length; i++)
        {
            SpriteRenderer piece = pieces[i];
            piece.transform.SetLocalPositionAndRotation(piecePositions[i], pieceRotations[i]);
            piece.transform.localScale = pieceScales[i];
            piece.sortingOrder = pieceOrders[i];
            piece.enabled = true;
        }

        buttonOff.enabled = true;
        buttonOn.enabled = false;
        buttonOn.transform.localScale = buttonScale;
        SetGuides(false, false);
        SetFill(0f, 0f);
        stream.enabled = false;
        splash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        StartCoroutine(Enter());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        plateExit.enabled = false;
        machineExit.enabled = false;
        jugExit.enabled = false;
        motor.Stop();
        splash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private IEnumerator Enter()
    {
        Vector3 machineTarget = machineHome;
        Vector3 jugTarget = jugHome;
        Vector3 trayTarget = trayHome;

        float halfWidth = view.orthographicSize * view.aspect - 0.35f;
        trayTarget.x = Mathf.Clamp(halfWidth - 2.7f, machineHome.x + 5.2f, trayHome.x);
        float left = machineTarget.x - 2.6f;
        float right = trayTarget.x + 2.7f;
        float fit = Mathf.Min(1f, 2f * halfWidth / (right - left));
        float shift = Mathf.Max(0f, right - halfWidth / fit);
        machineTarget.x -= shift;
        jugTarget.x -= shift;
        trayTarget.x -= shift;
        transform.localScale = stageScale * fit;

        machineEnter.PlayTo(transform.TransformPoint(machineTarget));
        jugEnter.PlayTo(transform.TransformPoint(jugTarget));
        trayEnter.PlayTo(transform.TransformPoint(trayTarget));
        yield return new WaitUntil(() => machineEnter.IsComplete && jugEnter.IsComplete && trayEnter.IsComplete);
        phase = Phase.Fruit;
        SetGuides(true, false);
    }

    private void Update()
    {
        if (view == null || phase == Phase.Busy)
            return;

        if (phase == Phase.Dragging)
        {
            UpdateDrag();
            return;
        }

        UpdateGuide();
        if (!TryPress(out Vector2 screen) || !TryProject(screen, out Vector3 point))
            return;

        if (phase == Phase.Button)
        {
            if (Hits(buttonOff, point, 0.55f))
            {
                phase = Phase.Busy;
                SetGuides(false, false);
                StartCoroutine(Juice());
            }
            return;
        }

        foreach (SpriteRenderer piece in pieces)
        {
            if (!Hits(piece, point, 0.25f))
                continue;

            grabPoint = fruit.InverseTransformPoint(point);
            pickupTime = 0f;
            grabbedPiece = piece;
            phase = Phase.Dragging;
            SetGuides(false, false);
            for (int i = 0; i < pieces.Length; i++)
                pieces[i].sortingOrder = 12 + i;
            MoveDraggedFruit(point);
            if (SoundManager.Instance != null)
                SoundManager.Instance.PlaySound(SoundManager.Instance.itemPickupSound);
            return;
        }
    }

    private void UpdateDrag()
    {
        if (TrackPointer(out Vector2 screen, out bool held) && TryProject(screen, out Vector3 point))
        {
            pickupTime += Time.deltaTime;
            MoveDraggedFruit(point);
            if (held)
                return;

            if (IsInHopper(point) || IsInHopper(grabbedPiece.bounds.center))
            {
                phase = Phase.Busy;
                StartCoroutine(LoadFruit());
                return;
            }
        }

        phase = Phase.Busy;
        StartCoroutine(ReturnFruit());
    }

    private void MoveDraggedFruit(Vector3 point)
    {
        float pickup = 1.08f + 0.04f * Ease(0f, 0.10f, pickupTime);
        float bounce = 0.04f * (Ease(0f, 0.08f, pickupTime) - Ease(0.08f, 0.20f, pickupTime));
        fruit.localScale = fruitScale * (pickup + bounce);

        Bounds bounds = pieces[0].bounds;
        for (int i = 1; i < pieces.Length; i++)
            bounds.Encapsulate(pieces[i].bounds);

        Vector3 pivot = view.WorldToViewportPoint(fruit.position);
        Vector3 min = view.WorldToViewportPoint(bounds.min);
        Vector3 max = view.WorldToViewportPoint(bounds.max);
        Vector3 viewport = view.WorldToViewportPoint(point - fruit.TransformVector(grabPoint));
        viewport.x = ClampViewportAxis(viewport.x, pivot.x - min.x, 1f + pivot.x - max.x);
        viewport.y = ClampViewportAxis(viewport.y, pivot.y - min.y, 1f + pivot.y - max.y);
        fruit.position = view.ViewportToWorldPoint(viewport);
    }

    private static float ClampViewportAxis(float value, float min, float max) =>
        min <= max ? Mathf.Clamp(value, min, max) : (min + max) * 0.5f;

    private bool IsInHopper(Vector3 position)
    {
        Vector2 relative = (Vector2)machine.InverseTransformPoint(position) - hopperPoint;
        return Mathf.Abs(relative.x) <= 1.9f && relative.y >= -0.75f && relative.y <= 2f;
    }

    private IEnumerator ReturnFruit()
    {
        Vector3 start = fruit.localPosition;
        Vector3 startScale = fruit.localScale;
        const float duration = 0.24f;
        for (float time = 0f; time < duration; time += Time.deltaTime)
        {
            float progress = Ease(0f, duration, time);
            fruit.localPosition = Vector3.Lerp(start, fruitHome, progress);
            fruit.localScale = Vector3.Lerp(startScale, fruitScale, progress);
            yield return null;
        }
        fruit.localPosition = fruitHome;
        fruit.localScale = fruitScale;
        for (int i = 0; i < pieces.Length; i++)
            pieces[i].sortingOrder = pieceOrders[i];
        phase = Phase.Fruit;
        SetGuides(true, false);
    }

    private IEnumerator LoadFruit()
    {
        Vector3 start = fruit.position;
        Vector3 startScale = fruit.localScale;
        Vector3 hopper = machine.TransformPoint(hopperPoint);
        Vector3 above = hopper + machine.TransformVector(Vector3.up * 1.15f);
        const float alignDuration = 0.24f;
        for (float time = 0f; time < alignDuration; time += Time.deltaTime)
        {
            float p = Ease(0f, alignDuration, time);
            fruit.position = Vector3.Lerp(start, above, p) + Vector3.up * (Mathf.Sin(p * Mathf.PI) * 0.25f);
            fruit.localScale = Vector3.Lerp(startScale, fruitScale, p);
            yield return null;
        }
        fruit.position = above;
        fruit.localScale = fruitScale;

        for (int i = 0; i < pieces.Length; i++)
        {
            SpriteRenderer piece = pieces[i];
            Transform item = piece.transform;
            Vector3 from = item.position;
            float side = Mathf.Clamp(piecePositions[i].x * 0.74f, -0.85f, 0.85f);
            Vector3 into = hopper + machine.TransformVector(new Vector3(side, -0.22f));
            Quaternion fromRotation = item.rotation;
            Quaternion landedRotation = fromRotation * Quaternion.Euler(0f, 0f, -30f * side);
            Vector3 landedScale = pieceScales[i] * 0.67f;
            piece.sortingOrder = 4; // The lip occludes the lower portion of each landed piece.
            const float dropDuration = 0.32f;
            bool landed = false;
            for (float time = 0f; time < dropDuration; time += Time.deltaTime)
            {
                float p = time / dropDuration;
                float fall = Mathf.Min(1f, p / 0.76f);
                float bounce = Mathf.Sin(Mathf.InverseLerp(0.76f, 1f, p) * Mathf.PI) * 0.12f;
                item.position = Vector3.Lerp(from, into, fall * fall) + machine.TransformVector(Vector3.up * bounce);
                item.rotation = Quaternion.Slerp(fromRotation, landedRotation, fall);
                item.localScale = Vector3.Lerp(pieceScales[i], landedScale, fall);
                if (!landed && fall >= 1f)
                {
                    landed = true;
                    PlayCue(dropSound, 0.65f);
                }
                yield return null;
            }
            item.SetPositionAndRotation(into, landedRotation);
            item.localScale = landedScale;
            if (!landed)
                PlayCue(dropSound, 0.65f);
        }

        yield return SlideOut(plateExit);
        phase = Phase.Button;
        SetGuides(false, true);
    }

    private IEnumerator FeedFruit(Vector3 machineStart)
    {
        foreach (SpriteRenderer piece in pieces)
        {
            Transform item = piece.transform;
            Vector3 start = machine.InverseTransformPoint(item.position);
            Vector3 scale = item.localScale;
            Quaternion rotation = item.rotation;
            Vector3 inside = new Vector3(hopperPoint.x, hopperPoint.y - 1.5f, start.z);
            const float duration = 0.49f;
            for (float time = 0f; time < duration; time += Time.deltaTime)
            {
                float progress = time / duration;
                float crush = Ease(0f, 0.65f, progress);
                float feed = Ease(0.24f, 1f, progress);
                float shake = Mathf.Sin(progress * 8f * Mathf.PI) * Mathf.Sin(progress * Mathf.PI);
                machine.localPosition = machineStart + Vector3.right * (shake * 0.045f);
                item.position = machine.TransformPoint(Vector3.Lerp(start, inside, feed));
                item.rotation = rotation * Quaternion.Euler(0f, 0f, Mathf.Sin(progress * Mathf.PI) * 10f);
                item.localScale = Vector3.Scale(scale, new Vector3(1f + 0.12f * crush - 0.37f * feed, 1f - 0.55f * crush, 1f));
                yield return null;
            }
            piece.enabled = false;
        }
        machine.localPosition = machineStart;
    }

    private IEnumerator Juice()
    {
        buttonOff.enabled = false;
        buttonOn.enabled = true;
        buttonOn.transform.localScale = buttonScale * 0.89f;
        PlayCue(buttonSound, 0.7f);
        Vector3 machineStart = machine.localPosition;
        const float primeDuration = 0.20f;
        for (float time = 0f; time < primeDuration; time += Time.deltaTime)
        {
            buttonOn.transform.localScale = Vector3.Lerp(buttonScale * 0.89f, buttonScale, Ease(0f, 0.16f, time));
            yield return null;
        }
        buttonOn.transform.localScale = buttonScale;
        if (motorSound != null)
            motor.Play();

        yield return FeedFruit(machineStart);
        const float streamStart = 0.18f;
        for (float time = 0f; time < streamStart; time += Time.deltaTime)
        {
            float p = time / streamStart;
            Vector3 top = jug.InverseTransformPoint(outlet.position);
            SetStream(top.y, Mathf.Lerp(top.y, fillRange.x, p * p), top.x, 0.16f);
            yield return null;
        }

        splash.Play();
        for (float time = 0f; time < fillDuration; time += Time.deltaTime)
        {
            float progress = time / fillDuration;
            float fill = progress * (0.84f + 0.16f * progress);
            float wave = Mathf.Sin(time * 12f) * Mathf.Sin(progress * Mathf.PI);
            machine.localPosition = machineStart + Vector3.right * (Mathf.Sin(time * 65f) * 0.026f);
            SetFill(fill, wave);
            Vector3 top = jug.InverseTransformPoint(outlet.position);
            SetStream(top.y, Surface(fill), top.x, 0.18f + 0.025f * Mathf.Sin(time * 23f));
            yield return null;
        }
        machine.localPosition = machineStart;
        SetFill(1f, 0f);
        buttonOn.enabled = false;
        buttonOff.enabled = true;

        const float tailDuration = 0.17f;
        Vector3 outletLocal = jug.InverseTransformPoint(outlet.position);
        for (float time = 0f; time < tailDuration; time += Time.deltaTime)
        {
            float p = time / tailDuration;
            SetStream(Mathf.Lerp(outletLocal.y, fillRange.y, p * p), fillRange.y, outletLocal.x, 0.16f * (1f - p));
            yield return null;
        }
        stream.enabled = false;
        splash.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        motor.Stop();
        PlayCue(completeSound, 0.85f);

        yield return new WaitForSeconds(0.25f);
        yield return SlideOut(machineExit);
        yield return ShowcaseJug();
        yield return SlideOut(jugExit);
        if (mode != null)
            mode.CompleteRecipe(gameObject);
    }

    private void PlayCue(AudioClip clip, float volume)
    {
        if (clip != null)
            motor.PlayOneShot(clip, volume);
    }

    private void SetFill(float fill, float wave)
    {
        float surface = Surface(fill);
        // Extend the colored sprite below the glass. The jug silhouette clips the sides and base.
        float bottom = fillRange.x - 0.55f;
        float height = surface - bottom;
        Transform level = liquid.transform;
        level.localPosition = new Vector3(level.localPosition.x, bottom + height * 0.5f, level.localPosition.z);
        level.localScale = new Vector3(3.3f / liquid.sprite.bounds.size.x, height / liquid.sprite.bounds.size.y, 1f);
        level.localRotation = Quaternion.Euler(0f, 0f, wave * 1.5f);
        liquid.enabled = fill > 0f;

        Vector3 emitter = splash.transform.localPosition;
        emitter.y = surface + 0.06f;
        splash.transform.localPosition = emitter;
    }

    private float Surface(float fill) => Mathf.Lerp(fillRange.x, fillRange.y, fill);

    private void SetStream(float top, float bottom, float x, float width)
    {
        float height = Mathf.Max(0f, top - bottom);
        Transform jet = stream.transform;
        jet.localPosition = new Vector3(x, bottom + height * 0.5f, 0f);
        jet.localScale = new Vector3(width / stream.sprite.bounds.size.x, height / stream.sprite.bounds.size.y, 1f);
        stream.enabled = height > 0.01f;
    }

    private static IEnumerator SlideOut(SlideOutObject exit)
    {
        exit.enabled = true;
        yield return new WaitUntil(() => !exit.gameObject.activeSelf);
        exit.enabled = false;
    }

    private IEnumerator ShowcaseJug()
    {
        Vector3 start = jug.position;
        float depth = view.WorldToViewportPoint(start).z;
        Vector3 center = view.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, depth));
        const float moveDuration = 0.45f;
        for (float time = 0f; time < moveDuration; time += Time.deltaTime)
        {
            jug.position = Vector3.Lerp(start, center, Ease(0f, moveDuration, time));
            yield return null;
        }
        jug.position = center;

        const float popDuration = 0.36f;
        for (float time = 0f; time < popDuration; time += Time.deltaTime)
        {
            float pop = Mathf.Sin(Ease(0f, popDuration, time) * Mathf.PI) * 0.18f;
            jug.localScale = jugScale * (1f + pop);
            yield return null;
        }
        jug.localScale = jugScale;
        yield return new WaitForSeconds(0.30f);
    }

    private void SetGuides(bool showFruit, bool showButton)
    {
        hintTime = 0f;
        fruitGuide.gameObject.SetActive(showFruit);
        buttonGuide.gameObject.SetActive(showButton);
        if (showFruit)
            fruitGuide.position = FruitGuideStart();
        if (showButton)
            buttonGuide.position = buttonOff.bounds.center;
    }

    private Vector3 FruitGuideStart() => pieces[0].bounds.center;

    private void UpdateGuide()
    {
        if (phase != Phase.Fruit)
            return;

        hintTime = Mathf.Repeat(hintTime + Time.deltaTime, 2.8f);
        Vector3 from = FruitGuideStart();
        Vector3 target = machine.TransformPoint(hopperPoint);
        float travel = Ease(0.55f, 1.55f, hintTime);
        Vector3 position = Vector3.Lerp(from, target, travel) + Vector3.up * (Mathf.Sin(travel * Mathf.PI) * 0.8f);
        fruitGuide.position = Vector3.Lerp(position, from, Ease(1.9f, 2.7f, hintTime));
    }

    private static float Ease(float start, float end, float time) =>
        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, time));

    private static bool Hits(SpriteRenderer sprite, Vector3 point, float margin)
    {
        Bounds bounds = sprite.bounds;
        bounds.Expand(margin * 2f);
        point.z = bounds.center.z;
        return bounds.Contains(point);
    }

    private bool TryProject(Vector2 screen, out Vector3 point)
    {
        Ray ray = view.ScreenPointToRay(screen);
        Plane plane = new Plane(Vector3.forward, fruit.position);
        bool hit = plane.Raycast(ray, out float distance);
        point = ray.GetPoint(distance);
        return hit;
    }

    private bool TryPress(out Vector2 screen)
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase == TouchPhase.Began &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject(touch.fingerId)))
            {
                fingerId = touch.fingerId;
                screen = touch.position;
                return true;
            }
        }
        fingerId = -1;
        screen = Input.mousePosition;
        return Input.touchCount == 0 && Input.GetMouseButtonDown(0) &&
            (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject());
    }

    private bool TrackPointer(out Vector2 screen, out bool held)
    {
        if (fingerId < 0)
        {
            screen = Input.mousePosition;
            held = Input.GetMouseButton(0);
            return held || Input.GetMouseButtonUp(0);
        }
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.fingerId == fingerId && touch.phase != TouchPhase.Canceled)
            {
                screen = touch.position;
                held = touch.phase != TouchPhase.Ended;
                return true;
            }
        }
        screen = default;
        held = false;
        return false;
    }
}
