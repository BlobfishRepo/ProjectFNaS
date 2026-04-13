using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace FNaS.Systems {
    public class HoldEscapeToMenu : MonoBehaviour {
        [Header("References")]
        public TMP_Text exitingText;
        public LoseState loseState;
        public WinState winState;

        [Header("Scene Flow")]
        [SerializeField] private string fallbackMenuSceneName = "SceneSettings";

        private PlayerInputActions input;
        private bool holdStarted;

        private void Awake() {
            input = new PlayerInputActions();

            if (loseState == null) {
                loseState = FindFirstObjectByType<LoseState>();
            }

            if (winState == null) {
                winState = FindFirstObjectByType<WinState>();
            }
        }

        private void OnEnable() {
            input.Player.Enable();
            input.Player.ExitToMenu.started += OnExitHoldStarted;
            input.Player.ExitToMenu.canceled += OnExitHoldCanceled;
            input.Player.ExitToMenu.performed += OnExitHoldPerformed;
        }

        private void OnDisable() {
            if (input != null) {
                input.Player.ExitToMenu.started -= OnExitHoldStarted;
                input.Player.ExitToMenu.canceled -= OnExitHoldCanceled;
                input.Player.ExitToMenu.performed -= OnExitHoldPerformed;
                input.Player.Disable();
            }
        }

        private void Start() {
            SetTextVisible(false);
        }

        private void OnExitHoldStarted(InputAction.CallbackContext ctx) {
            if (ShouldBlockManualExit()) return;

            holdStarted = true;
            SetTextVisible(true);
        }

        private void OnExitHoldCanceled(InputAction.CallbackContext ctx) {
            holdStarted = false;
            SetTextVisible(false);
        }

        private void OnExitHoldPerformed(InputAction.CallbackContext ctx) {
            if (ShouldBlockManualExit()) return;
            if (!holdStarted) return;

            Time.timeScale = 1f;

            NightSessionManager session = NightSessionManager.Instance;
            if (session != null) {
                session.ClearSession();
                SceneManager.LoadScene(session.introSceneName);
            }
            else {
                SceneManager.LoadScene(fallbackMenuSceneName);
            }
        }

        private bool ShouldBlockManualExit() {
            if (loseState != null && loseState.hasLost) return true;
            if (winState != null && winState.hasWon) return true;
            return false;
        }

        private void SetTextVisible(bool visible) {
            if (exitingText == null) return;

            exitingText.enabled = visible;
            if (visible) {
                exitingText.text = "Exiting...";
            }
        }
    }
}