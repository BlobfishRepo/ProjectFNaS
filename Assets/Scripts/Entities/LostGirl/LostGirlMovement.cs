using UnityEngine;
using FNaS.Settings;
using FNaS.Systems;

namespace FNaS.Entities.LostGirl {
    public class LostGirlMovement : MonoBehaviour, IRuntimeSettingsConsumer {
        public enum ActiveMoveMode {
            ChaseCurrentPlayer,
            ChargeSnapshot
        }

        [Header("Movement")]
        public ActiveMoveMode moveMode = ActiveMoveMode.ChaseCurrentPlayer;
        public float moveSpeed = 8f;
        public float rotateSpeedDegreesPerSecond = 720f;

        [Header("Runtime (read-only)")]
        [SerializeField] private bool isActive;
        [SerializeField] private Vector3 snapshotTarget;

        private Transform playerTarget;

        public bool IsActive => isActive;
        public Vector3 SnapshotTarget => snapshotTarget;

        public void ApplyRuntimeSettings(RuntimeGameSettings settings) {
            if (settings == null) return;
            moveSpeed = settings.GetFloat("lostGirl.moveSpeed");
        }

        public void BeginMovement(Transform player, ActiveMoveMode mode) {
            playerTarget = player;
            moveMode = mode;
            isActive = true;

            if (playerTarget != null) {
                snapshotTarget = playerTarget.position;
            }
        }

        public void StopMovement() {
            isActive = false;
            playerTarget = null;
        }

        private void Update() {
            if (!isActive) return;

            Vector3? target = GetCurrentTarget();
            if (!target.HasValue) return;

            Vector3 current = transform.position;
            Vector3 desired = target.Value;
            desired.y = current.y;

            Vector3 delta = desired - current;
            float dist = delta.magnitude;
            if (dist <= 0.0001f) return;

            Vector3 dir = delta / dist;

            transform.position += dir * moveSpeed * Time.deltaTime;

            Quaternion desiredRot = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                desiredRot,
                rotateSpeedDegreesPerSecond * Time.deltaTime
            );
        }

        private Vector3? GetCurrentTarget() {
            if (moveMode == ActiveMoveMode.ChargeSnapshot) {
                return snapshotTarget;
            }

            if (playerTarget == null) return null;
            return playerTarget.position;
        }
    }
}