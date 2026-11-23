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
    [SerializeField, Tooltip("Points just inside the pouring side of the jug, from the belly to the lip, in mask-local coordinates.")]
    private Vector2[] sourceFlowPath;
    [SerializeField, Range(0.1f, 1f)] private float sourceStartLevel = 0.78f;
    [SerializeField, Range(0f, 0.95f)] private float sourceRemainingFraction = 0.42f;
    [SerializeField] private SpriteRenderer receiverLiquid;
    [SerializeField, Range(0.5f, 1f)] private float receiverBaseWidth = 1f;
    [SerializeField] private float maximumTilt = -88f;
    [SerializeField, Min(0.1f)] private float spoutClearance = 1f;
    [SerializeField, Min(0.1f)] private float fillDuration = 1.65f;
    [SerializeField] private SlideOutObject jugExit;
    [SerializeField] private UnityEvent poured;

    private const float NeckLiquidDepth = 0.22f;
    private const float ReceiverSurfaceDepth = 0.13f;
    private const int StreamPointCount = 16;
    private static readonly AnimationCurve SettlingSurfaceAngle = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.28f, 19f), new Keyframe(0.76f, -5f), new Keyframe(1f, 0f));

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
    private LineRenderer returnFlow;
    private Vector3[] returnFlowPoints;
    private LiquidMaskArea sourceArea;
    private float sourceStartArea;
    private float sourceRemainingArea;
    private float settleProgress = 1f;
    private float idleSloshTime;
    private float idleSurfaceAngle;
    private bool pouredNotified;

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
        stream.positionCount = StreamPointCount;
        stream.numCapVertices = 4;
        stream.numCornerVertices = 3;
        Color juiceColor = sourceLiquid != null ? sourceLiquid.color : receiverLiquid.color;
        stream.startColor = juiceColor;
        stream.endColor = juiceColor;

        if (sourceMask != null && sourceLiquid != null)
        {
            sourceArea = new LiquidMaskArea(sourceMask.sprite);
            sourceArea.Project(Matrix4x4.identity);
            sourceStartArea = sourceArea.AreaBelow(Mathf.Lerp(sourceArea.Bottom, sourceArea.Top, sourceStartLevel));

            if (sourceFlowPath != null && sourceFlowPath.Length >= 2)
            {
                GameObject runoff = new GameObject("Juice Returning Inside Jug");
                runoff.transform.SetParent(sourceMask.transform, false);
                returnFlow = runoff.AddComponent<LineRenderer>();
                returnFlow.enabled = false;
                returnFlow.sharedMaterial = streamMaterial;
                returnFlow.useWorldSpace = true;
                returnFlow.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                returnFlow.sortingLayerID = sourceLiquid.sortingLayerID;
                returnFlow.sortingOrder = sourceLiquid.sortingOrder;
                returnFlow.numCapVertices = 4;
                returnFlow.numCornerVertices = 3;
                returnFlow.widthCurve = new AnimationCurve(
                    new Keyframe(0f, 0.24f), new Keyframe(0.35f, 0.86f), new Keyframe(1f, 1f));
                returnFlow.startColor = new Color(juiceColor.r, juiceColor.g, juiceColor.b, juiceColor.a * 0.78f);
                returnFlow.endColor = juiceColor;
                returnFlowPoints = new Vector3[sourceFlowPath.Length];
            }
        }

        if (jugExit != null)
            jugExit.Completed += OnJugExited;
    }

    private void OnEnable()
    {
        phase = Phase.Entering;
        pouredNotified = false;
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
        idleSloshTime = 0f;
        idleSurfaceAngle = 0f;
        sourceRemainingArea = sourceStartArea;
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
        float distance = Vector3.Distance(transform.position, home);
        float returnDuration = Mathf.Clamp(0.38f + distance * 0.045f, 0.38f, 0.8f);
        yield return MoveTo(home, restRotation, returnDuration).WaitForCompletion();
        if (idleAnimator != null)
            idleAnimator.speed = 1f;
        idleSloshTime = 0f;
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
        Vector3 pourPoint = new Vector3(glass.center.x - streamWidth * 0.7f, glass.max.y + spoutClearance, home.z + offset.z);
        yield return MoveTo(pourPoint - offset, angle, 0.32f).WaitForCompletion();

        const float travelTime = 0.22f;
        bool splashStarted = false;
        for (float elapsed = 0f; elapsed < fillDuration + travelTime; elapsed += Time.deltaTime)
        {
            // The glass receives the same amount after its travel time down the stream.
            float emitted = Mathf.Clamp01(elapsed / fillDuration);
            float arrived = Mathf.Clamp01((elapsed - travelTime) / fillDuration);
            sourceRemainingArea = sourceStartArea * Mathf.Lerp(1f, sourceRemainingFraction, TransferProgress(emitted));
            angle = FindPourRotation(parentToWorld, maskInJug);
            transform.localRotation = angle;
            transform.position = pourPoint - SpoutOffset(parentToWorld, angle);

            float surface = SetFill(TransferProgress(arrived));
            if (!splashStarted && elapsed >= travelTime)
            {
                splash.Play();
                splashStarted = true;
            }

            float head = Mathf.Clamp01(elapsed / travelTime);
            float tail = Mathf.Clamp01((elapsed - fillDuration) / travelTime);
            float strength = Mathf.Lerp(0.63f, 1f, Mathf.SmoothStep(0f, 1f, head));
            strength *= Mathf.Lerp(1f, 0.48f, Mathf.InverseLerp(0.86f, 1f, emitted));
            DrawStream(surface, tail * tail, head * head, strength * (1f - tail));
            yield return null;
        }
        sourceRemainingArea = sourceStartArea * sourceRemainingFraction;
        SetFill(1f);
        stream.enabled = false;
        splash.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        yield return SettleJug();
        motion = DOTween.Sequence()
            .Append(transform.DOScale(restScale * 1.09f, 0.13f).SetEase(Ease.OutCubic))
            .Append(transform.DOScale(restScale, 0.19f).SetEase(Ease.InOutSine));
        if (jugExit != null)
            jugExit.enabled = true;

        if (jugExit == null)
            NotifyPoured();
    }

    private IEnumerator SettleJug()
    {
        const float rightingDuration = 0.64f;
        const float settleDuration = 0.78f;
        Quaternion tiltedRotation = transform.localRotation;
        for (float elapsed = 0f; elapsed < settleDuration; elapsed += Time.deltaTime)
        {
            settleProgress = elapsed / settleDuration;
            float upright = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / rightingDuration));
            transform.localRotation = Quaternion.Slerp(tiltedRotation, restRotation, upright);
            yield return null;
        }

        transform.localRotation = restRotation;
        settleProgress = 1f;
    }

    private static float TransferProgress(float time) => Mathf.Lerp(time, Mathf.SmoothStep(0f, 1f, time), 0.45f);

    private Quaternion FindPourRotation(Matrix4x4 parentToWorld, Matrix4x4 maskInJug)
    {
        if (sourceArea == null)
            return Quaternion.AngleAxis(maximumTilt * 0.5f, Vector3.forward) * restRotation;

        float low = 0f;
        float high = 1f;
        // The surface must reach the whole channel. A cut through just the lip can leave a dry neck.
        for (int i = 0; i < 11; i++)
        {
            float turn = (low + high) * 0.5f;
            Quaternion angle = Quaternion.AngleAxis(maximumTilt * turn, Vector3.forward) * restRotation;
            Matrix4x4 jugPose = parentToWorld * Matrix4x4.TRS(Vector3.zero, angle, restScale);
            Matrix4x4 maskPose = jugPose * maskInJug;
            sourceArea.Project(maskPose);
            float channelHeight = jugPose.MultiplyPoint3x4(spoutLocal).y;
            if (sourceFlowPath != null)
            {
                foreach (Vector2 point in sourceFlowPath)
                    channelHeight = Mathf.Max(channelHeight, maskPose.MultiplyPoint3x4(point).y);
            }
            if (sourceArea.AreaBelow(channelHeight + NeckLiquidDepth) > sourceRemainingArea)
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
        NotifyPoured();
    }

    private void NotifyPoured()
    {
        if (pouredNotified) return;
        pouredNotified = true;
        poured?.Invoke();
    }

    private void DrawStream(float surface, float tail, float head, float strength)
    {
        Vector3 outlet = spout.position;
        stream.enabled = head > tail;
        if (!stream.enabled)
            return;

        // Overlap the filled mask under the jug artwork and let the stream curve down from the lip.
        if (sourceMask != null && sourceFlowPath != null && sourceFlowPath.Length > 0)
            outlet = sourceMask.transform.TransformPoint(sourceFlowPath[sourceFlowPath.Length - 1]);

        Vector3 impact = new Vector3(splash.transform.position.x, surface, outlet.z);
        Vector3 bend = spout.position;
        bend.x = impact.x + streamWidth * (0.32f + Mathf.Sin(Time.time * 12f) * 0.08f);
        bend.y -= (bend.y - surface) * 0.2f;
        stream.startWidth = streamWidth * strength * Mathf.Lerp(1f, 0.68f, tail);
        stream.endWidth = streamWidth * strength * Mathf.Lerp(1f, 0.68f, head);
        for (int i = 0; i < StreamPointCount; i++)
        {
            float t = Mathf.Lerp(tail, head, i / (StreamPointCount - 1f));
            stream.SetPosition(i, Vector3.Lerp(Vector3.Lerp(outlet, bend, t), Vector3.Lerp(bend, impact, t), t));
        }
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

        // The texture has an elliptical surface. Aim for its center instead of its back edge.
        float surface = level.TransformPoint(0f, liquidSpriteBounds.max.y - liquidSpriteBounds.size.y * ReceiverSurfaceDepth, 0f).y;
        Vector3 impact = splash.transform.position;
        impact.y = surface;
        splash.transform.position = impact;
        return surface;
    }

    private void LateUpdate()
    {
        float targetAngle = 0f;
        if (phase == Phase.Ready)
        {
            idleSloshTime += Time.deltaTime;
            targetAngle = Mathf.Sin(idleSloshTime * (2f * Mathf.PI / 1.8f)) * 4.5f;
        }

        idleSurfaceAngle = Mathf.MoveTowards(idleSurfaceAngle, targetAngle, Time.deltaTime * 40f);
        UpdateSourceLiquid();
    }

    private void UpdateSourceLiquid()
    {
        if (sourceArea == null)
            return;

        sourceLiquid.enabled = sourceRemainingArea > 0f;
        if (!sourceLiquid.enabled)
        {
            if (returnFlow != null)
                returnFlow.enabled = false;
            return;
        }

        // Solve for the same volume along the moving surface, then let it level out.
        float surfaceAngle = idleSurfaceAngle - SettlingSurfaceAngle.Evaluate(settleProgress) * Mathf.Sign(maximumTilt);
        Quaternion surfaceRotation = Quaternion.AngleAxis(surfaceAngle, Vector3.forward);
        Vector3 surfaceUp = surfaceRotation * Vector3.up;
        Vector3 surfaceRight = surfaceRotation * Vector3.right;
        Bounds interior = sourceMask.bounds;
        Matrix4x4 maskToWorld = sourceMask.transform.localToWorldMatrix;
        sourceArea.Project(Matrix4x4.Rotate(Quaternion.Inverse(surfaceRotation)) * maskToWorld);
        float surface = sourceArea.FindHeight(sourceRemainingArea);
        float height = surface - sourceArea.Bottom;
        float width = Mathf.Abs(surfaceRight.x) * interior.size.x + Mathf.Abs(surfaceRight.y) * interior.size.y;

        Transform liquid = sourceLiquid.transform;
        liquid.rotation = surfaceRotation;
        Transform parent = liquid.parent;
        Bounds sprite = sourceLiquid.sprite.bounds;
        float unitsX = parent.TransformVector(liquid.localRotation * Vector3.right).magnitude;
        float unitsY = parent.TransformVector(liquid.localRotation * Vector3.up).magnitude;
        liquid.localScale = new Vector3(width / (sprite.size.x * unitsX), height / (sprite.size.y * unitsY), 1f);
        float centerOffset = surface - height * 0.5f - Vector3.Dot(interior.center, surfaceUp);
        liquid.position = interior.center + surfaceUp * centerOffset;

        if (returnFlow != null)
            returnFlow.enabled = settleProgress < 1f && DrawReturnFlow(maskToWorld, surfaceUp, surface);
    }

    private bool DrawReturnFlow(Matrix4x4 maskToWorld, Vector3 surfaceUp, float surface)
    {
        // Pull the top of the remaining sheet down the inside wall as it drains into the pool.
        float retreat = Mathf.InverseLerp(0.16f, 0.88f, settleProgress);
        float pathIndex = (sourceFlowPath.Length - 1) * (1f - retreat * retreat);
        int next = Mathf.CeilToInt(pathIndex) - 1;
        if (next < 0)
            return false;

        Vector2 tip = Vector2.Lerp(sourceFlowPath[next], sourceFlowPath[next + 1], pathIndex - next);
        Vector3 point = maskToWorld.MultiplyPoint3x4(tip);
        float aboveSurface = Vector3.Dot(point, surfaceUp) - surface;
        if (aboveSurface <= 0f)
            return false;

        float exposedHeight = aboveSurface;
        returnFlowPoints[0] = point;
        int count = 1;
        for (int i = next; i >= 0; i--)
        {
            Vector3 lower = maskToWorld.MultiplyPoint3x4(sourceFlowPath[i]);
            float lowerHeight = Vector3.Dot(lower, surfaceUp) - surface;
            if (lowerHeight <= 0f)
            {
                // Extend a little into the liquid so the ribbon meets the pool without a seam.
                float overlap = Mathf.Min(0.04f, -lowerHeight);
                returnFlowPoints[count++] = Vector3.Lerp(point, lower, (aboveSurface + overlap) / (aboveSurface - lowerHeight));
                returnFlow.positionCount = count;
                for (int vertex = 0; vertex < count; vertex++)
                    returnFlow.SetPosition(vertex, returnFlowPoints[vertex]);
                returnFlow.widthMultiplier = streamWidth * 0.82f * Mathf.InverseLerp(0f, 0.2f, exposedHeight);
                return true;
            }

            returnFlowPoints[count++] = lower;
            point = lower;
            aboveSurface = lowerHeight;
        }

        return false;
    }

    private void ClearPourEffects()
    {
        stream.enabled = false;
        settleProgress = 1f;
        if (returnFlow != null)
            returnFlow.enabled = false;
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
