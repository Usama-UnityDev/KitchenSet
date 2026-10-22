using System.Collections.Generic;
using UnityEngine;

public class DraggableSortingOrder : MonoBehaviour
{
    [Header("Sorting Settings")]
    [Tooltip("Drag karte waqt base sprite ka minimum order in layer")]
    [SerializeField] private int draggingBaseOrder = 100;

    [Tooltip("Agar specific Sorting Layer par bhejna ho (agar khali chhoroge toh layer change nahi hogi, sirf order badhega)")]
    [SerializeField] private string draggingSortingLayerName = "";

    private struct RendererSortState
    {
        public SpriteRenderer Renderer;
        public int OriginalOrder;
        public string OriginalLayerName;
    }

    private readonly List<RendererSortState> cachedRenderers = new List<RendererSortState>();
    private bool isElevated = false;

    private void Awake()
    {
        CacheOriginalSorting();
    }

    /// <summary>
    /// Current hierarchy ke saare SpriteRenderers aur unke initial orders record karta hai
    /// </summary>
    public void CacheOriginalSorting()
    {
        cachedRenderers.Clear();
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);

        foreach (SpriteRenderer sr in renderers)
        {
            if (sr == null) continue;
            cachedRenderers.Add(new RendererSortState
            {
                Renderer = sr,
                OriginalOrder = sr.sortingOrder,
                OriginalLayerName = sr.sortingLayerName
            });
        }
    }

    /// <summary>
    /// Item aur uske tamaam children ko sabse upar le aata hai relative order ko maintain rakhte hue
    /// </summary>
    public void BringToFront()
    {
        if (isElevated) return;

        if (cachedRenderers.Count == 0)
        {
            CacheOriginalSorting();
        }

        int lowestOrder = int.MaxValue;
        for (int i = 0; i < cachedRenderers.Count; i++)
        {
            if (cachedRenderers[i].OriginalOrder < lowestOrder)
            {
                lowestOrder = cachedRenderers[i].OriginalOrder;
            }
        }

        if (lowestOrder == int.MaxValue) lowestOrder = 0;

        for (int i = 0; i < cachedRenderers.Count; i++)
        {
            RendererSortState state = cachedRenderers[i];
            if (state.Renderer == null) continue;

            int relativeOffset = state.OriginalOrder - lowestOrder;
            state.Renderer.sortingOrder = draggingBaseOrder + relativeOffset;

            if (!string.IsNullOrEmpty(draggingSortingLayerName))
            {
                state.Renderer.sortingLayerName = draggingSortingLayerName;
            }
        }

        isElevated = true;
    }

    /// <summary>
    /// Original sorting orders ko restore karta hai
    /// </summary>
    public void ResetSorting()
    {
        if (!isElevated) return;

        for (int i = 0; i < cachedRenderers.Count; i++)
        {
            RendererSortState state = cachedRenderers[i];
            if (state.Renderer == null) continue;

            state.Renderer.sortingOrder = state.OriginalOrder;
            state.Renderer.sortingLayerName = state.OriginalLayerName;
        }

        isElevated = false;
    }
}