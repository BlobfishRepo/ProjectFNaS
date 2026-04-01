using UnityEngine;
using FNaS.Gameplay;
using FNaS.Systems;

namespace FNaS.Systems {
    public class MonitorUsageTracker : MonoBehaviour {

        [Header("References")]
        public ViewController viewController;
        public SecurityMonitorController monitorController;
        public GameAttentionState attentionState;

        [Header("Which view counts as Monitor?")]
        public View monitorView;
        public string monitorViewNameFallback = "Monitor";

        private bool isUsingMonitor;

        private void OnEnable() {
            if (viewController != null)
                viewController.OnEnteredWaypointOrView += RefreshMonitorState;
        }

        private void OnDisable() {
            if (viewController != null)
                viewController.OnEnteredWaypointOrView -= RefreshMonitorState;
        }

        private void Start() {
            RefreshMonitorState();
        }

        private void RefreshMonitorState() {
            bool nowUsing = IsMonitorViewActive();

            if (nowUsing == isUsingMonitor) return;

            isUsingMonitor = nowUsing;

            if (isUsingMonitor) {
                monitorController?.BeginMonitorUse();
            }
            else {
                monitorController?.EndMonitorUse();
            }
        }

        private bool IsMonitorViewActive() {
            if (viewController == null) return false;
            var cv = viewController.CurrentView;
            if (cv == null) return false;

            if (monitorView != null)
                return cv == monitorView;

            if (!string.IsNullOrEmpty(monitorViewNameFallback)) {
                return string.Equals(
                    cv.gameObject.name,
                    monitorViewNameFallback,
                    System.StringComparison.OrdinalIgnoreCase
                );
            }

            return false;
        }
    }
}