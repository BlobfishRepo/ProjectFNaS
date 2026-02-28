using UnityEngine;

namespace FNaS.Systems {
    public class LoseState : MonoBehaviour {
        [Header("Runtime")]
        public bool hasLost;
        public string reason;

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip jumpscareClip;
        [Range(0f, 1f)] public float jumpscareVolume = 1f;

        public void TriggerLose(string why) {
            if (hasLost) return;
            hasLost = true;
            reason = why;
            Debug.Log($"LOSE TRIGGERED: {why}");

            if (audioSource != null && jumpscareClip != null)
                audioSource.PlayOneShot(jumpscareClip, jumpscareVolume);
        }

        public void ResetLose() {
            hasLost = false;
            reason = "";
        }
    }
}