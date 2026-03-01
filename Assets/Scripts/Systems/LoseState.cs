using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;

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

        private PlayerInputActions input;

        private void Awake() {
            input = new PlayerInputActions();
        }

        private void OnEnable() {
            input.Player.Enable();
            input.Player.Reset.performed += OnResetPressed;
        }

        private void OnDisable() {
            input.Player.Reset.performed -= OnResetPressed;
            input.Player.Disable();
        }

        private void Start() {
            if (losePanel != null)
                losePanel.SetActive(false);
        }

        public void TriggerLose(string why) {
            if (hasLost) return;

            hasLost = true;
            reason = why;

            Debug.Log($"LOSE TRIGGERED: {why}");

            if (audioSource != null && jumpscareClip != null)
                audioSource.PlayOneShot(jumpscareClip, jumpscareVolume);

            if (loseText != null)
                loseText.text = "YOU LOST\n\nPress Y to Restart";

            if (losePanel != null)
                losePanel.SetActive(true);

            Time.timeScale = 0f;
        }

        private void OnResetPressed(InputAction.CallbackContext ctx) {
            if (!hasLost) return;

            Time.timeScale = 1f;
            Scene current = SceneManager.GetActiveScene();
            SceneManager.LoadScene(current.buildIndex);
        }

        public void ResetLose() {
            hasLost = false;
            reason = "";

            if (losePanel != null)
                losePanel.SetActive(false);

            Time.timeScale = 1f;
        }
    }
}