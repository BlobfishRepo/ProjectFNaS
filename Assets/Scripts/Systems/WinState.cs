using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;

namespace FNaS.Systems {
    public class WinState : MonoBehaviour {

        [Header("Runtime")]
        public bool hasWon;

        [Header("Audio (optional)")]
        public AudioSource audioSource;
        public AudioClip winClip;
        [Range(0f, 1f)] public float winVolume = 1f;

        [Header("UI")]
        public GameObject winPanel;
        public TMP_Text winText;

        [Header("Optional: block win if already lost")]
        public LoseState loseState;

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
            if (winPanel != null) winPanel.SetActive(false);
        }

        public void TriggerWin() {
            if (hasWon) return;
            if (loseState != null && loseState.hasLost) return;

            hasWon = true;

            if (audioSource != null && winClip != null)
                audioSource.PlayOneShot(winClip, winVolume);

            if (winText != null)
                winText.text = "YOU WIN\n\nPress Y to Restart";

            if (winPanel != null)
                winPanel.SetActive(true);

            Time.timeScale = 0f;
        }

        private void OnResetPressed(InputAction.CallbackContext ctx) {
            // allow reset from either win or lose
            if (!hasWon && (loseState == null || !loseState.hasLost)) return;

            Time.timeScale = 1f;
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex);
        }
    }
}