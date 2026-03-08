using UnityEngine;
using FNaS.MasterNodes;

namespace FNaS.Gameplay {
    public class PlayerRoamMovement : PlayerMovementBase {
        [Header("References")]
        public Transform rigTransform;
        public Transform cameraPivot;

        [Header("Movement")]
        public float moveSpeed = 4.5f;

        [Header("Look")]
        public float mouseSensitivity = 0.12f;
        public float minPitch = -80f;
        public float maxPitch = 80f;
        public bool lockCursorOnStart = true;

        [Header("Optional")]
        [SerializeField] private MasterNode currentMasterNode;

        private PlayerInputController inputController;
        private float yaw;
        private float pitch;

        public override MasterNode CurrentMasterNode => currentMasterNode;
        public override bool IsMoving => false;

        public override Transform RigTransform => rigTransform;
        public override Transform ViewTransform => cameraPivot != null ? cameraPivot : transform;

        public override void Initialize(PlayerEntity player, PlayerInputController input) {
            inputController = input;

            if (rigTransform == null) rigTransform = transform;
            if (cameraPivot == null) cameraPivot = transform;

            Vector3 euler = cameraPivot.localEulerAngles;
            yaw = NormalizeAngle(euler.y);
            pitch = NormalizeAngle(euler.x);

            if (lockCursorOnStart && inputController != null) {
                inputController.SetCursorLocked(true);
            }
        }

        private void Update() {
            if (inputController == null || rigTransform == null || cameraPivot == null) return;

            HandleLook();
            HandleMove();
        }

        private void HandleLook() {
            Vector2 mouseDelta = inputController.LookDelta;

            yaw += mouseDelta.x * mouseSensitivity;
            pitch -= mouseDelta.y * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            cameraPivot.localRotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void HandleMove() {
            Vector2 move = inputController.Move;

            Vector3 forward = cameraPivot.forward;
            Vector3 right = cameraPivot.right;

            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 wishDir = forward * move.y + right * move.x;
            if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();

            rigTransform.position += wishDir * moveSpeed * Time.deltaTime;
        }

        private static float NormalizeAngle(float angle) {
            while (angle > 180f) angle -= 360f;
            while (angle < -180f) angle += 360f;
            return angle;
        }
    }
}