using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using FNaS.Settings;
using FNaS.Systems;

namespace FNaS.Gameplay {
    public class EdgeArrowUI : MonoBehaviour, IRuntimeSettingsConsumer {
        [Header("References")]
        [SerializeField] private ViewController viewController;
        [SerializeField] private PlayerWaypointController mover;

        [Header("Arrow Images")]
        [SerializeField] private Graphic leftArrow;
        [SerializeField] private Graphic rightArrow;
        [SerializeField] private Graphic upArrow;
        [SerializeField] private Graphic downArrow;

        [Header("Style")]
        [Range(0f, 1f)]
        [SerializeField] private float visibleAlpha = 0.18f;

        [SerializeField] private float fadeInSeconds = 0.06f;
        [SerializeField] private float fadeOutSeconds = 0.06f;

        [Header("Colors")]
        [SerializeField] private Color defaultBlueColor = new Color(0.2f, 0.45f, 1f);
        [SerializeField] private Color whiteColor = Color.white;

        private Coroutine fadeRoutine;
        private float currentAlpha = 0f;
        private bool useWhiteArrows;

        private void Awake() {
            ResolveReferences();
            ApplyRuntimeSettings(RuntimeGameSettings.Instance);

            SetAllAlpha(0f);
            UpdateArrowsVisible(false, false, false, false);
        }

        private void OnEnable() {
            ResolveReferences();

            if (viewController != null) {
                viewController.OnEnteredWaypointOrView += FadeInQuick;
            }

            if (RuntimeGameSettings.Instance != null) {
                RuntimeGameSettings.Instance.OnSettingsChanged += HandleSettingsChanged;
                ApplyRuntimeSettings(RuntimeGameSettings.Instance);
            }

            SetAllAlpha(0f);
        }

        private void OnDisable() {
            if (viewController != null) {
                viewController.OnEnteredWaypointOrView -= FadeInQuick;
            }

            if (RuntimeGameSettings.Instance != null) {
                RuntimeGameSettings.Instance.OnSettingsChanged -= HandleSettingsChanged;
            }
        }

        private void Update() {
            if (viewController == null || mover == null) {
                ResolveReferences();
                if (viewController == null || mover == null) return;
            }

            if (mover.IsMoving) {
                StopFade();
                SetAllAlpha(0f);
                return;
            }

            View v = viewController.CurrentView;
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
        }

        public void ApplyRuntimeSettings(RuntimeGameSettings settings) {
            useWhiteArrows = settings != null && settings.GetBool("player.edgeArrowsUseWhite");
            ApplyArrowColor();
        }

        private void HandleSettingsChanged() {
            ApplyRuntimeSettings(RuntimeGameSettings.Instance);
        }

        private void ResolveReferences() {
            if (!viewController) viewController = FindFirstObjectByType<ViewController>();
            if (!mover) mover = FindFirstObjectByType<PlayerWaypointController>();
        }

        private static bool HasEdge(View v, EdgeDir dir) {
            var link = v.GetEdge(dir);
            if (link.action == View.LinkAction.None) return false;
            if (link.action == View.LinkAction.GoToView && link.targetView == null) return false;
            return true;
        }

        private void UpdateArrowsVisible(bool left, bool right, bool up, bool down) {
            if (leftArrow) leftArrow.gameObject.SetActive(left);
            if (rightArrow) rightArrow.gameObject.SetActive(right);
            if (upArrow) upArrow.gameObject.SetActive(up);
            if (downArrow) downArrow.gameObject.SetActive(down);
        }

        private void ApplyArrowColor() {
            Color target = useWhiteArrows ? whiteColor : defaultBlueColor;

            SetRgb(leftArrow, target);
            SetRgb(rightArrow, target);
            SetRgb(upArrow, target);
            SetRgb(downArrow, target);
        }

        private void SetAllAlpha(float a) {
            currentAlpha = a;
            SetAlpha(leftArrow, a);
            SetAlpha(rightArrow, a);
            SetAlpha(upArrow, a);
            SetAlpha(downArrow, a);
        }

        private static void SetRgb(Graphic g, Color rgb) {
            if (!g) return;

            Color c = g.color;
            c.r = rgb.r;
            c.g = rgb.g;
            c.b = rgb.b;
            g.color = c;
        }

        private static void SetAlpha(Graphic g, float a) {
            if (!g) return;

            Color c = g.color;
            c.a = a;
            g.color = c;
        }

        private void StopFade() {
            if (fadeRoutine != null) {
                StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            }
        }

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
}