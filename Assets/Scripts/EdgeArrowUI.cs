using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class EdgeArrowUI : MonoBehaviour {
    [Header("References")]
    [SerializeField] private NodeViewController viewController;
    [SerializeField] private PlayerNodeController mover;

    [Header("Arrow Images")]
    [SerializeField] private Graphic leftArrow;
    [SerializeField] private Graphic rightArrow;
    [SerializeField] private Graphic upArrow;
    [SerializeField] private Graphic downArrow;

    [Header("Style")]
    [Range(0f, 1f)]
    [SerializeField] private float visibleAlpha = 0.18f; // faint
    [SerializeField] private float fadeInSeconds = 0.06f; // extremely quick
    [SerializeField] private float fadeOutSeconds = 0.06f;

    private Coroutine fadeRoutine;
    private float currentAlpha = 0f;

    private void Awake() {
        if (!viewController) viewController = FindFirstObjectByType<NodeViewController>();
        if (!mover) mover = FindFirstObjectByType<PlayerNodeController>();

        SetAllAlpha(0f);
        UpdateArrowsVisible(false, false, false, false);
    }

    private void OnEnable() {
        if (!viewController) viewController = FindFirstObjectByType<NodeViewController>();
        if (!mover) mover = FindFirstObjectByType<PlayerNodeController>();

        if (viewController != null) {
            viewController.OnEnteredNodeOrView += FadeInQuick;
            viewController.OnBeginMove += FadeOutQuick;
        }

        SetAllAlpha(0f);
    }

    private void OnDisable() {
        if (viewController != null) {
            viewController.OnEnteredNodeOrView -= FadeInQuick;
            viewController.OnBeginMove -= FadeOutQuick;
        }
    }

    private void Update() {
        if (viewController == null || mover == null)
            return;

        // Hide during node-to-node movement
        if (mover.IsMoving) {
            // Hard hide; no fade while moving
            StopFade();
            SetAllAlpha(0f);
            return;
        }

        // Read edge availability from current view
        NodeView v = viewController.CurrentView;
        if (v == null) {
            StopFade();
            SetAllAlpha(0f);
            return;
        }

        bool canL = HasEdge(v, EdgeDir.Left);
        bool canR = HasEdge(v, EdgeDir.Right);
        bool canU = HasEdge(v, EdgeDir.Up);
        bool canD = HasEdge(v, EdgeDir.Down);

        UpdateArrowsVisible(canL, canR, canU, canD);
        // Alpha is driven by NodeViewController events (below), but if you prefer always-on:
        // SetAllAlpha(visibleAlpha);
    }

    private static bool HasEdge(NodeView v, EdgeDir dir) {
        var link = v.GetEdge(dir);
        if (link.action == NodeView.LinkAction.None) return false;
        if (link.action == NodeView.LinkAction.GoToView && link.targetView == null) return false;
        // Back is valid if your history stack is non-empty, but UI can still hint.
        return true;
    }

    private void UpdateArrowsVisible(bool left, bool right, bool up, bool down) {
        if (leftArrow) leftArrow.gameObject.SetActive(left);
        if (rightArrow) rightArrow.gameObject.SetActive(right);
        if (upArrow) upArrow.gameObject.SetActive(up);
        if (downArrow) downArrow.gameObject.SetActive(down);
    }

    private void SetAllAlpha(float a) {
        currentAlpha = a;
        SetAlpha(leftArrow, a);
        SetAlpha(rightArrow, a);
        SetAlpha(upArrow, a);
        SetAlpha(downArrow, a);
    }

    private static void SetAlpha(Graphic g, float a) {
        if (!g) return;
        var c = g.color;
        c.a = a;
        g.color = c;
    }

    private void StopFade() {
        if (fadeRoutine != null) {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }
    }

    // --- Called by NodeViewController via events (recommended) ---
    public void FadeInQuick() {
        if (mover != null && mover.IsMoving) return;
        StartFadeTo(visibleAlpha, fadeInSeconds);
    }

    public void FadeOutQuick() {
        StartFadeTo(0f, fadeOutSeconds);
    }

    private void StartFadeTo(float target, float seconds) {
        StopFade();
        fadeRoutine = StartCoroutine(FadeRoutine(target, Mathf.Max(0.0001f, seconds)));
    }

    private IEnumerator FadeRoutine(float target, float seconds) {
        float start = currentAlpha;
        float t = 0f;

        while (t < 1f) {
            t += Time.deltaTime / seconds;
            float a = Mathf.Lerp(start, target, Mathf.Clamp01(t));
            SetAllAlpha(a);
            yield return null;
        }

        SetAllAlpha(target);
        fadeRoutine = null;
    }
}