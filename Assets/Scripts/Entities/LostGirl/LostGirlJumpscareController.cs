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
        [SerializeField] private Animator animator;

        [Header("Presentation")]
        [SerializeField] private Transform lostGirlRoot;
        [SerializeField] private Transform jumpscareAnchor;
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private Camera jumpscareCamera;
        [SerializeField] private string jumpscareLayerName = "Jumpscare";

        [Header("Settings")]
        [SerializeField] private string triggerName = "JumpscareTrigger";
        [SerializeField] private float jumpscareDelayBeforeLose = 0.5f;

        [Header("Audio")]
        [SerializeField] private AudioClip jumpscareClip;
        [SerializeField][Range(0f, 1f)] private float jumpscareVolume = 1f;

        private bool isPlaying;
        private int originalLayer = -1;
        private int jumpscareLayer = -1;

        public bool IsPlaying => isPlaying;

        private void Awake() {
            jumpscareLayer = LayerMask.NameToLayer(jumpscareLayerName);

            if (visualRoot != null) {
                originalLayer = visualRoot.layer;
            }

            if (jumpscareCamera != null) {
                jumpscareCamera.enabled = false;
            }
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

            if (lostGirlRoot != null && jumpscareAnchor != null) {
                lostGirlRoot.position = jumpscareAnchor.position;
                lostGirlRoot.rotation = jumpscareAnchor.rotation;
            }

            if (visualRoot != null && jumpscareLayer != -1) {
                SetLayerRecursively(visualRoot, jumpscareLayer);
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

            if (visualRoot != null && originalLayer != -1) {
                SetLayerRecursively(visualRoot, originalLayer);
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