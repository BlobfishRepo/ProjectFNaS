using TMPro;
using UnityEngine;

namespace FNaS.Systems {
    public class GlobalAITimerDebugDisplay : MonoBehaviour {
        [Header("References")]
        public TMP_Text textUI;
        public GlobalAIScheduler scheduler;

        [Header("Debug")]
        public bool showTimer = true;
        public bool showTenths = false;
        public string prefix = "";

        [Header("Runtime (read-only)")]
        [SerializeField] private float secondsRemaining;

        private void Awake() {
            if (textUI == null) {
                textUI = GetComponent<TMP_Text>();
            }

            if (scheduler == null) {
                scheduler = GlobalAIScheduler.Instance;
            }
        }

        private void OnEnable() {
            ApplyVisibility();
            RefreshText(forcePlaceholderWhenMissing: true);
        }

        private void Update() {
            if (scheduler == null) {
                scheduler = GlobalAIScheduler.Instance;
            }

            ApplyVisibility();
            RefreshText(forcePlaceholderWhenMissing: false);
        }

        private void ApplyVisibility() {
            if (textUI != null) {
                textUI.enabled = showTimer;
            }
        }

        private void RefreshText(bool forcePlaceholderWhenMissing) {
            if (textUI == null) return;
            if (!showTimer) return;

            if (scheduler == null) {
                if (forcePlaceholderWhenMissing) {
                    textUI.text = prefix;
                }
                return;
            }

            secondsRemaining = scheduler.SecondsUntilNextTick();

            if (showTenths) {
                textUI.text = string.IsNullOrEmpty(prefix)
                    ? secondsRemaining.ToString("0.0")
                    : $"{prefix}{secondsRemaining:0.0}";
            }
            else {
                int displayValue = Mathf.CeilToInt(secondsRemaining);
                textUI.text = string.IsNullOrEmpty(prefix)
                    ? displayValue.ToString()
                    : $"{prefix}{displayValue}";
            }
        }
    }
}