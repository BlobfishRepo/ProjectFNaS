using UnityEngine;
using FNaS.Gameplay;
using FNaS.Systems;
using FNaS.MasterNodes;
using FNaS.Entities.Stalker;

namespace FNaS.Entities.Stalker {
    public class StalkerLookController : MonoBehaviour {
        [Header("References")]
        [SerializeField] private StalkerEntity stalker;
        [SerializeField] private Transform headBone;
        [SerializeField] private Transform bodyForwardReference;
        [SerializeField] private Transform playerLookTarget;
        [SerializeField] private Transform activeCameraLookTarget;

        [Header("Behavior")]
        [SerializeField] private bool enableLookAtPlayer = true;
        [SerializeField] private bool enableLookAtCamera = true;
        [SerializeField] private float turnSpeed = 6f;
        [SerializeField] private float maxYawDegrees = 35f;
        [SerializeField] private float maxPitchDegrees = 20f;
        [SerializeField] private float idleReturnSpeed = 4f;

        private Quaternion initialLocalRotation;

        private void Awake() {
            if (stalker == null) stalker = GetComponentInParent<StalkerEntity>();

            if (headBone == null) {
                Debug.LogError("StalkerLookController: headBone is not assigned.", this);
                enabled = false;
                return;
            }

            initialLocalRotation = headBone.localRotation;
        }

        private void LateUpdate() {
            if (stalker == null || headBone == null) return;
            if (!stalker.IsThreatActive) {
                ReturnToIdle();
                return;
            }

            Transform target = ResolveLookTarget();
            if (target == null) {
                ReturnToIdle();
                return;
            }

            ApplyLook(target.position);
        }

        private Transform ResolveLookTarget() {
            if (stalker.loseState != null && stalker.loseState.hasLost) {
                return null;
            }

            if (enableLookAtCamera &&
                stalker.attentionState != null &&
                stalker.attentionState.isCameraActive &&
                stalker.attentionState.activeCameraNode != null &&
                stalker.CurrentMasterNode != null &&
                stalker.attentionState.activeCameraNode == stalker.CurrentMasterNode) {
                return activeCameraLookTarget;
            }

            if (enableLookAtPlayer && playerLookTarget != null) {
                return playerLookTarget;
            }

            return null;
        }

        private void ApplyLook(Vector3 worldTarget) {
            Transform basis = bodyForwardReference != null ? bodyForwardReference : transform;

            Vector3 toTargetWorld = worldTarget - headBone.position;
            Vector3 toTargetLocal = basis.InverseTransformDirection(toTargetWorld.normalized);

            float yaw = Mathf.Atan2(toTargetLocal.x, toTargetLocal.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(toTargetLocal.y, new Vector2(toTargetLocal.x, toTargetLocal.z).magnitude) * Mathf.Rad2Deg;

            yaw = Mathf.Clamp(yaw, -maxYawDegrees, maxYawDegrees);
            pitch = Mathf.Clamp(pitch, -maxPitchDegrees, maxPitchDegrees);

            Quaternion targetLocalRotation = initialLocalRotation * Quaternion.Euler(pitch, yaw, 0f);
            float t = 1f - Mathf.Exp(-turnSpeed * Time.deltaTime);
            headBone.localRotation = Quaternion.Slerp(headBone.localRotation, targetLocalRotation, t);
        }

        private void ReturnToIdle() {
            float t = 1f - Mathf.Exp(-idleReturnSpeed * Time.deltaTime);
            headBone.localRotation = Quaternion.Slerp(headBone.localRotation, initialLocalRotation, t);
        }
    }
}