using UnityEngine;

public class HeatmapRenderer : MonoBehaviour
{
    [Tooltip("Traffic history window only. Kill and death zones persist until playback is rewound before the event.")]
    [SerializeField, Min(0f)] private float activityWindowSeconds = 30f;
    [SerializeField, Range(0.05f, 1f)] private float refreshInterval = 0.2f;
    [SerializeField, Range(32, 256)] private int resolution = 128;
    [SerializeField, Range(0.005f, 0.1f)] private float influenceRadius = 0.05f;
    [SerializeField, Range(0f, 1f)] private float opacity = 0.65f;
    [SerializeField, Min(0.1f)] private float saturation = 3f;
    [Tooltip("Combat events are sparse; one fresh event should be clearly visible.")]
    [SerializeField, Min(0.1f)] private float combatSaturation = 1f;

    [SerializeField] private Color killColor = new Color(1f, 0.15f, 0.05f);
    [SerializeField] private Color deathColor = new Color(0.7f, 0.15f, 1f);
    [SerializeField] private Color trafficColor = new Color(0.05f, 0.8f, 1f);

    [SerializeField] private MatchPlaybackController playback;
    private sealed class Layer
    {
        public HeatmapTimeline timeline;
        public SpriteRenderer renderer;
        public Texture2D texture;
        public Sprite sprite;
        public Color color;
        public HeatmapZone zone;
    }
    private Layer[] layers;
    private SpriteRenderer map;
    private Material material;
    private float[] density;
    private Color32[] pixels;
    private double startTime;
    private double nextRefresh;
    private int size;

    public void Initialize(MatchData match, SpriteRenderer mapRenderer, double playbackStartTime)
    {
        Clear();

        var oldRenderer = GetComponent<SpriteRenderer>();

        if (oldRenderer != null) oldRenderer.enabled = false;

        if (match == null || mapRenderer == null || mapRenderer.sprite == null) return;

        map = mapRenderer;

        startTime = playbackStartTime;

        size = Mathf.Clamp(resolution, 32, 256);

        density = new float[size * size];

        pixels = new Color32[density.Length];

        Shader shader = Resources.Load<Shader>("HeatmapOverlay");

        if (shader == null)
        {
            Debug.LogError("HeatmapOverlay shader is missing from Resources.", this);
            return;
        }
        material = new Material(shader);

        layers = new[]
        {
            CreateLayer(match, HeatmapZone.Traffic, "Traffic Heatmap", trafficColor),
            CreateLayer(match, HeatmapZone.Deaths, "Death Heatmap", deathColor),
            CreateLayer(match, HeatmapZone.Kills, "Kill Heatmap", killColor)
        };
        RenderAtTime(0);

        nextRefresh = playbackStartTime + refreshInterval;
    }

    private Layer CreateLayer(MatchData match, HeatmapZone zone, string name, Color color)
    {
        var layer = new Layer 
        { 
            timeline = new HeatmapTimeline(match, zone), color = color, zone = zone
        };
        layer.texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
        };
        layer.sprite = Sprite.Create(layer.texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
        layer.renderer = new GameObject(name).AddComponent<SpriteRenderer>();
        layer.renderer.transform.SetParent(map.transform, false);

        Bounds bounds = map.sprite.bounds;
        
        layer.renderer.transform.localPosition = bounds.center;
        layer.renderer.transform.localScale = new Vector3(bounds.size.x, bounds.size.y, 1f);
        layer.renderer.sprite = layer.sprite;
        layer.renderer.sharedMaterial = material;
        layer.renderer.sortingLayerID = map.sortingLayerID;
        // Explicit ordering prevents dense traffic from covering sparse combat zones.
        layer.renderer.sortingOrder = map.sortingOrder +
            (zone == HeatmapZone.Traffic ? 1 : zone == HeatmapZone.Deaths ? 2 : 3);
        layer.renderer.flipX = map.flipX;
        layer.renderer.flipY = map.flipY;
        layer.renderer.enabled = false;

        return layer;
    }

    private double lastRenderedTime = double.NaN;

    private void FixedUpdate()
    {
        if (playback == null || layers == null) return;

        double time = playback.CurrentTime;

        if (time == lastRenderedTime) return;

        RenderAtTime(time);

        lastRenderedTime = time;
    }

    public void RenderAtTime(double elapsed)
    {
        if (layers == null || map == null) return;

        foreach (var layer in layers)
        {
            System.Array.Clear(density, 0, density.Length);

            int activeCount = 0;

            float sigma = Mathf.Max(0.5f, influenceRadius * size);

            int radius = Mathf.CeilToInt(3 * sigma);

            // Combat zones accumulate at their recorded locations; only traffic fades.
            double window = layer.zone == HeatmapZone.Traffic ? activityWindowSeconds : 0;

            foreach (var entry in layer.timeline.Active(elapsed, window))
            {
                float weight = HeatmapTimeline.Weight(entry.timeSeconds, elapsed, window);

                if (weight <= 0) continue;

                activeCount++;

                float cx = entry.u * (size - 1), cy = entry.v * (size - 1);
                
                int minX = Mathf.Max(0, Mathf.FloorToInt(cx) - radius);
                int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(cx) + radius);
                int minY = Mathf.Max(0, Mathf.FloorToInt(cy) - radius);
                int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(cy) + radius);
                
                for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = x - cx, dy = y - cy;

                    density[y * size + x] += weight * Mathf.Exp(-(dx * dx + dy * dy) / (2 * sigma * sigma));
                }
            }
            layer.renderer.gameObject.SetActive(activeCount > 0);
            layer.renderer.enabled = activeCount > 0 && isActiveAndEnabled;
            
            if (activeCount == 0) continue;
            
            for (int i = 0; i < pixels.Length; i++)
            {
                // Fixed saturation preserves fading: do not renormalize every frame.
                float threshold = layer.zone == HeatmapZone.Traffic ? saturation : combatSaturation;
                float intensity = Mathf.Clamp01(density[i] / Mathf.Max(0.1f, threshold));
                Color color = Color.Lerp(layer.color, Color.white, intensity * 0.4f);
                color.a = intensity * opacity;
                pixels[i] = color;
            }
            layer.texture.SetPixels32(pixels);
            layer.texture.Apply(false, false);
        }
    }

    private void OnEnable() { nextRefresh = 0; }
    private void OnDisable()
    {
        if (layers != null)
        {
            foreach (var layer in layers)
            {
                if(layer.renderer != null)
                {
                    layer.renderer.enabled = false;
                }
            }
        }
    }
    private void OnDestroy() => Clear();
    private void Clear()
    {
        if (layers != null)
        foreach (var layer in layers)
        {
            if (layer.renderer != null) Destroy(layer.renderer.gameObject);
            if (layer.sprite != null) Destroy(layer.sprite);
            if (layer.texture != null) Destroy(layer.texture);
        }
        layers = null;
        if (material != null) Destroy(material);
    }
}
