using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using FNaS.Gameplay;
using FNaS.Settings;

namespace FNaS.Systems {
    public class PaperWinProgress : MonoBehaviour, IRuntimeSettingsConsumer {
        private enum WriteState {
            Idle,
            PickupDelay,
            Writing
        }

        [Header("Progress")]
        [Tooltip("Seconds of actual writing time needed to win.")]
        public float secondsToWin = 12f;

        [Tooltip("Delay after pressing Interact before writing actually starts.")]
        public float pickupDelaySeconds = 0.4f;

        [Header("References")]
        public ViewController viewController;
        public PlayerWaypointController waypointMover;
        public WinState winState;
        public LoseState loseState;
        public SecurityMonitorController securityMonitorController;

        [Header("Allowed Views")]
        public View paperView;
        public string paperViewNameFallback = "Paper";

        public View monitorView;
        public string monitorViewNameFallback = "Monitor";

        [Header("UI")]
        public TMP_Text percentText;

        [Header("Debug")]
        public bool verboseLogging = false;

        [Header("Runtime (read-only)")]
        [SerializeField] private float progress01;
        [SerializeField] private WriteState writeState = WriteState.Idle;
        [SerializeField] private float pickupTimer;
        [SerializeField] private bool isOnAllowedWriteView;

        private PlayerInputActions input;

        private void Awake() {
            input = new PlayerInputActions();

            if (viewController == null) {
                viewController = GetComponentInParent<ViewController>();
            }

            if (waypointMover == null) {
                waypointMover = GetComponentInParent<PlayerWaypointController>();
            }

            if (winState == null) {
                winState = FindFirstObjectByType<WinState>();
            }

            if (loseState == null) {
                loseState = FindFirstObjectByType<LoseState>();
            }
        }

        private void OnEnable() {
            input.Player.Enable();
            input.Player.Interact.started += OnInteractPressed;

            if (viewController != null) {
                viewController.OnEnteredWaypointOrView += HandleViewChanged;
            }

            if (securityMonitorController != null) {
                securityMonitorController.OnCameraSwitched += HandleMonitorCameraSwitched;
            }
        }

        private void OnDisable() {
            input.Player.Interact.started -= OnInteractPressed;
            input.Player.Disable();

            if (viewController != null) {
                viewController.OnEnteredWaypointOrView -= HandleViewChanged;
            }

            if (securityMonitorController != null) {
                securityMonitorController.OnCameraSwitched -= HandleMonitorCameraSwitched;
            }
        }

        private void Start() {
            RefreshAllowedWriteViewFlag();
            UpdateText();
        }

        public void ApplyRuntimeSettings(RuntimeGameSettings settings) {
            if (settings == null) return;
            secondsToWin = Mathf.Max(0.01f, settings.GetFloat("paper.secondsToWin"));
        }

        private void Update() {
            if (ShouldBlockInput()) {
                StopWriting(resetPickupDelay: true);
                return;
            }

            RefreshAllowedWriteViewFlag();

            if (writeState != WriteState.Idle && IsPlayerMoving()) {
                if (verboseLogging) {
                    Debug.Log("Paper writing stopped because waypoint movement started.", this);
                }

                StopWriting(resetPickupDelay: true);
                return;
            }

            switch (writeState) {
                case WriteState.Idle:
                    break;

                case WriteState.PickupDelay:
                    UpdatePickupDelay();
                    break;

                case WriteState.Writing:
                    UpdateWriting();
                    break;
            }
        }

        private void OnInteractPressed(InputAction.CallbackContext ctx) {
            if (ShouldBlockInput()) return;
            if (writeState != WriteState.Idle) return;
            if (!IsCurrentlyOnAllowedWriteView()) return;
            if (IsPlayerMoving()) return;

            pickupTimer = 0f;
            writeState = WriteState.PickupDelay;

            if (verboseLogging) {
                Debug.Log("Paper writing pickup started.", this);
            }
        }

