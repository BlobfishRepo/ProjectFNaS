using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using FNaS.Entities.Mold;
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
        public FlashlightTool flashlightTool;
        public MoldSprayTool moldSprayTool;

        [Header("Allowed Views")]
        public View paperView;
        public string paperViewNameFallback = "Paper";
        public View monitorView;
        public string monitorViewNameFallback = "Monitor";

        [Header("UI")]
        public TMP_Text percentText;

        [Header("Writing Behavior")]
        [Tooltip("If true, switching monitor cameras stops writing.")]
        public bool cancelOnMonitorCameraSwitch = true;

        [Tooltip("If true, writing slows down when not directly on the Paper view.")]
        public bool slowWhenNotDirectlyViewingPaper = false;

        [Range(0.1f, 1f)]
        public float offPaperWriteSpeedMultiplier = 0.7f;

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
            viewController ??= GetComponentInParent<ViewController>();
            waypointMover ??= GetComponentInParent<PlayerWaypointController>();
            winState ??= FindFirstObjectByType<WinState>();
            loseState ??= FindFirstObjectByType<LoseState>();
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
            UpdatePercentText();
        }

        public void ApplyRuntimeSettings(RuntimeGameSettings settings) {
            if (settings == null) return;
            secondsToWin = Mathf.Max(0.01f, settings.GetFloat("paper.secondsToWin"));
        }

        private void Update() {
            RefreshAllowedWriteViewFlag();

            if (writeState == WriteState.Idle) {
                return;
            }

            if (!CanContinueWriting()) {
                Log("Paper writing stopped because conditions are no longer valid.");
                StopWriting(resetPickupDelay: true);
                return;
            }

            switch (writeState) {
                case WriteState.PickupDelay:
                    pickupTimer += Time.unscaledDeltaTime;
                    if (pickupTimer >= Mathf.Max(0.01f, pickupDelaySeconds)) {
                        writeState = WriteState.Writing;
                        Log("Paper writing began.");
                    }
                    break;

                case WriteState.Writing:
                    progress01 = Mathf.Clamp01(
                        progress01 + (Time.unscaledDeltaTime * GetCurrentWritingSpeedMultiplier()) / Mathf.Max(0.01f, secondsToWin)
                    );
                    UpdatePercentText();

                    if (progress01 >= 1f) {
                        winState?.TriggerWin();
                        Log("Paper writing completed. Triggering win.");
                    }
                    break;
            }
        }

        private void OnInteractPressed(InputAction.CallbackContext _) {
            if (writeState != WriteState.Idle) return;
            if (!CanStartWriting()) return;

            pickupTimer = 0f;
            writeState = WriteState.PickupDelay;
            Log("Paper writing pickup started.");
        }

        private void HandleViewChanged() {
            RefreshAllowedWriteViewFlag();

            if (writeState != WriteState.Idle && !CanContinueWriting()) {
                StopWriting(resetPickupDelay: true);
            }
        }

        private void HandleMonitorCameraSwitched(int newIndex) {
            if (writeState == WriteState.Idle || !cancelOnMonitorCameraSwitch) return;

            Log($"Paper writing stopped due to monitor camera switch to index {newIndex}.");
            StopWriting(resetPickupDelay: true);
        }

        private bool CanStartWriting() {
            return !ShouldBlockInput() &&
                   IsCurrentlyOnAllowedWriteView() &&
                   !IsPlayerMoving() &&
                   !AreToolsInterrupting();
        }

        private bool CanContinueWriting() {
            return !ShouldBlockInput() &&
                   IsCurrentlyOnAllowedWriteView() &&
                   !IsPlayerMoving() &&
                   !AreToolsInterrupting();
        }

        private void RefreshAllowedWriteViewFlag() {
            isOnAllowedWriteView = IsCurrentlyOnAllowedWriteView();
        }

        private bool IsCurrentlyOnAllowedWriteView() {
            View currentView = GetCurrentView();
            return IsPaperView(currentView) || IsMonitorView(currentView);
        }

        private View GetCurrentView() => viewController != null ? viewController.CurrentView : null;

        private bool IsPaperView(View view) {
            if (view == null) return false;
            if (paperView != null) return view == paperView;

            return !string.IsNullOrEmpty(paperViewNameFallback) &&
                   string.Equals(view.gameObject.name, paperViewNameFallback, System.StringComparison.OrdinalIgnoreCase);
        }

        private bool IsMonitorView(View view) {
            if (view == null) return false;
            if (monitorView != null) return view == monitorView;

            return !string.IsNullOrEmpty(monitorViewNameFallback) &&
                   string.Equals(view.gameObject.name, monitorViewNameFallback, System.StringComparison.OrdinalIgnoreCase);
        }

        private bool IsPlayerMoving() => waypointMover != null && waypointMover.IsMoving;

        private bool ShouldBlockInput() {
            return (loseState != null && loseState.hasLost) ||
                   (winState != null && winState.hasWon);
        }

        private bool AreToolsInterrupting() {
            return (flashlightTool != null && flashlightTool.isOn) ||
                   (moldSprayTool != null && moldSprayTool.IsSpraying());
        }

        private void StopWriting(bool resetPickupDelay) {
            if (writeState != WriteState.Idle) {
                Log($"Paper writing stopped from state: {writeState}");
            }

            writeState = WriteState.Idle;
            if (resetPickupDelay) pickupTimer = 0f;
        }

        private void UpdatePercentText() {
            if (percentText == null) return;
            percentText.text = $"{Mathf.RoundToInt(progress01 * 100f)}%";
        }

        private void Log(string message) {
            if (verboseLogging) {
                Debug.Log(message, this);
            }
        }

        public void ResetProgress() {
            progress01 = 0f;
            StopWriting(resetPickupDelay: true);
            RefreshAllowedWriteViewFlag();
            UpdatePercentText();
        }

        public float GetProgress01() => progress01;
        public bool IsWritingActive() => writeState == WriteState.Writing;
        public bool IsInPickupDelay() => writeState == WriteState.PickupDelay;
        public float GetPickupDelayDuration() => Mathf.Max(0f, pickupDelaySeconds);
        public float GetPickupDelayRemaining() => Mathf.Max(0f, pickupDelaySeconds - pickupTimer);

        public float GetCurrentWritingSpeedMultiplier() {
            if (!slowWhenNotDirectlyViewingPaper) return 1f;
            return IsPaperView(GetCurrentView()) ? 1f : Mathf.Clamp(offPaperWriteSpeedMultiplier, 0.1f, 1f);
        }
    }
}