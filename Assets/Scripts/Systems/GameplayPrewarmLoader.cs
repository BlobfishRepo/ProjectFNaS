using System.Collections;
using UnityEngine;

namespace FNaS.Systems {
    public class GameplayPrewarmLoader : MonoBehaviour {
        [Header("References")]
        public CanvasGroup blackFadeGroup;
        public PaperWritingStrokeDisplay paper;
        public StrokeGlyphLibrary glyphLibrary;

        [Header("Pause")]
        [Tooltip("If true, prewarm uses GameplayPauseManager's exact pause target list.")]
        public bool pauseGameplayDuringPrewarm = true;

        [Header("Timing")]
        public int framesBeforePreload = 1;
        public int framesAfterPreload = 1;

        [Header("Debug")]
        public bool verboseLogging = false;

        public bool IsFinished { get; private set; }

        private IEnumerator Start() {
            IsFinished = false;

            SetBlackImmediate();

            yield return StartCoroutine(PrewarmRoutine());

            IsFinished = true;
        }

        private IEnumerator PrewarmRoutine() {
            if (verboseLogging) {
                Debug.Log("GameplayPrewarmLoader: preload start", this);
            }

            for (int i = 0; i < Mathf.Max(0, framesBeforePreload); i++) {
                yield return null;
            }

            if (glyphLibrary != null) {
                glyphLibrary.Preload();
            }
            else if (verboseLogging) {
                Debug.LogWarning("GameplayPrewarmLoader: glyphLibrary is null.", this);
            }

            yield return null;

            if (paper != null) {
                paper.Rebuild();
            }
            else if (verboseLogging) {
                Debug.LogWarning("GameplayPrewarmLoader: paper is null.", this);
            }

            for (int i = 0; i < Mathf.Max(0, framesAfterPreload); i++) {
                yield return null;
            }

            if (verboseLogging) {
                Debug.Log("GameplayPrewarmLoader: preload end", this);
            }
        }

        private void SetBlackImmediate() {
            if (blackFadeGroup != null) {
                blackFadeGroup.alpha = 1f;
                blackFadeGroup.blocksRaycasts = true;
                blackFadeGroup.interactable = false;
            }
        }
    }
}