using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace FNaS.Systems {
    public class LoseState : MonoBehaviour {
        [Header("Runtime")]
        public bool hasLost;
        public string reason;

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip jumpscareClip;
        [Range(0f, 1f)] public float jumpscareVolume = 1f;

        [Header("UI")]
        public GameObject losePanel;
        public TMP_Text loseText;

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
            if (losePanel != null) {
                losePanel.SetActive(false);
            }
        }

        public void TriggerLose(string why) {
            if (hasLost) return;

            hasLost = true;
            reason = why;

            Debug.Log($"LOSE TRIGGERED: {why}");

            if (audioSource != null && jumpscareClip != null) {
                audioSource.PlayOneShot(jumpscareClip, jumpscareVolume);
            }

            if (loseText != null) {
                loseText.text = "YOU LOST\n\nR = Restart Night\nF = Main Menu";
            }

            if (losePanel != null) {
                losePanel.SetActive(true);
            }

            Time.timeScale = 0f;
        }

        private void OnRestartPressed(InputAction.CallbackContext ctx) {
            if (!hasLost) return;

            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void OnMenuPressed(InputAction.CallbackContext ctx) {
            if (!hasLost) return;

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

        public void ResetLose() {
            hasLost = false;
            reason = "";

            if (losePanel != null) {
                losePanel.SetActive(false);
            }

            Time.timeScale = 1f;
        }
    }
}