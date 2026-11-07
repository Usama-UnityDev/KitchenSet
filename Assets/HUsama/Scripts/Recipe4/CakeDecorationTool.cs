using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Shared pointer drag, pickup, cake lock and return behavior for cones and plates.</summary>
public class CakeDecorationTool : MonoBehaviour
{
    [SerializeField] private Vector2 nozzleInSprite = new Vector2(0.97f, 0.03f);
    [SerializeField] private Vector2 readyViewport = new Vector2(0.23f, 0.32f);
    [SerializeField, Min(1f)] private float pickupScale = 1.15f;
    [SerializeField] private SpriteRenderer[] plateItems;
    [SerializeField] private SlideOutObject slideOut;
    private bool exiting;
    private CakeDecorationController owner;
    private int category;
    private Vector3 home, scale, pointerOffset;
    private Camera cameraRef;
    private Collider2D hitArea;
    private SpriteRenderer[] renderers;
    private int[] sortingOrders;
    private bool dragging, locked, moving, pendingPickup;
    private int finger = -1;
    private Quaternion homeRotation;
    private LineRenderer creamStream;
    private Material streamMaterial;
    private SpriteRenderer toolRenderer;
    private bool plateReady, draggingItem;
    private Vector3 itemHome, itemGrabOffset;
    private Vector3 previousPointer;
    private Vector3 grabLocal;
    private float pipingPhase;
    private readonly SpriteRenderer[] itemVisuals = new SpriteRenderer[1];
    public SpriteRenderer SelectedPlateItem { get; private set; }
    public Vector3 GuidePoint => toolRenderer != null ? toolRenderer.bounds.center : transform.position;

    public void Initialize(CakeDecorationController controller, int index)
    {
        owner = controller;
        if (slideOut == null) slideOut = GetComponent<SlideOutObject>();
        if (slideOut != null)
        {
            slideOut.enabled = false;
            slideOut.Completed += OnSlideOutCompleted;
        }
        category = index;
        home = transform.localPosition;
        scale = transform.localScale;
        cameraRef = Camera.main;
        homeRotation = transform.localRotation;
        toolRenderer = GetComponent<SpriteRenderer>();
        if (plateItems == null) plateItems = new SpriteRenderer[0];
        hitArea = GetComponent<Collider2D>();
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        sortingOrders = new int[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) sortingOrders[i] = renderers[i].sortingOrder;
        // These tools use explicit pointer hit testing; physics must not push them away.
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null) body.simulated = false;
        foreach (var animator in GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        foreach (var legacy in GetComponentsInChildren<CreamBagDraggable>(true)) legacy.enabled = false;
        foreach (var slide in GetComponentsInChildren<SlideOutObject>(true)) slide.enabled = false;
        if (index != 3)
        {
            var stream = new GameObject("Nozzle cream stream");
            stream.transform.SetParent(transform, false);
            creamStream = stream.AddComponent<LineRenderer>();
            streamMaterial = new Material(Shader.Find("Sprites/Default"));
            creamStream.sharedMaterial = streamMaterial;
            creamStream.positionCount = 3;
            creamStream.numCapVertices = 5;
            creamStream.startWidth = 0.09f;
            creamStream.endWidth = 0.15f;
            creamStream.sortingOrder = 29;
            creamStream.enabled = false;
        }
    }

    public void Show(int selected)
    {
        Hide();
        gameObject.SetActive(true);
        float depth = Mathf.Abs(cameraRef.transform.position.z - transform.position.z);
        Vector3 ready = cameraRef.ViewportToWorldPoint(new Vector3(readyViewport.x, readyViewport.y, depth));
        ready.z = transform.position.z;
        home = transform.parent != null ? transform.parent.InverseTransformPoint(ready) : ready;
        for (int i = 0; i < plateItems.Length; i++)
        {
            plateItems[i].gameObject.SetActive(i == selected);
            if (i == selected) { SelectedPlateItem = plateItems[i]; itemVisuals[0] = plateItems[i]; }
        }
        moving = true;
        transform.localPosition = home;
        CakeToolScreenBounds.Constrain(transform, cameraRef, renderers);
        home = transform.localPosition;
        transform.localPosition = home + Vector3.left * 15f;
        transform.DOLocalMove(home, 0.5f).SetEase(Ease.OutCubic).OnComplete(() => { moving = false; owner.GuideTool(this); });
    }

