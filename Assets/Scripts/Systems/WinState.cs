using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace FNaS.Systems {
    public class WinState : MonoBehaviour {
        [Header("Runtime")]
        public bool hasWon;

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip winClip;
        [Range(0f, 1f)] public float winVolume = 1f;

        [Header("UI")]
        public GameObject winPanel;
        public TMP_Text winText;

        [Header("References")]
        public LoseState loseState;

        [Header("Scene Flow")]
        [SerializeField] private string fallbackMenuSceneName = "SceneSettings";

        private PlayerInputActions input;

        private void Awake() {
            input = new PlayerInputActions();
        }

        private void OnEnable() {
            input.Player.Enable();
            input.Player.Interact.performed += OnRestartPressed;
            input.Player.Flashlight.performed += OnMenuPressed;
        }

        private void OnDisable() {
            if (input != null) {
                input.Player.Interact.performed -= OnRestartPressed;
                input.Player.Flashlight.performed -= OnMenuPressed;
                input.Player.Disable();
            }
        }

        private void Start() {
            if (winPanel != null) {
                winPanel.SetActive(false);
            }
        }

        private bool IsCampaignMode() {
            NightSessionManager session = NightSessionManager.Instance;
            return session != null && session.PlayMode == NightPlayMode.Campaign;
        }

        public void TriggerWin() {
            if (hasWon) return;
            if (loseState != null && loseState.hasLost) return;

            hasWon = true;

            if (audioSource != null && winClip != null) {
                audioSource.PlayOneShot(winClip, winVolume);
            }

            if (IsCampaignMode()) {
                if (winPanel != null) {
                    winPanel.SetActive(false);
                }
                return;
            }

            if (winText != null) {
                winText.text = "YOU WIN\n\nR = Restart Night\nF = Main Menu";
            }

            if (winPanel != null) {
                winPanel.SetActive(true);
            }

            Time.timeScale = 0f;
        }

        private void OnRestartPressed(InputAction.CallbackContext ctx) {
            if (!hasWon) return;
            if (IsCampaignMode()) return;

            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void OnMenuPressed(InputAction.CallbackContext ctx) {
            if (!hasWon) return;
            if (IsCampaignMode()) return;

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
    }
}