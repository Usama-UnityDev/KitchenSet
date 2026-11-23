using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>Stages the chef offering and captures the completion photograph.</summary>
public abstract class ServingFinalPresentation : MonoBehaviour
{
    [SerializeField] private UnityEvent nextModeRequested = new UnityEvent();
    [SerializeField] private Sprite chefSprite;
    [SerializeField] private Shader chefModelShader;
    [SerializeField] private Shader presentationSpriteShader;
    [SerializeField] private string nextModeScene = "Home";
    [Header("Scene Win Panel (UIs / Canvas / WIN PANEL)")]
    [SerializeField] private GameObject winPanel;
    [SerializeField] private RectTransform winCard;
    [SerializeField] private RawImage winPhotograph;
    [SerializeField] private AspectRatioFitter winPhotoAspect;
    [SerializeField] private Button nextModeButton;
    private Transform display;
    private Bounds displayBounds;
    private Texture2D photograph;
    private Transform chef;
    private Transform servingRig;
    private ServingChefRig chefRig;
    private readonly System.Collections.Generic.List<Material> chefMaterials = new System.Collections.Generic.List<Material>();
    private bool presenting, navigating;
    private Sequence offering;
    public Texture2D Photograph => photograph;

    protected void Awake()
    {
        if (winPanel != null) winPanel.SetActive(false);
    }

