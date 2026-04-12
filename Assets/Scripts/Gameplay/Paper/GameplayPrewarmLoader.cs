using System.Collections;
using UnityEngine;

namespace FNaS.Systems {
    public class GameplayPrewarmLoader : MonoBehaviour {
        [Header("References")]
        public CanvasGroup blackFadeGroup;
        public PaperWritingStrokeDisplay paper;
        public StrokeGlyphLibrary glyphLibrary;

        [Header("Optional")]
        public MonoBehaviour[] disableDuringPrewarm;

        [Header("Timing")]
        public int framesBeforePreload = 1;
        public int framesAfterPreload = 1;
        public float fadeOutDuration = 0.15f;

        [Header("Debug")]
        public bool verboseLogging = false;

        private IEnumerator Start() {
            SetBlackImmediate();
            SetBehavioursEnabled(false);

            for (int i = 0; i < Mathf.Max(0, framesBeforePreload); i++) {
                yield return null;
            }

            if (verboseLogging) {
                Debug.Log("GameplayPrewarmLoader: preload begin", this);
            }

            if (glyphLibrary != null) {
                glyphLibrary.Preload();
            }

            yield return null;

            if (paper != null) {
                paper.Rebuild();
            }

            for (int i = 0; i < Mathf.Max(0, framesAfterPreload); i++) {
                yield return null;
            }

            if (verboseLogging) {
                Debug.Log("GameplayPrewarmLoader: preload end", this);
            }

            SetBehavioursEnabled(true);

            if (blackFadeGroup != null) {
                yield return FadeCanvasGroup(blackFadeGroup, 1f, 0f, fadeOutDuration);
            }
        }

        private void SetBlackImmediate() {
            if (blackFadeGroup != null) {
                blackFadeGroup.alpha = 1f;
                blackFadeGroup.blocksRaycasts = true;
                blackFadeGroup.interactable = false;
            }
        }

        private void SetBehavioursEnabled(bool enabled) {
            if (disableDuringPrewarm == null) return;

            for (int i = 0; i < disableDuringPrewarm.Length; i++) {
                if (disableDuringPrewarm[i] != null) {
                    disableDuringPrewarm[i].enabled = enabled;
                }
            }
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration) {
            if (group == null) yield break;

            group.alpha = from;

            if (duration <= 0f) {
                group.alpha = to;
                group.blocksRaycasts = to > 0.001f;
                yield break;
            }

            float t = 0f;
            while (t < duration) {
                t += Time.deltaTime;
                group.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }

            group.alpha = to;
            group.blocksRaycasts = to > 0.001f;
        }
    }
}