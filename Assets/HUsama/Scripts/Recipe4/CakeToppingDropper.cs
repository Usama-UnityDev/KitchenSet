using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class CakeToppingDropper : MonoBehaviour
{
    [System.Serializable]
    public class ToppingGroup
    {
        public string groupName = "Strawberries";
        [Tooltip("Button ID ya tag jo UI se match karega")]
        public string toppingId = "strawberry";
        [Tooltip("Cake par drop hone wale target objects (pehle se cake par placed honge ya parent container)")]
        public List<Transform> toppingTargets = new List<Transform>();
        [Tooltip("Upar se girne ka height offset")]
        public float spawnHeight = 3.5f;
        [Tooltip("Ek ke baad doosri cheez girne ka delay")]
        public float dropInterval = 0.12f;
        [Tooltip("Drop hone ka animation time")]
        public float dropDuration = 0.45f;
    }

    [Header("--- Topping Sets ---")]
    [SerializeField] private List<ToppingGroup> toppingGroups = new List<ToppingGroup>();

    [Header("--- Polish & Audio ---")]
    [SerializeField] private AudioClip dropSound;
    [SerializeField] private ParticleSystem completeParticles;

    public Action<string> OnToppingCompleted;

    private HashSet<string> completedToppings = new HashSet<string>();

    private void Start()
    {
        // Shuru mein tamam toppings hidden/disabled rahein
        foreach (var group in toppingGroups)
        {
            if (group == null) continue;
            foreach (var item in group.toppingTargets)
            {
                if (item != null)
                    item.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// UI Button click karne par yeh method call hoga (e.g. "strawberry", "sprinkles", "cluster")
    /// </summary>
    public void DropTopping(string toppingId)
    {
        ToppingGroup targetGroup = toppingGroups.Find(g => g.toppingId.Equals(toppingId, StringComparison.OrdinalIgnoreCase));
        if (targetGroup == null)
        {
            Debug.LogWarning($"ToppingGroup with ID '{toppingId}' not found!");
            return;
        }

        StartCoroutine(DropSequenceRoutine(targetGroup));
    }

    private IEnumerator DropSequenceRoutine(ToppingGroup group)
    {
        for (int i = 0; i < group.toppingTargets.Count; i++)
        {
            Transform target = group.toppingTargets[i];
            if (target == null) continue;

            Vector3 finalLocalPos = target.localPosition;
            Vector3 finalScale = target.localScale;

            // Upar se spawn karein
            target.localPosition = finalLocalPos + new Vector3(0f, group.spawnHeight, 0f);
            target.localScale = finalScale * 0.4f;
            target.gameObject.SetActive(true);

            // Audio sound
            SoundManager.Instance?.PlaySound(dropSound != null ? dropSound : SoundManager.Instance.itemPickupSound);

            // Drop Bounce animation (Gravity + Bounce)
            Sequence dropSeq = DOTween.Sequence();
            dropSeq.Join(target.DOLocalMove(finalLocalPos, group.dropDuration).SetEase(Ease.OutBounce));
            dropSeq.Join(target.DOScale(finalScale, group.dropDuration * 0.7f).SetEase(Ease.OutBack));

            yield return new WaitForSeconds(group.dropInterval);
        }

        // Final pop & sparkles
        if (completeParticles != null)
        {
            completeParticles.Play();
        }

        if (SoundManager.Instance != null && SoundManager.Instance.successSparkleSound != null)
        {
            SoundManager.Instance.PlaySound(SoundManager.Instance.successSparkleSound);
        }

        completedToppings.Add(group.toppingId);
        OnToppingCompleted?.Invoke(group.toppingId);
    }
}
