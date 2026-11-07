using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>The final presentation stage owns the plated cake and its completion photograph.</summary>
public class CakeFinalPresentation : MonoBehaviour
{
    [SerializeField] private SpriteRenderer servingPlate;
    [SerializeField] private Sprite[] sprinkleSprites;
    [SerializeField] private UnityEvent nextModeRequested = new UnityEvent();
    [SerializeField] private Sprite chefSprite;
    [SerializeField] private Shader chefModelShader;
    [SerializeField] private Shader presentationSpriteShader;
    [SerializeField] private string nextModeScene = "Home";
    [SerializeField, Min(0.1f)] private float showerDuration = 1.5f;
    [Header("Scene Win Panel (UIs / Canvas / WIN PANEL)")]
    [SerializeField] private GameObject winPanel;
    [SerializeField] private RectTransform winCard;
    [SerializeField] private RawImage winPhotograph;
    [SerializeField] private AspectRatioFitter winPhotoAspect;
    [SerializeField] private Button nextModeButton;
    private Transform display;
    private Bounds displayBounds;
    private Texture2D photograph;
    private Material sprinkleMaterial;
    private Transform chef;
    private Transform servingRig;
    private ServingChefRig chefRig;
    private readonly System.Collections.Generic.List<Material> chefMaterials = new System.Collections.Generic.List<Material>();
    private bool presenting, navigating;
    public Texture2D Photograph => photograph;

    private void Awake()
    {
        if (winPanel != null) winPanel.SetActive(false);
    }

    public IEnumerator PrepareAndPresent(Transform cakeRoot, Transform cakeItems, SlideOutObject exit)
    {
        yield return SprinkleShower(cakeItems);
        Vector3 size = cakeRoot.localScale;
        yield return cakeRoot.DOScale(size * 1.12f, 0.22f).SetEase(Ease.OutSine).WaitForCompletion();
        yield return cakeRoot.DOScale(size, 0.26f).SetEase(Ease.OutBack).WaitForCompletion();
        if (exit != null)
        {
            exit.direction = SlideOutObject.ExitDirection.Right;
            exit.enabled = true;
            while (cakeRoot.gameObject.activeSelf) yield return null;
        }
        else Debug.LogWarning("Assign the cake's SlideOutObject for the final exit.", this);
        BuildPlatedCake(cakeItems);
        if (SequentialAnimatorController.Instance != null)
            SequentialAnimatorController.Instance.StartSequence(gameObject);
        else gameObject.SetActive(true);
    }

    private IEnumerator SprinkleShower(Transform cakeItems)
    {
        if (sprinkleSprites == null || sprinkleSprites.Length == 0) yield break;
        Sprite sprite = sprinkleSprites[Random.Range(0, sprinkleSprites.Length)];
        Bounds cake = CombinedBounds(cakeItems);
        GameObject shower = new GameObject("Final topping shower");
        var particles = shower.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        shower.transform.position = new Vector3(cake.center.x, cake.max.y + 1.3f, cake.center.z);
        var main = particles.main;
        main.loop = false;
        main.duration = showerDuration;
        main.startLifetime = 1.6f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.13f, 0.25f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 0.45f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;
        var emission = particles.emission;
        emission.rateOverTime = 32f;
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(cake.size.x * 0.75f, 0.04f, 0f);
        var velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.y = -1.6f;
        var sheet = particles.textureSheetAnimation;
        sheet.enabled = true;
        sheet.mode = ParticleSystemAnimationMode.Sprites;
        sheet.AddSprite(sprite);
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        sprinkleMaterial = new Material(Shader.Find("Sprites/Default"));
        renderer.sharedMaterial = sprinkleMaterial;
        renderer.sortingOrder = 35;
        GameObject plane = new GameObject("Cake topping landing plane");
        plane.transform.position = new Vector3(cake.center.x, cake.max.y - 0.25f, cake.center.z);
        var collision = particles.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.Planes;
        collision.SetPlane(0, plane.transform);
        collision.bounce = 0f;
        collision.dampen = 1f;
        collision.lifetimeLoss = 0f;
        particles.Play();
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySound(SoundManager.Instance.successSparkleSound);
        yield return new WaitForSeconds(showerDuration + 1.6f);
        Destroy(shower);
        Destroy(plane);
        Destroy(sprinkleMaterial);
        sprinkleMaterial = null;
    }

