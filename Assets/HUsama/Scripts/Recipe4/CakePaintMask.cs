using System;
using UnityEngine;

/// <summary>A small per-sprite paint mask. Only pixels underneath the tool are revealed.</summary>
public sealed class CakePaintMask : IDisposable
{
    private const int Size = 96;
    private readonly SpriteRenderer target;
    private readonly Material material;
    private readonly Texture2D mask;
    private readonly Color32[] pixels = new Color32[Size * Size];
    private readonly bool[] required = new bool[Size * Size];
    private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
    private int total, painted;
    private Vector3 previous;
    private bool hasPrevious;
    public float Coverage => total > 0 ? (float)painted / total : 0f;

    public CakePaintMask(SpriteRenderer renderer, Shader shader)
    {
        target = renderer;
        material = new Material(shader);
        target.sharedMaterial = material;
        mask = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true);
        mask.wrapMode = TextureWrapMode.Clamp;
        mask.filterMode = FilterMode.Bilinear;
        // Read alpha through the GPU so source textures can remain non-readable in builds.
        RenderTexture rt = RenderTexture.GetTemporary(Size, Size, 0, RenderTextureFormat.ARGB32);
        RenderTexture saved = RenderTexture.active;
        Texture2D alpha = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        try
        {
            Sprite sprite = target.sprite;
            Rect rect = sprite.textureRect;
            Vector2 textureSize = new Vector2(sprite.texture.width, sprite.texture.height);
            Graphics.Blit(sprite.texture, rt, rect.size / textureSize, rect.position / textureSize);
            RenderTexture.active = rt;
            alpha.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            Color32[] source = alpha.GetPixels32();
            for (int i = 0; i < source.Length; i++)
            {
                required[i] = source[i].a > 24;
                if (required[i]) total++;
            }
        }
        finally
        {
            RenderTexture.active = saved;
            RenderTexture.ReleaseTemporary(rt);
            UnityEngine.Object.Destroy(alpha);
        }
        Bounds bounds = target.sprite.bounds;
        properties.SetVector("_LocalRect", new Vector4(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y));
        properties.SetTexture("_PaintTex", mask);
        Upload();
    }

    public void BreakStroke() { hasPrevious = false; }

    public bool SpreadFromCenter(float progress)
    {
        if (progress >= 1f) { Reveal(); return true; }
        float radius = Mathf.Sqrt(Mathf.Clamp01(progress)) * 1.45f;
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float dx = ((x + 0.5f) / Size - 0.5f) * 2f;
            float dy = ((y + 0.5f) / Size - 0.5f) * 2f;
            byte value = (byte)(Mathf.Clamp01((radius - Mathf.Sqrt(dx * dx + dy * dy)) / 0.12f) * 255f);
            int i = y * Size + x;
            if (value <= pixels[i].r) continue;
            if (required[i] && pixels[i].r < 200 && value >= 200) painted++;
            pixels[i] = new Color32(value, value, value, 255);
        }
        Upload();
        return Coverage >= 0.97f;
    }

    public bool Paint(Vector3 worldPoint, float worldRadius)
    {
        Vector3 local = target.transform.InverseTransformPoint(worldPoint);
        Bounds bounds = target.sprite.bounds;
        Vector3 scale = target.transform.lossyScale;
        Vector2 radius = new Vector2(worldRadius / Mathf.Max(0.001f, bounds.size.x * Mathf.Abs(scale.x)),
            worldRadius / Mathf.Max(0.001f, bounds.size.y * Mathf.Abs(scale.y)));
        // Interpolate fast pointer movement; never join strokes across a release or outside the cake.
        Vector3 start = hasPrevious ? previous : worldPoint;
        int steps = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(start, worldPoint) / (worldRadius * 0.35f)), 1, 24);
        for (int step = 1; step <= steps; step++)
        {
            Vector3 point = target.transform.InverseTransformPoint(Vector3.Lerp(start, worldPoint, (float)step / steps));
            Vector2 center = new Vector2((point.x - bounds.min.x) / bounds.size.x, (point.y - bounds.min.y) / bounds.size.y);
            Stamp(center, radius);
        }
        previous = worldPoint;
        hasPrevious = true;
        Upload();
        return Coverage >= 0.97f;
    }

    private void Stamp(Vector2 center, Vector2 radius)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt((center.x - radius.x) * Size));
        int maxX = Mathf.Min(Size - 1, Mathf.CeilToInt((center.x + radius.x) * Size));
        int minY = Mathf.Max(0, Mathf.FloorToInt((center.y - radius.y) * Size));
        int maxY = Mathf.Min(Size - 1, Mathf.CeilToInt((center.y + radius.y) * Size));
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            float dx = ((x + 0.5f) / Size - center.x) / radius.x;
            float dy = ((y + 0.5f) / Size - center.y) / radius.y;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            byte value = (byte)(Mathf.Clamp01((1f - distance) / 0.22f) * 255);
            int index = y * Size + x;
            if (value <= pixels[index].r) continue;
            if (required[index] && pixels[index].r < 200 && value >= 200) painted++;
            pixels[index] = new Color32(value, value, value, 255);
        }
    }

    public void Reveal()
    {
        properties.SetFloat("_Complete", 1f);
        target.SetPropertyBlock(properties);
        painted = total;
    }

    private void Upload()
    {
        mask.SetPixels32(pixels);
        mask.Apply(false, false);
        target.SetPropertyBlock(properties);
    }

    public void Dispose()
    {
        UnityEngine.Object.Destroy(mask);
        UnityEngine.Object.Destroy(material);
    }
}
