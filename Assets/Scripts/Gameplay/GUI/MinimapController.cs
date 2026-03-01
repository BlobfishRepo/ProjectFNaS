using UnityEngine;

public class MinimapController : MonoBehaviour {
    [Header("References")]
    public RectTransform mapRect;        // the UI Image rect (MapImage)
    public RectTransform playerDot;      // the UI Image rect (PlayerDot)
    public Transform playerWorld;        // player transform (or yaw pivot)

    [Header("World bounds (apartment)")]
    public Transform worldBottomLeft;    // empty GameObject placed at BL corner
    public Transform worldTopRight;      // empty GameObject placed at TR corner

    [Header("Dot padding inside map (optional)")]
    public Vector2 mapPadding = new Vector2(6f, 6f);

    void LateUpdate() {
        if (!playerWorld || !mapRect || !playerDot || !worldBottomLeft || !worldTopRight)
            return;

        Vector3 bl = worldBottomLeft.position;
        Vector3 tr = worldTopRight.position;

        Vector3 p = playerWorld.position;

        // Normalize (x,z) into 0..1 range
        float u = Mathf.InverseLerp(bl.x, tr.x, p.x);
        u = 1f - u;
        float v = Mathf.InverseLerp(bl.z, tr.z, p.z);
        v = 1f - v;

        // Clamp so we never leave the minimap
        u = Mathf.Clamp01(u);
        v = Mathf.Clamp01(v);

        // Convert normalized -> local UI coordinates in mapRect
        Vector2 size = mapRect.rect.size;
        Vector2 local =
            new Vector2(
                Mathf.Lerp(-size.x * 0.5f + mapPadding.x, size.x * 0.5f - mapPadding.x, u),
                Mathf.Lerp(-size.y * 0.5f + mapPadding.y, size.y * 0.5f - mapPadding.y, v)
            );

        playerDot.anchoredPosition = local;
    }
}