        private void UpdatePickupDelay() {
            if (!CanContinueWriting()) {
                StopWriting(resetPickupDelay: true);
                return;
            }

            pickupTimer += Time.unscaledDeltaTime;

            if (pickupTimer >= Mathf.Max(0.01f, pickupDelaySeconds)) {
                writeState = WriteState.Writing;

                if (verboseLogging) {
                    Debug.Log("Paper writing began.", this);
                }
            }
        }

        private void UpdateWriting() {
            if (!CanContinueWriting()) {
                StopWriting(resetPickupDelay: true);
                return;
            }

            float denom = Mathf.Max(0.01f, secondsToWin);
            progress01 = Mathf.Clamp01(progress01 + Time.unscaledDeltaTime / denom);
            UpdateText();

            if (progress01 >= 1f) {
                winState?.TriggerWin();

                if (verboseLogging) {
                    Debug.Log("Paper writing completed. Triggering win.", this);
                }
            }
        }

        private void HandleViewChanged() {
            RefreshAllowedWriteViewFlag();

            if (writeState == WriteState.Idle) return;

            if (!CanContinueWriting()) {
                StopWriting(resetPickupDelay: true);
            }
        }

        private void HandleMonitorCameraSwitched(int newIndex) {
            if (writeState == WriteState.Idle) return;

            if (verboseLogging) {
                Debug.Log($"Paper writing stopped due to monitor camera switch to index {newIndex}.", this);
            }

            StopWriting(resetPickupDelay: true);
        }

        private void RefreshAllowedWriteViewFlag() {
            isOnAllowedWriteView = IsCurrentlyOnAllowedWriteView();
        }

        private bool IsCurrentlyOnAllowedWriteView() {
            View currentView = GetCurrentView();
            if (currentView == null) return false;

            return IsPaperView(currentView) || IsMonitorView(currentView);
        }

        private View GetCurrentView() {
            return viewController != null ? viewController.CurrentView : null;
        }

        private bool IsPaperView(View view) {
            if (view == null) return false;

            if (paperView != null) {
                return view == paperView;
            }

            if (!string.IsNullOrEmpty(paperViewNameFallback)) {
                return string.Equals(
                    view.gameObject.name,
                    paperViewNameFallback,
                    System.StringComparison.OrdinalIgnoreCase
                );
            }

            return false;
        }

        private bool IsMonitorView(View view) {
            if (view == null) return false;

            if (monitorView != null) {
                return view == monitorView;
            }

            if (!string.IsNullOrEmpty(monitorViewNameFallback)) {
                return string.Equals(
                    view.gameObject.name,
                    monitorViewNameFallback,
                    System.StringComparison.OrdinalIgnoreCase
                );
            }

            return false;
        }

        private bool CanContinueWriting() {
            if (ShouldBlockInput()) return false;
            if (!IsCurrentlyOnAllowedWriteView()) return false;
            if (IsPlayerMoving()) return false;
            return true;
        }

        private bool IsPlayerMoving() {
            return waypointMover != null && waypointMover.IsMoving;
        }

        private bool ShouldBlockInput() {
            if (loseState != null && loseState.hasLost) return true;
            if (winState != null && winState.hasWon) return true;
            return false;
        }

        private void StopWriting(bool resetPickupDelay) {
            if (writeState != WriteState.Idle && verboseLogging) {
                Debug.Log($"Paper writing stopped from state: {writeState}", this);
            }

            writeState = WriteState.Idle;

            if (resetPickupDelay) {
                pickupTimer = 0f;
            }
        }

        private void UpdateText() {
            if (percentText == null) return;

            int pct = Mathf.RoundToInt(progress01 * 100f);
            percentText.text = $"{pct}%";
        }

        public void ResetProgress() {
            progress01 = 0f;
            UpdateText();
            StopWriting(resetPickupDelay: true);
            RefreshAllowedWriteViewFlag();
        }

        public float GetProgress01() {
            return progress01;
        }

        public bool IsWritingActive() {
            return writeState == WriteState.Writing;
        }

        public bool IsInPickupDelay() {
            return writeState == WriteState.PickupDelay;
        }
    }
}