    public void Hide()
    {
        exiting = false;
        if (slideOut != null) slideOut.enabled = false;
        transform.DOKill();
        if (draggingItem && SelectedPlateItem != null) SelectedPlateItem.transform.localPosition = itemHome;
        dragging = locked = pendingPickup = moving = plateReady = draggingItem = false;
        finger = -1;
        transform.localPosition = home;
        transform.localScale = scale;
        transform.localRotation = homeRotation;
        if (creamStream != null) creamStream.enabled = false;
        RestoreSorting();
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (cameraRef == null || moving) return;
        Vector2 screen = Input.mousePosition;
        bool down = Input.GetMouseButtonDown(0), held = Input.GetMouseButton(0), up = Input.GetMouseButtonUp(0);
        int pointerId = -1;
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (finger >= 0)
            {
                bool found = false;
                for (int i = 0; i < Input.touchCount; i++)
                    if (Input.GetTouch(i).fingerId == finger) { touch = Input.GetTouch(i); found = true; break; }
                if (!found) { Release(); return; }
            }
            screen = touch.position;
            pointerId = touch.fingerId;
            down = touch.phase == TouchPhase.Began;
            up = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
            held = !up;
            if (touch.phase == TouchPhase.Canceled) { Release(); return; }
        }
        Vector3 world = cameraRef.ScreenToWorldPoint(new Vector3(screen.x, screen.y, Mathf.Abs(cameraRef.transform.position.z - transform.position.z)));
        world.z = transform.position.z;
        if (plateReady && !dragging)
        {
            UpdatePlateItem(world, down, held, up, pointerId);
            return;
        }
        if (down && !dragging && !pendingPickup)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointerId)) return;
            Bounds hit = GetComponent<SpriteRenderer>().bounds;
            bool contains = (hitArea != null && hitArea.OverlapPoint(world)) ||
                (world.x >= hit.min.x && world.x <= hit.max.x && world.y >= hit.min.y && world.y <= hit.max.y);
            if (!contains) return;
            finger = pointerId;
            previousPointer = world;
            SoundManager.Instance?.PlaySound(SoundManager.Instance.itemPickupSound);
            owner.SetDragging(true);
            for (int i = 0; i < renderers.Length; i++) renderers[i].sortingOrder = sortingOrders[i] + 30;
            pointerOffset = transform.position - world;
            grabLocal = transform.InverseTransformPoint(world);
            dragging = true;
        }
        if (up || ((!held) && (dragging || pendingPickup))) { Release(); return; }
        if (!dragging) return;
        Vector3 velocity = (world - previousPointer) / Mathf.Max(Time.deltaTime, 0.001f);
        previousPointer = world;
        pipingPhase += Time.deltaTime * 12f;
        if (category != 3)
        {
            float squeeze = locked ? Mathf.Sin(pipingPhase) * 0.018f : 0f;
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.Scale(scale * pickupScale, new Vector3(1f + squeeze, 1f - squeeze, 1f)), 1f - Mathf.Exp(-20f * Time.deltaTime));
        }
        else transform.localScale = Vector3.Lerp(transform.localScale, scale * pickupScale, 1f - Mathf.Exp(-20f * Time.deltaTime));
        float pipingTilt = locked && category != 3 ? -10f + Mathf.Sin(pipingPhase) * 3f : 0f;
        Vector3 pivotBefore = NozzlePoint;
        Quaternion tilt = homeRotation * Quaternion.Euler(0f, 0f, pipingTilt + Mathf.Clamp(-velocity.x * 0.6f, -9f, 9f));
        transform.localRotation = Quaternion.Slerp(transform.localRotation, tilt, 1f - Mathf.Exp(-18f * Time.deltaTime));
        transform.position += pivotBefore - NozzlePoint;
        Vector3 tip = NozzlePoint - transform.position;
        Vector3 extrusion = category == 3 ? Vector3.zero : Vector3.up * 0.12f;
        Vector3 next = world - transform.TransformVector(grabLocal);
        Bounds bounds = owner.GetApplicationBounds(category);
        Vector3 tipPoint = next + tip - extrusion;
        // Swept segment contact also catches a fast drag across the cake in one frame.
        Vector3 oldTip = transform.position + tip - extrusion;
        Vector3 delta = tipPoint - oldTip;
        Bounds contact = bounds;
        contact.Expand(new Vector3(0.12f, 0.12f, 1f));
        if (category == 3)
        {
            Bounds plate = CakeToolScreenBounds.VisibleBounds(toolRenderer);
            plate.center += next - transform.position;
            Bounds cake = owner.CakeBounds;
            locked = plate.max.x >= cake.min.x - 0.2f && plate.min.x < cake.center.x &&
                plate.max.y >= cake.min.y && plate.min.y <= cake.max.y;
        }
        if (!locked && (contact.Contains(tipPoint) ||
            (delta.sqrMagnitude > 0f && contact.IntersectRay(new Ray(oldTip, delta.normalized), out float enter) && enter <= delta.magnitude))) locked = true;
        if (locked && category != 3)
        {
            tipPoint = bounds.ClosestPoint(tipPoint);
            next = tipPoint - tip + extrusion;
        }
        next.z = transform.position.z;
        Vector3 previousPosition = transform.position;
        transform.position = next;
        CakeToolScreenBounds.Constrain(transform, cameraRef, renderers);
        float distance = Vector3.Distance(previousPosition, transform.position);
        tipPoint = NozzlePoint - extrusion;
        if (locked && category == 3)
        {
            transform.DOKill();
            dragging = pendingPickup = locked = false;
            plateReady = true;
            // Dock the plate at its authored size before the user picks up its topping.
            transform.localScale = scale;
            transform.localRotation = homeRotation;
            // Dock outside the cake silhouette, never on top of the decoration area.
            Bounds plateBounds = CakeToolScreenBounds.VisibleBounds(toolRenderer);
            Bounds cake = owner.CakeBounds;
            Vector3 dock = transform.position;
            dock.x += cake.min.x - 0.2f - plateBounds.max.x;
            dock.y += cake.center.y - plateBounds.center.y;
            transform.position = dock;
            CakeToolScreenBounds.Constrain(transform, cameraRef, renderers);
            finger = -1;
            owner.SetDragging(false);
            owner.GuidePlateDrop(SelectedPlateItem.bounds.center);
            return;
        }
        if (creamStream != null)
        {
            creamStream.enabled = locked;
            if (locked)
            {
                Color color = owner.SelectedCreamColor(category);
                creamStream.startColor = creamStream.endColor = color;
                creamStream.SetPosition(0, NozzlePoint);
                creamStream.SetPosition(1, Vector3.Lerp(NozzlePoint, tipPoint, 0.5f) + Vector3.right * 0.015f);
                creamStream.SetPosition(2, tipPoint);
            }
        }
        if (locked && bounds.Contains(tipPoint) && owner.Apply(category, distance, tipPoint))
        {
            dragging = false;
            if (category != 3) ReturnHome(true);
            else moving = true;
        }
    }

    private void UpdatePlateItem(Vector3 world, bool down, bool held, bool up, int pointerId)
    {
        if (SelectedPlateItem == null) return;
        if (down && !draggingItem && SelectedPlateItem.bounds.Contains(new Vector3(world.x, world.y, SelectedPlateItem.bounds.center.z)))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointerId)) return;
            finger = pointerId;
            draggingItem = true;
            itemHome = SelectedPlateItem.transform.localPosition;
            itemGrabOffset = SelectedPlateItem.transform.position - world;
            owner.SetDragging(true);
        }
        if (!draggingItem) return;
        SelectedPlateItem.transform.position = world + itemGrabOffset;
        CakeToolScreenBounds.Constrain(SelectedPlateItem.transform, cameraRef, itemVisuals);
        if (!up && held) return;
        draggingItem = false;
        finger = -1;
        if (owner.IsCenterDrop(SelectedPlateItem.bounds.center))
        {
            plateReady = false;
            moving = true;
            owner.Apply(category, 0f, owner.CenterDropPoint);
        }
        else
        {
            SelectedPlateItem.transform.localPosition = itemHome;
            owner.SetDragging(false);
            owner.GuidePlateDrop(SelectedPlateItem.bounds.center);
        }
    }

    private void Release()
    {
        if (draggingItem)
        {
            SelectedPlateItem.transform.localPosition = itemHome;
            draggingItem = false;
            finger = -1;
            owner.SetDragging(false);
            return;
        }
        if (!dragging && !pendingPickup) return;
        ReturnHome(false);
    }

    public void ReturnHome(bool completed)
    {
        if (creamStream != null) creamStream.enabled = false;
        transform.DOKill();
        dragging = locked = pendingPickup = plateReady = draggingItem = false;
        SoundManager.Instance?.PlaySound(SoundManager.Instance.itemReleaseSound);
        moving = true;
        finger = -1;
        transform.DOScale(scale, 0.25f);
        transform.DOLocalRotateQuaternion(homeRotation, 0.25f);
        transform.DOLocalMove(home, 0.35f).SetEase(Ease.OutCubic)
            .OnUpdate(() => CakeToolScreenBounds.Constrain(transform, cameraRef, renderers)).OnComplete(() =>
        {
            RestoreSorting();
            if (completed)
            {
                if (slideOut == null)
                {
                    Debug.LogError("Assign the tool's SlideOutObject component before completing its task.", this);
                    return;
                }
                // Movement limits apply to player dragging, not to the component-owned exit.
                exiting = true;
                slideOut.enabled = true;
            }
            else
            {
                moving = false;
                owner.ToolReturned();
            }
        });
    }

    private void OnSlideOutCompleted()
    {
        if (!exiting) return;
        exiting = false;
        moving = false;
        owner.ToolReturned();
    }

    private void OnApplicationFocus(bool focus)
    {
        if (focus) return;
        if (draggingItem) { SelectedPlateItem.transform.localPosition = itemHome; draggingItem = false; finger = -1; }
        if (plateReady) ReturnHome(false); else Release();
    }
    private Vector3 NozzlePoint
    {
        get
        {
            if (category == 3) return transform.position;
            Bounds b = toolRenderer.sprite.bounds;
            return transform.TransformPoint(new Vector3(Mathf.Lerp(b.min.x, b.max.x, nozzleInSprite.x),
                Mathf.Lerp(b.min.y, b.max.y, nozzleInSprite.y), 0f));
        }
    }
    private void RestoreSorting()
    {
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++) renderers[i].sortingOrder = sortingOrders[i];
    }
    private void OnDisable() { transform.DOKill(); }
    private void OnDestroy()
    {
        if (slideOut != null) slideOut.Completed -= OnSlideOutCompleted;
        if (streamMaterial != null) Destroy(streamMaterial);
    }
}