    private void BuildPlatedCake(Transform source)
    {
        Camera camera = Camera.main;
        display = new GameObject("Chef carrying the player's cake").transform;
        servingRig = new GameObject("Held plate and decorated cake").transform;
        servingRig.SetParent(display, false);
        Transform cake = Instantiate(source, servingRig, false);
        cake.name = "Player decorated cake";
        cake.gameObject.SetActive(true);
        foreach (Animator animator in cake.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        foreach (Collider2D collider in cake.GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
        SpriteRenderer[] originals = source.GetComponentsInChildren<SpriteRenderer>(true);
        SpriteRenderer[] copies = cake.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < originals.Length; i++)
        {
            var block = new MaterialPropertyBlock();
            originals[i].GetPropertyBlock(block);
            copies[i].SetPropertyBlock(block);
            var spriteMaterial = new Material(presentationSpriteShader);
            copies[i].sharedMaterial = spriteMaterial;
            chefMaterials.Add(spriteMaterial);
            copies[i].sortingLayerID = servingPlate.sortingLayerID;
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
            visual.sortingLayerID = servingPlate.sortingLayerID;
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
        float fit = Mathf.Min(chefHeight * 0.34f / cakeBounds.size.y, chefBounds.size.x * 0.86f / cakeBounds.size.x);
        cake.localScale *= fit;
        cakeBounds = CombinedBounds(cake);

        Transform plateTransform = new GameObject("Serving plate held by both hands").transform;
        plateTransform.SetParent(servingRig, false);
        SpriteRenderer plate = plateTransform.gameObject.AddComponent<SpriteRenderer>();
        plate.sprite = servingPlate.sprite;
        var plateMaterial = new Material(presentationSpriteShader);
        plate.sharedMaterial = plateMaterial;
        chefMaterials.Add(plateMaterial);
        plate.sortingLayerID = servingPlate.sortingLayerID;
        plate.sortingOrder = 99;
        Bounds plateBounds = CakeToolScreenBounds.VisibleBounds(plate);
        float plateScale = cakeBounds.size.x * 1.18f / plateBounds.size.x;
        // The original plate artwork is almost circular; the serving view needs an ellipse.
        plateTransform.localScale = new Vector3(plateScale, plateScale * 0.48f, plateScale);
        plateBounds = CakeToolScreenBounds.VisibleBounds(plate);
        Vector3 grip = new Vector3(chefBounds.center.x, chefBounds.min.y + chefHeight * 0.24f, chef.position.z - 0.3f);
        plateTransform.position += grip - plateBounds.center;
        plateBounds = CakeToolScreenBounds.VisibleBounds(plate);
        // The cake's bottom ellipse overlaps the inner plate, leaving just the rim visible.
        Vector3 cakeBase = new Vector3(cakeBounds.center.x, cakeBounds.min.y, cakeBounds.center.z);
        Vector3 landing = new Vector3(plateBounds.center.x, plateBounds.center.y - plateBounds.size.y * 0.18f, grip.z - 0.02f);
        cake.position += landing - cakeBase;
        chefRig.Initialize(plateTransform, plate);

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

    private void OnEnable()
    {
        if (display != null && !presenting) { presenting = true; StartCoroutine(ArriveAndPhotograph()); }
    }

    private IEnumerator ArriveAndPhotograph()
    {
        Camera cameraRef = Camera.main;
        Vector3 home = display.position;
        float offset = cameraRef.orthographic ? cameraRef.orthographicSize * cameraRef.aspect * 2.5f : displayBounds.size.x * 3f;
        display.position = home + Vector3.left * offset;
        yield return display.DOMove(home, 0.85f).SetEase(Ease.OutCubic).WaitForCompletion();
        yield return new WaitForSeconds(0.25f);
        // Arms track the serving plate through the offering gesture, preserving both hand contacts.
        Vector3 traySize = servingRig.localScale;
        Vector3 trayPosition = servingRig.localPosition;
        Sequence offer = DOTween.Sequence();
        offer.Append(servingRig.DOScale(traySize * 1.10f, 0.48f).SetEase(Ease.InOutSine));
        offer.Join(servingRig.DOLocalMove(trayPosition + Vector3.up * (displayBounds.size.y * 0.025f), 0.48f).SetEase(Ease.InOutSine));
        offer.Join(DOTween.To(() => chefRig.Offer, value => chefRig.Offer = value, 1f, 0.48f));
        yield return offer.WaitForCompletion();
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

    private static Bounds CombinedBounds(Transform root)
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
            Debug.LogError("Assign the scene WIN PANEL references on CakeFinalPresentation.", this);
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

    private void OnDestroy()
    {
        if (display != null) display.DOKill();
        if (chef != null) chef.DOKill();
        if (servingRig != null) servingRig.DOKill();
        foreach (Material material in chefMaterials) if (material != null) Destroy(material);
        if (winCard != null) winCard.DOKill();
        if (winPhotograph != null) winPhotograph.texture = null;
        if (photograph != null) Destroy(photograph);
        if (sprinkleMaterial != null) Destroy(sprinkleMaterial);
    }
}
