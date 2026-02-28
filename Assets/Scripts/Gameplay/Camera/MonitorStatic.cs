using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class MonitorStatic : MonoBehaviour {
    [Header("Static Look")]
    [Range(0f, 1f)]
    [SerializeField] private float intensity = 0.25f;

    [SerializeField] private float flickerSpeed = 10f;
    [SerializeField] private float scrollSpeed = 0.25f;

    [Header("Texture Settings")]
    [SerializeField] private int texSize = 256;

    private RawImage img;
    private Texture2D noiseTex;
    private Color[] pixels;
    private float t;

    private void Awake() {
        img = GetComponent<RawImage>();

        noiseTex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        noiseTex.wrapMode = TextureWrapMode.Repeat;
        noiseTex.filterMode = FilterMode.Point;

        pixels = new Color[texSize * texSize];

        img.texture = noiseTex;
        img.color = new Color(1f, 1f, 1f, intensity);
    }

    private void OnDestroy() {
        if (noiseTex != null) Destroy(noiseTex);
    }

    private void Update() {
        t += Time.deltaTime;

        // Flicker the alpha a bit (feels like camera interference)
        float flicker = 0.75f + 0.25f * Mathf.Sin(t * flickerSpeed);
        img.color = new Color(1f, 1f, 1f, intensity * flicker);

        // Scroll UVs for movement
        Rect r = img.uvRect;
        r.x += scrollSpeed * Time.deltaTime;
        r.y += (scrollSpeed * 0.5f) * Time.deltaTime;
        img.uvRect = r;

        // Update noise (cheap-ish at 256; don’t crank too high yet)
        int n = pixels.Length;
        for (int i = 0; i < n; i++) {
            float v = Random.value;
            pixels[i] = new Color(v, v, v, 1f);
        }

        noiseTex.SetPixels(pixels);
        noiseTex.Apply(false, false);
    }
}