using System.Collections;
using UnityEngine;
using FNaS.Systems;
using FNaS.Gameplay;

namespace FNaS.Entities.Stalker {
    public class StalkerJumpscareController : MonoBehaviour {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private LoseState loseState;
        [SerializeField] private StalkerLookController lookController;
        [SerializeField] private PlayerWaypointController playerMovement;
        [SerializeField] private Transform stalkerRoot;
        [SerializeField] private Transform jumpscareAnchor;

        [Header("Render Front")]
        [SerializeField] private GameObject stalkerVisualRoot;
        [SerializeField] private Camera jumpscareCamera;
        [SerializeField] private string jumpscareLayerName = "Jumpscare";

        [Header("Settings")]
        [SerializeField] private string triggerName = "JumpscareTrigger";
        [SerializeField] private float jumpscareDelayBeforeLose = 0.5f;
        [SerializeField] private bool disableLookDuringJumpscare = true;

        [Header("Audio")]
        [SerializeField] private AudioClip jumpscareClip;
        [SerializeField][Range(0f, 1f)] private float jumpscareVolume = 1f;

        private bool isPlaying;
        private int jumpscareLayer = -1;
        private int originalLayer = -1;

        public bool IsPlaying => isPlaying;

        private void Awake() {
            jumpscareLayer = LayerMask.NameToLayer(jumpscareLayerName);

            if (stalkerVisualRoot != null)
                originalLayer = stalkerVisualRoot.layer;

            if (jumpscareCamera != null)
                jumpscareCamera.enabled = false;
        }

        public void PlayJumpscare(string loseReason) {
            if (isPlaying) return;
            StartCoroutine(PlayRoutine(loseReason));
        }

        private IEnumerator PlayRoutine(string loseReason) {
            isPlaying = true;

            if (jumpscareClip != null && Camera.main != null) {
                AudioSource.PlayClipAtPoint(jumpscareClip, Camera.main.transform.position, jumpscareVolume);
            }

            if (disableLookDuringJumpscare && lookController != null) {
                lookController.enabled = false;
            }

            if (playerMovement != null) {
                playerMovement.CancelActiveMovementImmediate();
                playerMovement.enabled = false;
            }

            if (stalkerRoot != null && jumpscareAnchor != null) {
                stalkerRoot.position = jumpscareAnchor.position;
                stalkerRoot.rotation = jumpscareAnchor.rotation;
            }

            if (stalkerVisualRoot != null && jumpscareLayer != -1) {
                SetLayerRecursively(stalkerVisualRoot, jumpscareLayer);
            }

            if (jumpscareCamera != null) {
                jumpscareCamera.enabled = true;
            }

            if (animator != null) {
                animator.Rebind();
                animator.Update(0f);
                animator.ResetTrigger(triggerName);
                animator.SetTrigger(triggerName);
            }

            yield return new WaitForSeconds(jumpscareDelayBeforeLose);

            loseState?.TriggerLose(loseReason);

            if (stalkerVisualRoot != null && originalLayer != -1) {
                SetLayerRecursively(stalkerVisualRoot, originalLayer);
            }

            if (jumpscareCamera != null) {
                jumpscareCamera.enabled = false;
            }

            isPlaying = false;
        }

        private void SetLayerRecursively(GameObject obj, int layer) {
            if (obj == null) return;

            obj.layer = layer;
            foreach (Transform child in obj.transform) {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }
}