using UnityEngine;

namespace FNaS.Entities.LostGirl {
    public class LostGirlJumpscareLook : MonoBehaviour {
        [Header("References")]
        [SerializeField] private Transform headBone;
        [SerializeField] private Transform target;

        [Header("Settings")]
        [SerializeField] private bool isActive = true;
        [SerializeField] private Vector3 localForwardAxis = Vector3.forward;

        public void SetActive(bool value) {
            isActive = value;
        }

        public void SetTarget(Transform newTarget) {
            target = newTarget;
        }

        private void LateUpdate() {
            if (!isActive || headBone == null || target == null) return;

            Vector3 toTarget = target.position - headBone.position;
            if (toTarget.sqrMagnitude < 0.0001f) return;

            Quaternion desiredWorldRotation =
                Quaternion.LookRotation(toTarget.normalized, Vector3.up) *
                Quaternion.Inverse(Quaternion.LookRotation(localForwardAxis.normalized, Vector3.up));

            headBone.rotation = desiredWorldRotation;
        }
    }
}