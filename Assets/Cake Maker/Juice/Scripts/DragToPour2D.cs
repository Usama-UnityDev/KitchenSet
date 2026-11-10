using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D), typeof(Rigidbody2D), typeof(SpriteEnterAnimation))]
public sealed class DragToPour2D : MonoBehaviour
{
    [SerializeField] private Collider2D receiver;
    [SerializeField] private Animator idleAnimator;
    [SerializeField, Min(1f)] private float pickupScale = 1.12f;

    [Header("Pour")]
    [SerializeField] private Transform spout;
    [SerializeField] private Material streamMaterial;
    [SerializeField, Min(0.01f)] private float streamWidth = 0.34f;
    [SerializeField] private ParticleSystem splash;
    [SerializeField] private SpriteMask sourceMask;
    [SerializeField] private SpriteRenderer sourceLiquid;
    [SerializeField, Tooltip("Path along the inside wall from the belly to the lip, in mask-local coordinates.")]
    private Vector2[] sourceFlowPath;
    [SerializeField, Range(0.1f, 1f)] private float sourceStartLevel = 0.78f;
    [SerializeField] private SpriteRenderer receiverLiquid;
    [SerializeField, Range(0.5f, 1f)] private float receiverBaseWidth = 1f;
    [SerializeField] private float maximumTilt = -100f;
    [SerializeField, Min(0.1f)] private float spoutClearance = 1f;
    [SerializeField, Min(0.1f)] private float fillDuration = 1.65f;
    [SerializeField] private SlideOutObject jugExit;
    [SerializeField] private UnityEvent poured;

    private const float SpoutContactDepth = 0.1f;

    private enum Phase { Entering, Ready, Dragging, Busy }

    private Phase phase;
    private Camera view;
    private Collider2D pickup;
    private SpriteEnterAnimation entrance;
    private Tween motion;
    private Vector3 authoredPosition;
    private Vector3 home;
    private Quaternion restRotation;
    private Vector3 restScale;
    private Vector3 spoutLocal;
    private Vector3 grabOffset;
    private Vector2 pressScreen;
    private int fingerId;
    private Bounds pickupBounds;
    private Vector3 liquidPosition;
    private Vector3 liquidScale;
    private Bounds liquidSpriteBounds;
    private Vector3 splashPosition;
    private LineRenderer stream;
    private LineRenderer sourceFlow;
    private Vector3[] sourceFlowPoints;
    private float sourceFlowStrength;
    private LiquidMaskArea sourceArea;
    private float sourceStartArea;
    private float drained;

    private void Awake()
    {
        pickup = GetComponent<Collider2D>();
        entrance = GetComponent<SpriteEnterAnimation>();
        authoredPosition = transform.localPosition;
        restRotation = transform.localRotation;
        restScale = transform.localScale;
        liquidPosition = receiverLiquid.transform.localPosition;
        liquidScale = receiverLiquid.transform.localScale;
        liquidSpriteBounds = receiverLiquid.sprite.bounds;
        splashPosition = splash.transform.localPosition;
        stream = spout.gameObject.AddComponent<LineRenderer>();
        stream.enabled = false;
        stream.sharedMaterial = streamMaterial;
        stream.useWorldSpace = true;
        stream.sortingLayerID = receiverLiquid.sortingLayerID;
        stream.sortingOrder = receiverLiquid.sortingOrder + 2;
        stream.positionCount = 3;
        stream.numCapVertices = 3;
        stream.numCornerVertices = 2;
        stream.widthCurve = new AnimationCurve(new Keyframe(0f, 0.84f),
            new Keyframe(0.23f, 1f), new Keyframe(1f, 0.72f));
        Color juiceColor = sourceLiquid != null ? sourceLiquid.color : receiverLiquid.color;
        stream.startColor = Color.Lerp(juiceColor, Color.white, 0.08f);
        stream.endColor = juiceColor;

        if (sourceMask != null && sourceLiquid != null)
        {
            sourceArea = new LiquidMaskArea(sourceMask.sprite);
            sourceArea.Project(Matrix4x4.identity);
            sourceStartArea = sourceArea.AreaBelow(Mathf.Lerp(sourceArea.Bottom, sourceArea.Top, sourceStartLevel));
            if (sourceFlowPath != null && sourceFlowPath.Length >= 2)
            {
                GameObject flow = new GameObject("Juice Inside Neck");
                flow.transform.SetParent(sourceMask.transform, false);
                sourceFlow = flow.AddComponent<LineRenderer>();
                sourceFlow.enabled = false;
                sourceFlow.sharedMaterial = streamMaterial;
                sourceFlow.useWorldSpace = true;
                sourceFlow.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                sourceFlow.sortingLayerID = sourceLiquid.sortingLayerID;
                sourceFlow.sortingOrder = sourceLiquid.sortingOrder;
                sourceFlow.startColor = sourceLiquid.color;
                sourceFlow.endColor = sourceLiquid.color;
                sourceFlow.numCornerVertices = 3;
                sourceFlow.widthCurve = AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1f);
                sourceFlowPoints = new Vector3[sourceFlowPath.Length + 1];
            }
        }

