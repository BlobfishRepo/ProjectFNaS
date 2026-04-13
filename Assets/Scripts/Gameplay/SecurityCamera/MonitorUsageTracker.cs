using UnityEngine;
using FNaS.Gameplay;

namespace FNaS.Systems {
    public class MonitorUsageTracker : MonoBehaviour {
        [Header("References")]
        public ViewController viewController;
        public SecurityMonitorController monitorController;
        public GameAttentionState attentionState;

        [Header("Which view counts as Monitor?")]
        public View monitorView;
        public string monitorViewNameFallback = "Monitor";

        [Header("Debug")]
        public bool verboseLogging = false;

        private bool lastMonitorInUse;
        private int lastPublishedCamIndex = -2;

        private void Start() {
            PublishAttentionState(force: true);
        }

        private void Update() {
            PublishAttentionState(force: false);
        }

        private void PublishAttentionState(bool force) {
            if (attentionState == null) return;

            bool usingMonitor = IsMonitorViewActive();
            int camIndex = monitorController != null ? monitorController.ActiveIndex : -1;

            bool changed =
                force ||
                usingMonitor != lastMonitorInUse ||
                camIndex != lastPublishedCamIndex;

            if (!changed) return;

            lastMonitorInUse = usingMonitor;
            lastPublishedCamIndex = camIndex;

            attentionState.isMonitorInUse = usingMonitor;
            attentionState.isCameraActive = usingMonitor && camIndex >= 0;

            if (attentionState.isCameraActive && monitorController != null) {
                attentionState.activeCameraNode = monitorController.GetActiveMasterNode();
                attentionState.activeCameraLookTarget = monitorController.GetActiveLookTarget();
            }
            else {
                attentionState.activeCameraNode = null;
                attentionState.activeCameraLookTarget = null;
            }

            if (verboseLogging) {
                Debug.Log(
                    $"MonitorUsageTracker publish | usingMonitor={attentionState.isMonitorInUse} " +
                    $"| camIndex={camIndex} " +
                    $"| node={(attentionState.activeCameraNode != null ? attentionState.activeCameraNode.name : "null")} " +
                    $"| currentView={(viewController != null && viewController.CurrentView != null ? viewController.CurrentView.gameObject.name : "null")}",
                    this
                );
            }
        }

        private bool IsMonitorViewActive() {
            if (viewController == null) return false;

            View cv = viewController.CurrentView;
            if (cv == null) return false;

            if (monitorView != null) {
                return cv == monitorView;
            }

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