    protected void BuildServedItem(Transform source, SpriteRenderer servingSurface, bool drink)
    {
        Camera camera = Camera.main;
        display = new GameObject(drink ? "Chef carrying the juice" : "Chef carrying the player's cake").transform;
        servingRig = new GameObject(drink ? "Held decorated glass" : "Held plate and decorated cake").transform;
        servingRig.SetParent(display, false);
        Transform cake = drink ? CopyDrinkVisuals(source, servingRig) : Instantiate(source, servingRig, false);
        cake.name = drink ? "Player decorated juice glass" : "Player decorated cake";
        cake.gameObject.SetActive(true);
        foreach (Animator animator in cake.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        foreach (Collider2D collider in cake.GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
        SpriteRenderer[] originals = drink ? cake.GetComponentsInChildren<SpriteRenderer>(true) : source.GetComponentsInChildren<SpriteRenderer>(true);
        SpriteRenderer[] copies = cake.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < originals.Length; i++)
        {
            var block = new MaterialPropertyBlock();
            originals[i].GetPropertyBlock(block);
            copies[i].SetPropertyBlock(block);
            var spriteMaterial = new Material(presentationSpriteShader);
            if (drink)
                spriteMaterial.SetInt("_DepthTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
            copies[i].sharedMaterial = spriteMaterial;
            chefMaterials.Add(spriteMaterial);
            copies[i].sortingLayerID = servingSurface.sortingLayerID;
            copies[i].sortingOrder = originals[i].sortingOrder + 100;
        }

        GameObject model = Resources.Load<GameObject>("KitchenServingChef");
        if (model == null || chefModelShader == null)
            throw new System.InvalidOperationException("Import KitchenServingChef.dae and assign the serving chef shader.");
        chef = Instantiate(model, display, false).transform;
        chef.name = "Two-handed kitchen chef";
        chef.localRotation = Quaternion.Euler(0f, 180f, 0f);
        foreach (Animator animator in chef.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        foreach (Renderer visual in chef.GetComponentsInChildren<Renderer>())
        {
            Material[] sourceMaterials = visual.sharedMaterials;
            var materials = new Material[sourceMaterials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = new Material(chefModelShader);
                materials[i].mainTexture = sourceMaterials[i].mainTexture;
                chefMaterials.Add(materials[i]);
            }
            visual.sharedMaterials = materials;
            visual.sortingLayerID = servingSurface.sortingLayerID;
            visual.sortingOrder = 98;
            if (visual is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
        }
        Bounds fullChef = CombinedBounds(chef);
        float visibleFraction = 0.57f;
        float chefHeight = camera.orthographicSize * 1.4f;
        chef.localScale *= chefHeight / (fullChef.size.y * visibleFraction);
        fullChef = CombinedBounds(chef);
        float waist = fullChef.min.y + fullChef.size.y * (1f - visibleFraction);
        chefRig = chef.gameObject.AddComponent<ServingChefRig>();
        chefRig.WaistLocalY = chef.InverseTransformPoint(new Vector3(chef.position.x, waist, chef.position.z)).y;
        Bounds chefBounds = fullChef;
        chefBounds.SetMinMax(new Vector3(fullChef.min.x, waist, fullChef.min.z), fullChef.max);

        Bounds cakeBounds = CombinedBounds(cake);
        float fit = Mathf.Min(chefHeight * (drink ? 0.45f : 0.34f) / cakeBounds.size.y, chefBounds.size.x * (drink ? 0.52f : 0.86f) / cakeBounds.size.x);
        cake.localScale *= fit;
        cakeBounds = CombinedBounds(cake);

        Vector3 grip = new Vector3(chefBounds.center.x, chefBounds.min.y + chefHeight * 0.24f, chef.position.z - 0.3f);
        if (drink)
        {
            SpriteRenderer glassBody = null;
            foreach (SpriteRenderer visual in copies)
                if (visual.name == "front") { glassBody = visual; break; }
            if (glassBody == null) throw new System.InvalidOperationException("The juice glass needs its front sprite for the two-hand grip.");
            Bounds body = CakeToolScreenBounds.VisibleBounds(glassBody);
            grip.z = chefBounds.min.z - 0.08f;
            cake.position += grip - new Vector3(body.center.x, body.min.y + body.size.y * 0.24f, body.center.z);
            chefRig.Initialize(cake, glassBody, gripGlass: true);
        }
        else
        {
            Transform plateTransform = new GameObject("Serving plate held by both hands").transform;
            plateTransform.SetParent(servingRig, false);
            SpriteRenderer plate = plateTransform.gameObject.AddComponent<SpriteRenderer>();
            plate.sprite = servingSurface.sprite;
            var plateMaterial = new Material(presentationSpriteShader);
            plate.sharedMaterial = plateMaterial;
            chefMaterials.Add(plateMaterial);
            plate.sortingLayerID = servingSurface.sortingLayerID;
            plate.sortingOrder = 99;
            Bounds plateBounds = CakeToolScreenBounds.VisibleBounds(plate);
            float plateScale = cakeBounds.size.x * 1.18f / plateBounds.size.x;
            // The original plate artwork is almost circular; the serving view needs an ellipse.
            plateTransform.localScale = new Vector3(plateScale, plateScale * 0.48f, plateScale);
            plateBounds = CakeToolScreenBounds.VisibleBounds(plate);
            plateTransform.position += grip - plateBounds.center;
            plateBounds = CakeToolScreenBounds.VisibleBounds(plate);
            // The cake's bottom ellipse overlaps the inner plate, leaving just the rim visible.
            Vector3 cakeBase = new Vector3(cakeBounds.center.x, cakeBounds.min.y, cakeBounds.center.z);
            Vector3 landing = new Vector3(plateBounds.center.x, plateBounds.center.y - plateBounds.size.y * 0.18f, grip.z - 0.02f);
            cake.position += landing - cakeBase;
            chefRig.Initialize(plateTransform, plate);
        }

        displayBounds = CombinedBounds(display);
        float frameFit = Mathf.Min(camera.orthographicSize * 1.65f / displayBounds.size.y,
            camera.orthographicSize * camera.aspect * 1.45f / displayBounds.size.x);
        display.localScale *= Mathf.Min(1f, frameFit);
        displayBounds = CombinedBounds(display);
        Vector3 center = camera.ViewportToWorldPoint(new Vector3(0.5f, 0.49f, Mathf.Abs(camera.transform.position.z)));
        center.z = 0f;
        display.position += center - displayBounds.center;
        display.SetParent(transform, true);
        displayBounds = CombinedBounds(display);
    }

    private static Transform CopyDrinkVisuals(Transform source, Transform parent)
    {
        // Copy only visible artwork: entrance scripts, colliders and particles must not restart.
        Transform copy = new GameObject("Decorated drink artwork").transform;
        copy.SetParent(parent, false);
        foreach (SpriteRenderer original in source.GetComponentsInChildren<SpriteRenderer>(true))
        {
            bool visible = original.enabled;
            for (Transform node = original.transform; node != source && node != null; node = node.parent)
                visible &= node.gameObject.activeSelf;
            if (!visible || original.sprite == null || original.name == "base shadow") continue;
            Transform art = new GameObject(original.name).transform;
            art.SetParent(copy, false);
            art.localPosition = source.InverseTransformPoint(original.transform.position);
            art.localRotation = Quaternion.Inverse(source.rotation) * original.transform.rotation;
            Vector3 size = original.transform.lossyScale, rootSize = source.lossyScale;
            art.localScale = new Vector3(size.x / rootSize.x, size.y / rootSize.y, size.z / rootSize.z);
            SpriteRenderer sprite = art.gameObject.AddComponent<SpriteRenderer>();
            sprite.sprite = original.sprite;
            sprite.color = original.color;
            sprite.flipX = original.flipX;
            sprite.flipY = original.flipY;
            sprite.sortingOrder = original.sortingOrder;
        }
        return copy;
    }

    protected void OnEnable()
    {
        if (display != null && !presenting) StartCoroutine(ArriveAndPhotograph(Vector3.left));
    }

    protected IEnumerator ArriveAndPhotograph(Vector3 entranceDirection)
    {
        presenting = true;
        Camera camera = Camera.main;
        Vector3 home = display.position;
        float offset = camera.orthographic ? camera.orthographicSize * camera.aspect * 2.5f : displayBounds.size.x * 3f;
        display.position = home + entranceDirection * offset;
        return OfferAndCapture(camera, home);
    }

    private IEnumerator OfferAndCapture(Camera cameraRef, Vector3 home)
    {
        // Let the model and grip settle off-screen before showing the first frame of the entrance.
        yield return null;
        yield return display.DOMove(home, 0.85f).SetEase(Ease.OutCubic).WaitForCompletion();
        yield return new WaitForSeconds(0.25f);
        // Arms track the serving plate through the offering gesture, preserving both hand contacts.
        Vector3 traySize = servingRig.localScale;
        Vector3 trayPosition = servingRig.localPosition;
        offering = DOTween.Sequence();
        offering.Append(servingRig.DOScale(traySize * 1.10f, 0.48f).SetEase(Ease.InOutSine));
        offering.Join(servingRig.DOLocalMove(trayPosition + Vector3.up * (displayBounds.size.y * 0.025f), 0.48f).SetEase(Ease.InOutSine));
        offering.Join(DOTween.To(() => chefRig.Offer, value => chefRig.Offer = value, 1f, 0.48f));
        yield return offering.WaitForCompletion();
        offering = null;
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySound(SoundManager.Instance.successSparkleSound);
        yield return new WaitForSeconds(0.65f);
        displayBounds = CombinedBounds(display);
        yield return new WaitForEndOfFrame();
        Vector3 min = cameraRef.WorldToScreenPoint(displayBounds.min);
        Vector3 max = cameraRef.WorldToScreenPoint(displayBounds.max);
        float pad = Mathf.Max(max.x - min.x, max.y - min.y) * 0.12f;
        int x = Mathf.Clamp(Mathf.FloorToInt(min.x - pad), 0, Screen.width - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(min.y - pad), 0, Screen.height - 1);
        int width = Mathf.Clamp(Mathf.CeilToInt(max.x + pad) - x, 1, Screen.width - x);
        int height = Mathf.Clamp(Mathf.CeilToInt(max.y + pad) - y, 1, Screen.height - y);
        photograph = new Texture2D(width, height, TextureFormat.RGB24, false);
        photograph.ReadPixels(new Rect(x, y, width, height), 0, 0);
        photograph.Apply();
        ShowCompletion();
    }

    protected static Bounds CombinedBounds(Transform root)
    {
        Bounds result = new Bounds(root.position, Vector3.zero);
        bool first = true;
        ServingChefRig rig = root.GetComponentInChildren<ServingChefRig>();
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            Bounds visible = renderer is SpriteRenderer sprite ? CakeToolScreenBounds.VisibleBounds(sprite) : renderer.bounds;
            if (!(renderer is SpriteRenderer) && rig != null)
                visible.SetMinMax(new Vector3(visible.min.x, Mathf.Max(visible.min.y, rig.WaistWorldY), visible.min.z), visible.max);
            if (first) { result = visible; first = false; }
            else result.Encapsulate(visible);
        }
        return result;
    }

    private void ShowCompletion()
    {
        if (winPanel == null || winCard == null || winPhotograph == null || winPhotoAspect == null || nextModeButton == null)
        {
            Debug.LogError("Assign the scene WIN PANEL references on the final presentation component.", this);
            return;
        }
        winPhotograph.texture = photograph;
        winPhotoAspect.aspectRatio = (float)photograph.width / photograph.height;
        nextModeButton.interactable = true;
        winPanel.transform.SetAsLastSibling();
        winPanel.SetActive(true);
        winCard.DOKill();
        winCard.localScale = Vector3.one * 0.82f;
        winCard.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
    }

    public void NextMode()
    {
        if (navigating) return;
        navigating = true;
        if (nextModeRequested.GetPersistentEventCount() > 0) { nextModeRequested.Invoke(); return; }
        if (Application.CanStreamedLevelBeLoaded(nextModeScene)) SceneManager.LoadScene(nextModeScene);
        else { navigating = false; Debug.LogError("Add the next mode scene to Build Settings: " + nextModeScene, this); }
    }

    protected virtual void OnDestroy() => ResetPresentation();

    protected void ResetPresentation()
    {
        offering?.Kill();
        offering = null;
        if (display != null) display.DOKill();
        if (chef != null) chef.DOKill();
        if (servingRig != null) servingRig.DOKill();
        if (display != null)
        {
            display.gameObject.SetActive(false);
            Destroy(display.gameObject);
        }
        display = chef = servingRig = null;
        presenting = navigating = false;
        foreach (Material material in chefMaterials) if (material != null) Destroy(material);
        chefMaterials.Clear();
        if (winCard != null) winCard.DOKill();
        if (winPhotograph != null) winPhotograph.texture = null;
        if (photograph != null) Destroy(photograph);
        photograph = null;
    }
}