        if (jugExit != null)
            jugExit.Completed += OnJugExited;
    }

    private void OnEnable()
    {
        phase = Phase.Entering;
        view = Camera.main;
        if (jugExit != null)
            jugExit.enabled = false;
        transform.SetLocalPositionAndRotation(authoredPosition, restRotation);
        transform.localScale = restScale;
        if (idleAnimator != null)
        {
            idleAnimator.enabled = false;
            idleAnimator.speed = 1f;
        }

        ClearPourEffects();
        drained = 0f;
        SetFill(0f);
        UpdateSourceLiquid();
        splash.transform.localPosition = splashPosition;
        if (view == null)
            return;

        Physics2D.SyncTransforms();
        pickupBounds = pickup.bounds;
        pickupBounds.center -= transform.position;
        home = ClampToView(transform.position, 1f);
        entrance.PlayTo(home);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        motion?.Kill();
        motion = null;
        ClearPourEffects();
        if (jugExit != null)
            jugExit.enabled = false;
    }

    private void OnDestroy()
    {
        if (jugExit != null)
            jugExit.Completed -= OnJugExited;
    }

    private void Update()
    {
        if (view == null)
            return;

        if (phase == Phase.Entering && entrance.IsComplete)
        {
            phase = Phase.Ready;
            if (idleAnimator != null)
                idleAnimator.enabled = true;
        }

        if (phase == Phase.Ready && TryPress(out Vector2 screen) && pickup.OverlapPoint(Project(screen)))
        {
            phase = Phase.Dragging;
            pressScreen = screen;
            grabOffset = transform.position - Project(screen);
            if (idleAnimator != null)
                idleAnimator.speed = 0f;
            motion = transform.DOScale(restScale * pickupScale, 0.16f).SetEase(Ease.OutCubic);
        }

        if (phase != Phase.Dragging)
            return;

        if (!TryHold(out Vector2 heldScreen))
        {
            phase = Phase.Busy;
            StartCoroutine(ReturnHome());
            return;
        }

        if ((heldScreen - pressScreen).sqrMagnitude < 36f)
            return;

        transform.position = ClampToView(Project(heldScreen) + grabOffset, pickupScale);
        Physics2D.SyncTransforms();
        if (receiver.isActiveAndEnabled && pickup.Distance(receiver).isOverlapped)
        {
            phase = Phase.Busy;
            StartCoroutine(Pour());
        }
    }

    private IEnumerator ReturnHome()
    {
        yield return MoveTo(home, restRotation, 0.24f).WaitForCompletion();
        if (idleAnimator != null)
            idleAnimator.speed = 1f;
        phase = Phase.Ready;
    }

    private IEnumerator Pour()
    {
        Bounds glass = receiver.bounds;
        spoutLocal = transform.InverseTransformPoint(spout.position);
        Matrix4x4 parentToWorld = transform.parent != null ? transform.parent.localToWorldMatrix : Matrix4x4.identity;
        Matrix4x4 maskInJug = sourceArea != null
            ? transform.worldToLocalMatrix * sourceMask.transform.localToWorldMatrix : Matrix4x4.identity;
        Quaternion angle = FindPourRotation(parentToWorld, maskInJug);
        Vector3 offset = SpoutOffset(parentToWorld, angle);
        Vector3 pourPoint = new Vector3(glass.center.x, glass.max.y + spoutClearance, home.z + offset.z);
        yield return MoveTo(pourPoint - offset, angle, 0.32f).WaitForCompletion();

        const float travelTime = 0.22f;
        bool hitGlass = false;
        for (float elapsed = 0f; elapsed < fillDuration + travelTime; elapsed += Time.deltaTime)
        {
            // The glass receives the same amount after its travel time down the stream.
            drained = Mathf.Clamp01(elapsed / fillDuration);
            float received = Mathf.Clamp01((elapsed - travelTime) / fillDuration);
            if (drained < 1f)
            {
                angle = FindPourRotation(parentToWorld, maskInJug);
                transform.localRotation = angle;
                transform.position = pourPoint - SpoutOffset(parentToWorld, angle);
            }

            float surface = SetFill(received);
            if (!hitGlass && elapsed >= travelTime)
            {
                hitGlass = true;
                splash.Play();
            }

            float head = Mathf.Clamp01(elapsed / travelTime);
            float tail = Mathf.Clamp01((elapsed - fillDuration) / travelTime);
            float strength = Mathf.Lerp(1f, 0.38f, Mathf.InverseLerp(0.8f, 1f, drained));
            DrawStream(surface, tail * tail, head * head, strength * (1f - tail));
            yield return null;
        }
        drained = 1f;
        SetFill(1f);
        stream.enabled = false;
        sourceFlowStrength = 0f;
        splash.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        yield return MoveTo(transform.position, restRotation, 0.2f).WaitForCompletion();
        if (jugExit != null)
            jugExit.enabled = true;
        else
            poured?.Invoke();
    }

    private Quaternion FindPourRotation(Matrix4x4 parentToWorld, Matrix4x4 maskInJug)
    {
        if (sourceArea == null)
            return Quaternion.AngleAxis(maximumTilt * 0.5f, Vector3.forward) * restRotation;

        float remainingArea = sourceStartArea * (1f - drained);
        float low = 0f;
        float high = 1f;
        // Rotate around the spout until the remaining liquid reaches the lip.
        for (int i = 0; i < 11; i++)
        {
            float turn = (low + high) * 0.5f;
            Quaternion angle = Quaternion.AngleAxis(maximumTilt * turn, Vector3.forward) * restRotation;
            Matrix4x4 jugPose = parentToWorld * Matrix4x4.TRS(Vector3.zero, angle, restScale);
            sourceArea.Project(jugPose * maskInJug);
            float lipHeight = jugPose.MultiplyPoint3x4(spoutLocal).y + SpoutContactDepth;
            if (sourceArea.AreaBelow(lipHeight) > remainingArea)
                low = turn;
            else
                high = turn;
        }
        return Quaternion.AngleAxis(maximumTilt * (low + high) * 0.5f, Vector3.forward) * restRotation;
    }

    private Vector3 SpoutOffset(Matrix4x4 parentToWorld, Quaternion angle) =>
        parentToWorld.MultiplyVector(angle * Vector3.Scale(spoutLocal, restScale));

    private void OnJugExited()
    {
        jugExit.enabled = false;
        poured?.Invoke();
    }

    private void DrawStream(float surface, float tail, float head, float strength)
    {
        // Start under the jug artwork so the stream meets the rim without a gap.
        Vector3 outlet = spout.position;
        outlet.x = Mathf.MoveTowards(outlet.x, transform.position.x, streamWidth * 0.38f);
        outlet.y += SpoutContactDepth;
        Vector3 impact = new Vector3(splash.transform.position.x, surface, outlet.z);
        Vector3 top = Vector3.Lerp(outlet, impact, tail);
        Vector3 bottom = Vector3.Lerp(outlet, impact, head);
        Vector3 middle = Vector3.Lerp(top, bottom, 0.4f);
        middle.x += (head - tail) * (0.055f + Mathf.Sin(Time.time * 13f) * 0.025f);
        stream.widthMultiplier = streamWidth * strength * (1f + Mathf.Sin(Time.time * 18f) * 0.045f);
        stream.SetPosition(0, top);
        stream.SetPosition(1, middle);
        stream.SetPosition(2, bottom);
        stream.enabled = head > tail;
        sourceFlowStrength = tail == 0f && stream.enabled && drained < 1f ? strength : 0f;
    }

    private float SetFill(float progress)
    {
        Transform level = receiverLiquid.transform;
        Vector3 scale = liquidScale;
        scale.x *= Mathf.Lerp(receiverBaseWidth, 1f, progress);
        scale.y *= progress;
        // Keep the base of the authored full sprite in place as its height changes.
        level.localPosition = liquidPosition + Vector3.up * (liquidSpriteBounds.min.y * (liquidScale.y - scale.y));
        level.localScale = scale;
        receiverLiquid.enabled = progress > 0f;

        float surface = level.TransformPoint(0f, liquidSpriteBounds.max.y, 0f).y;
        Vector3 impact = splash.transform.position;
        impact.y = surface;
        splash.transform.position = impact;
        return surface;
    }

    private void LateUpdate() => UpdateSourceLiquid();

    private void UpdateSourceLiquid()
    {
        if (sourceArea == null)
            return;

        sourceLiquid.enabled = drained < 1f;
        if (sourceFlow != null)
            sourceFlow.enabled = sourceLiquid.enabled && sourceFlowStrength > 0f;
        if (!sourceLiquid.enabled)
            return;

        Bounds interior = sourceMask.bounds;
        sourceArea.Project(sourceMask.transform.localToWorldMatrix);
        float surface = sourceArea.FindHeight(sourceStartArea * (1f - drained));
        float height = surface - sourceArea.Bottom;

        // Keep the surface level in world space while the jug rotates around it.
        Transform liquid = sourceLiquid.transform;
        liquid.rotation = Quaternion.identity;
        Transform parent = liquid.parent;
        Bounds sprite = sourceLiquid.sprite.bounds;
        float unitsX = parent.TransformVector(liquid.localRotation * Vector3.right).magnitude;
        float unitsY = parent.TransformVector(liquid.localRotation * Vector3.up).magnitude;
        liquid.localScale = new Vector3(interior.size.x / (sprite.size.x * unitsX), height / (sprite.size.y * unitsY), 1f);
        liquid.position = new Vector3(interior.center.x, sourceArea.Bottom + height * 0.5f, interior.center.z);

        if (sourceFlow != null && sourceFlow.enabled)
            DrawSourceFlow(surface);
    }

    private void DrawSourceFlow(float surface)
    {
        int start = 0;
        for (int i = 0; i < sourceFlowPath.Length; i++)
        {
            sourceFlowPoints[i] = sourceMask.transform.TransformPoint(sourceFlowPath[i]);
            if (sourceFlowPoints[i].y < sourceFlowPoints[start].y)
                start = i;
        }
        sourceFlowPoints[sourceFlowPath.Length] = stream.GetPosition(0);

        // Start inside the pool, then follow the curved neck to the outgoing stream.
        while (start + 1 < sourceFlowPath.Length && sourceFlowPoints[start + 1].y <= surface - 0.04f)
            start++;

        sourceFlow.positionCount = sourceFlowPoints.Length - start;
        for (int i = start; i < sourceFlowPoints.Length; i++)
            sourceFlow.SetPosition(i - start, sourceFlowPoints[i]);
        sourceFlow.widthMultiplier = streamWidth * 0.86f * sourceFlowStrength;
    }

    private void ClearPourEffects()
    {
        stream.enabled = false;
        sourceFlowStrength = 0f;
        if (sourceFlow != null)
            sourceFlow.enabled = false;
        splash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private Tween MoveTo(Vector3 position, Quaternion rotation, float duration)
    {
        motion?.Kill();
        motion = DOTween.Sequence()
            .Join(transform.DOMove(position, duration))
            .Join(transform.DOLocalRotateQuaternion(rotation, duration))
            .Join(transform.DOScale(restScale, duration))
            .SetEase(Ease.InOutSine);
        return motion;
    }

    private float Depth => view.WorldToScreenPoint(transform.position).z;

    private Vector3 Project(Vector2 screen) => view.ScreenToWorldPoint(new Vector3(screen.x, screen.y, Depth));

    private Vector3 ClampToView(Vector3 position, float scale)
    {
        Vector3 min = view.ViewportToWorldPoint(new Vector3(0f, 0f, Depth)) - pickupBounds.min * scale;
        Vector3 max = view.ViewportToWorldPoint(new Vector3(1f, 1f, Depth)) - pickupBounds.max * scale;
        position.x = ClampAxis(position.x, min.x, max.x);
        position.y = ClampAxis(position.y, min.y, max.y);
        return position;
    }

    private static float ClampAxis(float value, float min, float max) =>
        min <= max ? Mathf.Clamp(value, min, max) : (min + max) * 0.5f;

    private bool TryPress(out Vector2 screen)
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Began)
                continue;

            fingerId = touch.fingerId;
            screen = touch.position;
            return true;
        }

        fingerId = -1;
        screen = Input.mousePosition;
        return Input.touchCount == 0 && Input.GetMouseButtonDown(0);
    }

    private bool TryHold(out Vector2 screen)
    {
        if (fingerId < 0)
        {
            screen = Input.mousePosition;
            return Input.GetMouseButton(0);
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.fingerId != fingerId)
                continue;

            screen = touch.position;
            return touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
        }

        screen = default;
        return false;
    }

    private sealed class LiquidMaskArea
    {
        private readonly Vector2[] vertices;
        private readonly ushort[] triangles;
        private readonly float[] areas;
        private readonly float[] heights;

        public float Bottom { get; private set; }
        public float Top { get; private set; }

        public LiquidMaskArea(Sprite sprite)
        {
            vertices = sprite.vertices;
            triangles = sprite.triangles;
            heights = new float[vertices.Length];
            areas = new float[triangles.Length / 3];
            for (int i = 0; i < areas.Length; i++)
            {
                Vector2 origin = vertices[triangles[i * 3]];
                Vector2 b = vertices[triangles[i * 3 + 1]] - origin;
                Vector2 c = vertices[triangles[i * 3 + 2]] - origin;
                areas[i] = Mathf.Abs(b.x * c.y - b.y * c.x) * 0.5f;
            }
        }

        public void Project(Matrix4x4 matrix)
        {
            Bottom = float.PositiveInfinity;
            Top = float.NegativeInfinity;
            for (int i = 0; i < vertices.Length; i++)
            {
                float y = matrix.MultiplyPoint3x4(vertices[i]).y;
                heights[i] = y;
                Bottom = Mathf.Min(Bottom, y);
                Top = Mathf.Max(Top, y);
            }
        }

        public float FindHeight(float targetArea)
        {
            float lower = Bottom;
            float upper = Top;
            for (int i = 0; i < 14; i++)
            {
                float middle = (lower + upper) * 0.5f;
                if (AreaBelow(middle) < targetArea)
                    lower = middle;
                else
                    upper = middle;
            }
            return (lower + upper) * 0.5f;
        }

        public float AreaBelow(float height)
        {
            float area = 0f;
            for (int i = 0; i < areas.Length; i++)
            {
                float a = heights[triangles[i * 3]];
                float b = heights[triangles[i * 3 + 1]];
                float c = heights[triangles[i * 3 + 2]];
                float low = Mathf.Min(Mathf.Min(a, b), c);
                float high = Mathf.Max(Mathf.Max(a, b), c);
                if (height <= low)
                    continue;
                if (height >= high)
                {
                    area += areas[i];
                    continue;
                }

                // A horizontal cut leaves a similar triangle at the bottom or the top.
                float middle = Mathf.Max(Mathf.Min(a, b), Mathf.Min(Mathf.Max(a, b), c));
                if (height < middle)
                {
                    float depth = height - low;
                    area += areas[i] * depth * depth / ((middle - low) * (high - low));
                }
                else
                {
                    float gap = high - height;
                    area += areas[i] * (1f - gap * gap / ((high - middle) * (high - low)));
                }
            }
            return area;
        }
    }
}
