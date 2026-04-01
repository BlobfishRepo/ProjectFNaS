using UnityEngine;
using FNaS.Gameplay;
using FNaS.Systems;
using FNaS.MasterNodes;

namespace FNaS.Entities.Stalker {
    public class StalkerLookController : MonoBehaviour {
        [Header("References")]
        [SerializeField] private StalkerEntity stalker;
        [SerializeField] private Transform headBone;

        [Tooltip("Usually the stalker's chest/root/visual root. Used as the yaw basis.")]
        [SerializeField] private Transform bodyForwardReference;

        [Tooltip("Usually the player's camera pivot / head target.")]
        [SerializeField] private Transform playerLookTarget;

        [Tooltip("Optional fallback if attentionState has no active camera look target.")]
        [SerializeField] private Transform fallbackCameraLookTarget;

        [Header("Behavior")]
        [SerializeField] private bool enableLookAtPlayer = true;
        [SerializeField] private bool enableLookAtCamera = true;

        [SerializeField] private float turnSpeed = 6f;
        [SerializeField] private float idleReturnSpeed = 4f;

        [SerializeField] private float maxYawDegrees = 35f;
        [SerializeField] private float maxPitchDegrees = 20f;

        [Header("Camera Preference")]
        [Tooltip("How close the stalker must be to the active camera target to care about it.")]
        [SerializeField] private float maxCameraInterestDistance = 12f;

        [Tooltip("Optional: require the stalker to be somewhat in front of the camera target.")]
        [SerializeField] private bool requireCameraFrontHemisphere = false;

        private Quaternion initialLocalRotation;

        // Rotation offset from bodyForwardReference-space to the head's resting world rotation.
        private Quaternion restOffsetFromBasis = Quaternion.identity;

        private Transform HeadParent => headBone != null ? headBone.parent : null;

        private void Awake() {
            if (stalker == null) stalker = GetComponentInParent<StalkerEntity>();

            if (headBone == null) {
                Debug.LogError("StalkerLookController: headBone is not assigned.", this);
                enabled = false;
                return;
            }

            if (bodyForwardReference == null) {
                bodyForwardReference = transform;
            }

            initialLocalRotation = headBone.localRotation;

            // Store the resting offset between the chosen basis and the current head world rotation.
            restOffsetFromBasis = Quaternion.Inverse(bodyForwardReference.rotation) * headBone.rotation;
        }

        private void LateUpdate() {
            if (stalker == null || headBone == null) return;

            if (!stalker.IsThreatActive) {
                ReturnToIdle();
                return;
            }

            if (stalker.loseState != null && stalker.loseState.hasLost) {
                ReturnToIdle();
                return;
            }

            Transform target = ResolveLookTarget();
            if (target == null) {
                ReturnToIdle();
                return;
            }

            Debug.Log(target != null ? $"Look target: {target.name}" : "Look target: null");
            ApplyLook(target.position);
        }

        private Transform ResolveLookTarget() {
            Transform cameraTarget = ResolveActiveCameraTarget();

            if (enableLookAtCamera && cameraTarget != null && ShouldPreferCamera(cameraTarget)) {
                return cameraTarget;
            }

            if (enableLookAtPlayer && playerLookTarget != null) {
                return playerLookTarget;
            }

            return null;
        }

        private Transform ResolveActiveCameraTarget() {
            if (stalker == null || stalker.attentionState == null) return fallbackCameraLookTarget;

            // New preferred field if you add it to GameAttentionState.
            if (stalker.attentionState.activeCameraLookTarget != null) {
                return stalker.attentionState.activeCameraLookTarget;
            }

            return fallbackCameraLookTarget;
        }

        private bool ShouldPreferCamera(Transform cameraTarget) {
            if (stalker == null || stalker.attentionState == null) return false;

            // Important distinction:
            // "camera system powered" is not the same as "player is actively using the monitor".
            if (!stalker.attentionState.isMonitorInUse) return false;

            Vector3 toCamera = cameraTarget.position - transform.position;
            float dist = toCamera.magnitude;
            if (dist > maxCameraInterestDistance) return false;

            if (requireCameraFrontHemisphere) {
                Vector3 flat = toCamera;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.0001f) {
                    float dot = Vector3.Dot(bodyForwardReference.forward, flat.normalized);
                    if (dot < -0.15f) return false;
                }
            }

            return true;
        }

        private void ApplyLook(Vector3 worldTarget) {
            Transform basis = bodyForwardReference != null ? bodyForwardReference : transform;

            Vector3 toTargetWorld = worldTarget - headBone.position;
            if (toTargetWorld.sqrMagnitude < 0.0001f) {
                ReturnToIdle();
                return;
            }

            // Convert direction into the chosen basis space for clamped yaw/pitch.
            Vector3 toTargetBasis = basis.InverseTransformDirection(toTargetWorld.normalized);

            float yaw = Mathf.Atan2(toTargetBasis.x, toTargetBasis.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(
                toTargetBasis.y,
                new Vector2(toTargetBasis.x, toTargetBasis.z).magnitude
            ) * Mathf.Rad2Deg;

            yaw = Mathf.Clamp(yaw, -maxYawDegrees, maxYawDegrees);
            pitch = Mathf.Clamp(pitch, -maxPitchDegrees, maxPitchDegrees);

            // Build a desired WORLD rotation from basis space, then convert back to head local.
            Quaternion desiredWorld =
                basis.rotation *
                Quaternion.Euler(pitch, yaw, 0f) *
                restOffsetFromBasis;

            Quaternion desiredLocal = HeadParent != null
                ? Quaternion.Inverse(HeadParent.rotation) * desiredWorld
                : desiredWorld;

            headBone.localRotation = desiredLocal;
        }

        private void ReturnToIdle() {
            headBone.localRotation = initialLocalRotation;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected() {
            if (headBone == null) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(headBone.position, 0.03f);
        }
#endif
    }
}