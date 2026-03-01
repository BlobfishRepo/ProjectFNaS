using UnityEngine;
using TMPro;
using FNaS.Gameplay;

namespace FNaS.Systems {
    public class PaperWinProgress : MonoBehaviour {

        [Header("Progress")]
        [Tooltip("Seconds of continuously being on the Paper view to win.")]
        public float secondsToWin = 12f;

        [Header("References")]
        public ViewController viewController;
        public WinState winState;
        public LoseState loseState;

        [Header("Which view counts as Paper?")]
        [Tooltip("Drag the Desk->Views->Paper object (the View component) here.")]
        public View paperView;

        [Tooltip("Optional fallback if you don't want to drag the reference. Matches by GameObject name.")]
        public string paperViewNameFallback = "Paper";

        [Header("UI")]
        public TMP_Text percentText; // can be on HUD, or on the paper canvas, anywhere

        private float progress01;
        private bool onPaper;

        private void OnEnable() {
            if (viewController != null)
                viewController.OnEnteredWaypointOrView += RefreshOnPaperFlag;
        }

        private void OnDisable() {
            if (viewController != null)
                viewController.OnEnteredWaypointOrView -= RefreshOnPaperFlag;
        }

        private void Start() {
            RefreshOnPaperFlag();
            UpdateText();
        }

        private void Update() {
            if (winState != null && winState.hasWon) return;
            if (loseState != null && loseState.hasLost) return;

            if (!onPaper) return;

            float denom = Mathf.Max(0.01f, secondsToWin);
            progress01 = Mathf.Clamp01(progress01 + Time.deltaTime / denom);
            UpdateText();

            if (progress01 >= 1f) {
                winState?.TriggerWin();
            }
        }

        private void RefreshOnPaperFlag() {
            onPaper = IsPaperViewActive();
        }

        private bool IsPaperViewActive() {
            if (viewController == null) return false;
            var cv = viewController.CurrentView;
            if (cv == null) return false;

            // Best: direct reference match
            if (paperView != null) return cv == paperView;

            // Fallback: name match (case-insensitive)
            if (!string.IsNullOrEmpty(paperViewNameFallback)) {
                return string.Equals(cv.gameObject.name, paperViewNameFallback, System.StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private void UpdateText() {
            if (percentText == null) return;
            int pct = Mathf.RoundToInt(progress01 * 100f);
            percentText.text = $"{pct}%";
        }

        public void ResetProgress() {
            progress01 = 0f;
            UpdateText();
            RefreshOnPaperFlag();
        }
    }
}