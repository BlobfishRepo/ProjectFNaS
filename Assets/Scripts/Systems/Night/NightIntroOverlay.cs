using System.Collections;
using TMPro;
using UnityEngine;

namespace FNaS.Systems {
    public class NightIntroOverlay : MonoBehaviour {
        [Header("References")]
        public CanvasGroup overlayGroup;
        public TMP_Text titleText;
        public GameplayPrewarmLoader prewarmLoader;

        [Header("Timing")]
        public float holdAfterPrewarmSeconds = 1f;
        public float fadeOutSeconds = 0.8f;
        public float maxWaitForPrewarmSeconds = 10f;

        private void Awake() {
            ShowImmediate();
        }

        private IEnumerator Start() {
            if (prewarmLoader == null) {
                prewarmLoader = FindFirstObjectByType<GameplayPrewarmLoader>();
            }

            float waited = 0f;
            while (prewarmLoader != null && !prewarmLoader.IsFinished && waited < maxWaitForPrewarmSeconds) {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (holdAfterPrewarmSeconds > 0f) {
                yield return new WaitForSecondsRealtime(holdAfterPrewarmSeconds);
            }

            if (overlayGroup != null) {
                float t = 0f;
                while (t < fadeOutSeconds) {
                    t += Time.unscaledDeltaTime;
                    overlayGroup.alpha = Mathf.Lerp(1f, 0f, t / Mathf.Max(0.001f, fadeOutSeconds));
                    yield return null;
                }

                overlayGroup.alpha = 0f;
                overlayGroup.blocksRaycasts = false;
                overlayGroup.interactable = false;
            }

            if (titleText != null) {
                titleText.enabled = false;
            }
        }

        public void ShowImmediate() {
            if (titleText != null) {
                NightSessionManager session = NightSessionManager.Instance;
                titleText.text = session != null ? session.GetIntroText() : "Night 1\n12:00 AM";
                titleText.enabled = true;
            }

            if (overlayGroup != null) {
                overlayGroup.alpha = 1f;
                overlayGroup.blocksRaycasts = true;
                overlayGroup.interactable = false;
            }
        }
    }
}