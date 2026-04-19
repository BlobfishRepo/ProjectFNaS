using System.Collections;
using UnityEngine;
using FNaS.Systems;
using FNaS.Gameplay;

namespace FNaS.Entities.LostGirl {
    public class LostGirlJumpscareController : MonoBehaviour {
        [Header("References")]
        [SerializeField] private LoseState loseState;
        [SerializeField] private PlayerWaypointController playerMovement;
        [SerializeField] private LostGirlMovement lostGirlMovement;

        [Header("Gameplay Visual")]
        [SerializeField] private GameObject gameplayVisualRoot;

        [Header("Jumpscare Visual")]
        [SerializeField] private GameObject jumpscareVisualRoot;
        [SerializeField] private Transform jumpscareModelRoot;
        [SerializeField] private Animator jumpscareAnimator;
        [SerializeField] private LostGirlJumpscareLook jumpscareLookAt;

        [Header("Presentation")]
        [SerializeField] private Transform jumpscareAnchor;
        [SerializeField] private Camera jumpscareCamera;
        [SerializeField] private string jumpscareLayerName = "Jumpscare";
        [SerializeField] private Transform playerLookTarget;

        [Header("Settings")]
        [SerializeField] private string triggerName = "JumpscareTrigger";
        [SerializeField] private float jumpscareDelayBeforeLose = 0.35f;

        [Header("Audio")]
        [SerializeField] private AudioClip jumpscareClip;
        [SerializeField][Range(0f, 1f)] private float jumpscareVolume = 1f;

        private bool isPlaying;
        private int originalJumpscareLayer = -1;
        private int jumpscareLayer = -1;

        public bool IsPlaying => isPlaying;

        private void Awake() {
            jumpscareLayer = LayerMask.NameToLayer(jumpscareLayerName);

            if (jumpscareVisualRoot != null) {
                originalJumpscareLayer = jumpscareVisualRoot.layer;
                jumpscareVisualRoot.SetActive(false);
            }

            if (jumpscareCamera != null) {
                jumpscareCamera.enabled = false;
            }

            //if (jumpscareLookAt != null) {
            //    jumpscareLookAt.SetActive(false);
            //}
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

            if (playerMovement != null) {
                playerMovement.CancelActiveMovementImmediate();
                playerMovement.enabled = false;
            }

            if (lostGirlMovement != null) {
                lostGirlMovement.StopMovement();
                lostGirlMovement.enabled = false;
            }

            // Hide gameplay model so only the separate jumpscare model is visible.
            if (gameplayVisualRoot != null) {
                gameplayVisualRoot.SetActive(false);
            }

            if (jumpscareVisualRoot != null) {
                jumpscareVisualRoot.SetActive(true);
            }

            if (jumpscareModelRoot != null && jumpscareAnchor != null) {
                jumpscareModelRoot.position = jumpscareAnchor.position;
                jumpscareModelRoot.rotation = jumpscareAnchor.rotation;
            }

            if (jumpscareVisualRoot != null && jumpscareLayer != -1) {
                SetLayerRecursively(jumpscareVisualRoot, jumpscareLayer);
            }

            if (jumpscareCamera != null) {
                jumpscareCamera.enabled = true;
            }

            if (jumpscareLookAt != null) {
                jumpscareLookAt.SetTarget(
                    playerLookTarget != null
                        ? playerLookTarget
                        : (Camera.main != null ? Camera.main.transform : null)
                );
                jumpscareLookAt.SetActive(true);
            }

            if (jumpscareAnimator != null) {
                jumpscareAnimator.Rebind();
                jumpscareAnimator.Update(0f);
                jumpscareAnimator.ResetTrigger(triggerName);
                jumpscareAnimator.SetTrigger(triggerName);
            }

            yield return new WaitForSeconds(jumpscareDelayBeforeLose);

            loseState?.TriggerLose(loseReason);

            // Leave the head where it is if you prefer. If not, turn this back on later.
            // if (jumpscareLookAt != null) {
            //     jumpscareLookAt.SetActive(false);
            // }

            if (jumpscareVisualRoot != null && originalJumpscareLayer != -1) {
                SetLayerRecursively(jumpscareVisualRoot, originalJumpscareLayer);
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