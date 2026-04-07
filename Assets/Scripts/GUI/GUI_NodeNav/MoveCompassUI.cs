using UnityEngine;
using UnityEngine.UI;

namespace FNaS.Gameplay {
    public class MoveCompassUI : MonoBehaviour {
        [Header("References")]
        [SerializeField] private ViewController viewController;
        [SerializeField] private PlayerWaypointController mover;

        [Header("UI")]
        [SerializeField] private CanvasGroup canvasGroup;
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

        private float wAlpha, aAlpha, sAlpha, dAlpha;

        private void Awake() {
            if (!viewController) viewController = FindFirstObjectByType<ViewController>();
            if (!mover) mover = FindFirstObjectByType<PlayerWaypointController>();
            if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
        }

        private void Update() {
            if (!viewController || !mover)
                return;

            Waypoint waypoint = viewController.CurrentWaypoint;
            View view = viewController.CurrentView;

            if (!waypoint) {
                ApplyTick(ref wAlpha, tickW, unavailableAlpha);
                ApplyTick(ref aAlpha, tickA, unavailableAlpha);
                ApplyTick(ref sAlpha, tickS, unavailableAlpha);
                ApplyTick(ref dAlpha, tickD, unavailableAlpha);
                SetOverall(1f);
                return;
            }

            bool canW = CanMove(Direction.W, waypoint, view);
            bool canA = CanMove(Direction.A, waypoint, view);
            bool canS = CanMove(Direction.S, waypoint, view);
            bool canD = CanMove(Direction.D, waypoint, view);

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

            SetOverall(1f);
        }

        private float GetTargetAlpha(Direction dir, bool can, bool moving, Direction? chosen) {
            if (!moving)
                return can ? availableAlpha : unavailableAlpha;

            if (chosen.HasValue && chosen.Value == dir)
                return availableAlpha;

            return unavailableAlpha;
        }

        private bool CanMove(Direction dir, Waypoint waypoint, View view) {
            if (waypoint == null) return false;

            if (view != null) {
                var ov = view.GetOverride(dir);

                if (ov.enabled) {
                    return ov.targetWaypoint != null;
                }
            }

            WaypointTransition fallback = waypoint.GetTransition(dir);
            return fallback != null && fallback.target != null;
        }

        private void SmoothTick(ref float current, Image img, float target) {
            if (!img) return;
            float k = 1f - Mathf.Exp(-fadeSpeed * Time.deltaTime);
            current = Mathf.Lerp(current, target, k);

            Color c = img.color;
            c.a = current;
            img.color = c;

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
}