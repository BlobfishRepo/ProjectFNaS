using UnityEngine;
using UnityEngine.UI;

public class MoveCompassUI : MonoBehaviour {
    [Header("References")]
    [SerializeField] private NodeViewController viewController;
    [SerializeField] private PlayerNodeController mover;

    [Header("UI")]
    [SerializeField] private CanvasGroup canvasGroup; // optional but recommended
    [SerializeField] private Image tickW;
    [SerializeField] private Image tickA;
    [SerializeField] private Image tickS;
    [SerializeField] private Image tickD;

    [Header("Tuning")]
    [Tooltip("Alpha for available directions when NOT moving.")]
    [Range(0f, 1f)] public float availableAlpha = 0.9f;

    [Tooltip("Alpha for unavailable directions when NOT moving.")]
    [Range(0f, 1f)] public float unavailableAlpha = 0.15f;

    [Tooltip("Overall multiplier while moving between nodes (grayed out).")]
    [Range(0f, 1f)] public float movingMultiplier = 0.25f;

    [Tooltip("How quickly the UI responds (higher = snappier).")]
    public float fadeSpeed = 18f;

    // internal smoothed alphas
    private float wAlpha, aAlpha, sAlpha, dAlpha;

    private void Awake() {
        if (!viewController) viewController = FindFirstObjectByType<NodeViewController>();
        if (!mover) mover = FindFirstObjectByType<PlayerNodeController>();
        if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Update() {
        if (!viewController || !mover)
            return;

        Node node = viewController.CurrentNode;
        NodeView view = viewController.CurrentView;

        // If we somehow have no node yet, just dim everything
        if (!node) {
            ApplyTick(ref wAlpha, tickW, unavailableAlpha);
            ApplyTick(ref aAlpha, tickA, unavailableAlpha);
            ApplyTick(ref sAlpha, tickS, unavailableAlpha);
            ApplyTick(ref dAlpha, tickD, unavailableAlpha);
            SetOverall(1f);
            return;
        }

        // Determine availability (view override first, then node)
        bool canW = CanMove(Direction.W, node, view);
        bool canA = CanMove(Direction.A, node, view);
        bool canS = CanMove(Direction.S, node, view);
        bool canD = CanMove(Direction.D, node, view);

        Direction? chosen = viewController.ActiveMoveDir;
        bool moving = mover.IsMoving;

        float targetW = GetTargetAlpha(Direction.W, canW, moving, chosen);
        float targetA = GetTargetAlpha(Direction.A, canA, moving, chosen);
        float targetS = GetTargetAlpha(Direction.S, canS, moving, chosen);
        float targetD = GetTargetAlpha(Direction.D, canD, moving, chosen);

        SmoothTick(ref wAlpha, tickW, targetW);
        SmoothTick(ref aAlpha, tickA, targetA);
        SmoothTick(ref sAlpha, tickS, targetS);
        SmoothTick(ref dAlpha, tickD, targetD);

        // Keep the whole widget present, but grayed during movement
        SetOverall(1f);
    }

    private float GetTargetAlpha(Direction dir, bool can, bool moving, Direction? chosen) {
        if (!moving)
            return can ? availableAlpha : unavailableAlpha;

        // moving: everything dims, except the chosen direction (which stays “lit”)
        if (chosen.HasValue && chosen.Value == dir)
            return availableAlpha;      // keep bright
        return unavailableAlpha;        // gray out all others (even if they were available)
    }

    private bool CanMove(Direction dir, Node node, NodeView view) {
        if (node == null) return false;

        // 1) View override
        if (view != null) {
            var ov = view.GetOverride(dir);

            if (ov.enabled) {
                if (ov.targetNode == null) return false;

                // Resolve the actual NodeTransition on THIS node for this key
                // and ensure it points to the override node.
                NodeTransition tr = node.GetTransition(dir);
                return tr != null && tr.target == ov.targetNode;
            }
        }

        // 2) Default node mapping
        NodeTransition fallback = node.GetTransition(dir);
        return fallback != null && fallback.target != null;
    }

    private void SmoothTick(ref float current, Image img, float target) {
        if (!img) return;
        float k = 1f - Mathf.Exp(-fadeSpeed * Time.deltaTime);
        current = Mathf.Lerp(current, target, k);

        Color c = img.color;
        c.a = current;
        img.color = c;

        // optional: disable raycast/overdraw when basically invisible
        img.enabled = current > 0.01f;
    }

    private void ApplyTick(ref float current, Image img, float alpha) {
        current = alpha;
        if (!img) return;
        Color c = img.color;
        c.a = alpha;
        img.color = c;
        img.enabled = alpha > 0.01f;
    }

    private void SetOverall(float a) {
        if (canvasGroup) canvasGroup.alpha = a;
    }
}