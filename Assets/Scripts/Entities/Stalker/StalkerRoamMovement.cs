using UnityEngine;
using FNaS.MasterNodes;

namespace FNaS.Entities.Stalker {
    public class StalkerRoamMovement : StalkerMovementBase {
        public enum RoamMode {
            StraightLine,
            RandomWalk
        }

        [Header("Roam Mode")]
        public RoamMode roamMode = RoamMode.RandomWalk;
        public Transform moveTarget;
        public float moveSpeed = 2f;
        public float directionChangeInterval = 2f;
        public float roamRadius = 6f;

        [Header("Optional Semantic Node")]
        [Tooltip("Optional semantic node to report while roaming. Leave null if unused.")]
        public MasterNode currentMasterNode;

        private Vector3 startPos;
        private Vector3 currentDirection;
        private float directionTimer;
        private bool initialized;

        public override MasterNode CurrentMasterNode => currentMasterNode;
        public override bool AtDoor => false;

        public override void Initialize() {
            if (initialized) return;
            initialized = true;

            if (moveTarget == null) moveTarget = transform;
            startPos = moveTarget.position;
            PickRandomDirection();
        }

        public override void TickMovementVisuals() {
            if (moveTarget == null) return;

            if (roamMode == RoamMode.StraightLine) {
                moveTarget.position += moveTarget.forward * moveSpeed * Time.deltaTime;
                return;
            }

            directionTimer -= Time.deltaTime;
            if (directionTimer <= 0f) {
                PickRandomDirection();
            }

            Vector3 next = moveTarget.position + currentDirection * moveSpeed * Time.deltaTime;
            Vector3 flatOffset = next - startPos;
            flatOffset.y = 0f;

            if (flatOffset.magnitude > roamRadius) {
                currentDirection = (-flatOffset).normalized;
                next = moveTarget.position + currentDirection * moveSpeed * Time.deltaTime;
            }

            moveTarget.position = next;

            if (currentDirection.sqrMagnitude > 0.0001f) {
                moveTarget.rotation = Quaternion.LookRotation(currentDirection, Vector3.up);
            }
        }

        public override void RefreshOccupancy() {
            // No node occupancy in roam mode.
        }

        public override bool TryAdvance(MasterNode playerNode, bool allowShareNodeWithPlayer) {
            Nudge();
            return true;
        }

        public override bool PushBack(int steps) {
            if (moveTarget == null) return false;

            moveTarget.position -= moveTarget.forward * Mathf.Max(1, steps);
            return true;
        }

        public override void ReappearInFirstNNodes(int firstNNodes) {
            if (moveTarget == null) return;

            moveTarget.position = startPos;
            PickRandomDirection();
        }

        public override void ClearOccupancy() {
            // No node occupancy in roam mode.
        }

        private void PickRandomDirection() {
            directionTimer = Mathf.Max(0.2f, directionChangeInterval);

            Vector2 v = Random.insideUnitCircle.normalized;
            if (v.sqrMagnitude < 0.001f) v = Vector2.right;

            currentDirection = new Vector3(v.x, 0f, v.y);
        }

        private void Nudge() {
            if (moveTarget == null) return;

            Vector3 step = currentDirection.sqrMagnitude > 0.001f
                ? currentDirection
                : moveTarget.forward;

            moveTarget.position += step.normalized * 0.75f;
        }
    }
}