/// <summary>Clamp the entire visible tool, including pickup scale, rotation and plate contents.</summary>
public static class CakeToolScreenBounds
{
    private static readonly System.Collections.Generic.Dictionary<Sprite, Bounds> visible = new System.Collections.Generic.Dictionary<Sprite, Bounds>();

    private static Bounds LocalBounds(Sprite sprite)
    {
        if (visible.TryGetValue(sprite, out Bounds result)) return result;
        const int size = 128;
        var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
        var old = RenderTexture.active;
        var alpha = new Texture2D(size, size, TextureFormat.RGBA32, false);
        try
        {
            Rect rect = sprite.textureRect;
            Vector2 dimensions = new Vector2(sprite.texture.width, sprite.texture.height);
            Graphics.Blit(sprite.texture, rt, rect.size / dimensions, rect.position / dimensions);
            RenderTexture.active = rt;
            alpha.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            Color32[] pixels = alpha.GetPixels32();
            int left = size, right = 0, bottom = size, top = 0;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                if (pixels[y * size + x].a > 8) { left = Mathf.Min(left, x); right = Mathf.Max(right, x + 1); bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y + 1); }
            result = sprite.bounds;
            if (left < right)
            {
                Vector3 min = result.min, extent = result.size;
                result.SetMinMax(min + new Vector3(extent.x * left / size, extent.y * bottom / size, 0),
                    min + new Vector3(extent.x * right / size, extent.y * top / size, 0));
            }
            visible[sprite] = result;
            return result;
        }
        finally { RenderTexture.active = old; RenderTexture.ReleaseTemporary(rt); Object.Destroy(alpha); }
    }

    public static Bounds VisibleBounds(SpriteRenderer renderer)
    {
        Bounds local = LocalBounds(renderer.sprite);
        Bounds result = new Bounds(renderer.transform.TransformPoint(local.center), Vector3.zero);
        for (int corner = 0; corner < 4; corner++)
            result.Encapsulate(renderer.transform.TransformPoint(new Vector3((corner & 1) == 0 ? local.min.x : local.max.x, (corner & 2) == 0 ? local.min.y : local.max.y, 0f)));
        return result;
    }

    public static void Constrain(Transform root, Camera camera, SpriteRenderer[] visuals, float margin = 0.002f)
    {
        if (camera == null || visuals == null) return;
        Rect safe = Screen.safeArea;
        safe = Rect.MinMaxRect(Mathf.Max(safe.xMin, camera.pixelRect.xMin), Mathf.Max(safe.yMin, camera.pixelRect.yMin),
            Mathf.Min(safe.xMax, camera.pixelRect.xMax), Mathf.Min(safe.yMax, camera.pixelRect.yMax));
        float pad = Mathf.Min(safe.width, safe.height) * Mathf.Clamp(margin, 0f, 0.003f);
        safe = Rect.MinMaxRect(safe.xMin + pad, safe.yMin + pad, safe.xMax - pad, safe.yMax - pad);
        for (int attempt = 0; attempt < 2; attempt++)
        {
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            bool found = false;
            foreach (SpriteRenderer visual in visuals)
            {
                if (visual == null || !visual.enabled || !visual.gameObject.activeInHierarchy) continue;
                Bounds bounds = LocalBounds(visual.sprite);
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector3 local = new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x, (corner & 2) == 0 ? bounds.min.y : bounds.max.y, 0f);
                    if (visual.flipX) local.x = -local.x;
                    if (visual.flipY) local.y = -local.y;
                    Vector3 point = camera.WorldToScreenPoint(visual.transform.TransformPoint(local));
                    min = Vector2.Min(min, point); max = Vector2.Max(max, point);
                    found = true;
                }
            }
            if (!found) return;
            Vector2 size = max - min;
            float fit = Mathf.Min(1f, safe.width / Mathf.Max(1f, size.x), safe.height / Mathf.Max(1f, size.y));
            if (fit < 0.999f) { root.localScale *= fit; continue; }
            Vector2 shift = new Vector2(min.x < safe.xMin ? safe.xMin - min.x : max.x > safe.xMax ? safe.xMax - max.x : 0f,
                min.y < safe.yMin ? safe.yMin - min.y : max.y > safe.yMax ? safe.yMax - max.y : 0f);
            Vector3 screen = camera.WorldToScreenPoint(root.position);
            root.position += camera.ScreenToWorldPoint(screen + new Vector3(shift.x, shift.y, 0f)) - camera.ScreenToWorldPoint(screen);
            return;
        }
    }
}
