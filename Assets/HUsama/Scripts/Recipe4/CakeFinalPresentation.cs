using System.Collections;
using DG.Tweening;
using UnityEngine;

public class CakeFinalPresentation : ServingFinalPresentation
{
    [SerializeField] private SpriteRenderer servingPlate;
    [SerializeField] private Sprite[] sprinkleSprites;
    [SerializeField, Min(0.1f)] private float showerDuration = 1.5f;

    private Material sprinkleMaterial;

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
        BuildServedItem(source, servingPlate, false);
    }

    protected override void OnDestroy()
    {
        if (sprinkleMaterial != null) Destroy(sprinkleMaterial);
        base.OnDestroy();
    }
}
