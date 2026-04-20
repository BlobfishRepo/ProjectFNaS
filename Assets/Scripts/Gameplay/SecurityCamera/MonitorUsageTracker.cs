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

        [Header("Runtime (read-only)")]
        [SerializeField] private bool suppressMonitorAttention;
        [SerializeField] private float suppressTimer;

        private bool lastMonitorInUse;
        private int lastPublishedCamIndex = -2;

        private void Start() {
            PublishAttentionState(force: true);
        }

        private void Update() {
            if (suppressMonitorAttention && suppressTimer > 0f) {
                suppressTimer -= Time.deltaTime;
                if (suppressTimer <= 0f) {
                    suppressTimer = 0f;
                    suppressMonitorAttention = false;

                    if (verboseLogging) {
                        Debug.Log("MonitorUsageTracker: timed suppression ended.", this);
                    }
                }
            }

            PublishAttentionState(force: false);
        }

        public void SetMonitorAttentionSuppressed(bool suppressed) {
            suppressMonitorAttention = suppressed;

            if (!suppressed) {
                suppressTimer = 0f;
            }

            PublishAttentionState(force: true);
        }

        public void SuppressMonitorAttentionForSeconds(float seconds) {
            suppressMonitorAttention = true;
            suppressTimer = Mathf.Max(0f, seconds);
            PublishAttentionState(force: true);

            if (verboseLogging) {
                Debug.Log($"MonitorUsageTracker: suppressing monitor attention for {suppressTimer:F2}s", this);
            }
        }

        private void PublishAttentionState(bool force) {
            if (attentionState == null) return;

            bool usingMonitor = !suppressMonitorAttention && IsMonitorViewActive();
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
                    $"MonitorUsageTracker publish | suppressed={suppressMonitorAttention} " +
                    $"| usingMonitor={attentionState.isMonitorInUse} " +
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