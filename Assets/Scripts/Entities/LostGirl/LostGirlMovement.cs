using UnityEngine;
using UnityEngine.AI;
using FNaS.Settings;
using FNaS.Systems;

namespace FNaS.Entities.LostGirl {
    public class LostGirlMovement : MonoBehaviour, IRuntimeSettingsConsumer {
        public enum ActiveMoveMode {
            ChaseCurrentPlayer,
            ChargeSnapshot
        }

        public enum FailureReason {
            None,
            MissingTarget,
            InvalidPath,
            ClosedDoor,
            Stuck,
            SpawnNotOnNavMesh
        }

        [Header("References")]
        public NavMeshAgent agent;

        [Header("Movement")]
        public ActiveMoveMode moveMode = ActiveMoveMode.ChargeSnapshot;
        public float moveSpeed = 8f;

        [Tooltip("How often ChargeSnapshot refreshes toward the player's latest position.")]
        public float snapshotRefreshInterval = 0.3f;

        [Header("NavMesh")]
        public float stoppingDistance = 0.05f;
        public float destinationSampleDistance = 1.25f;
        public float repathInterval = 0.10f;
        public float invalidPathGraceSeconds = 0.20f;

        [Header("Spawn")]
        public float spawnSnapDistance = 0.5f;

        [Header("Stuck Detection")]
        public float stuckCheckInterval = 0.35f;
        public float minProgressDistance = 0.02f;
        public float stuckSecondsBeforeFail = 1.5f;

        [Header("Debug")]
        public bool verboseLogging = false;

        [Header("Runtime (read-only)")]
        [SerializeField] private bool isActive;
        [SerializeField] private Vector3 snapshotTarget;
        [SerializeField] private FailureReason lastFailureReason = FailureReason.None;

        private Transform playerTarget;
        private float nextRepathTime;
        private float invalidPathTimer;
        private float stuckTimer;
        private float nextStuckCheckTime;
        private float lastSnapshotRefreshTime;
        private Vector3 lastStuckCheckPosition;
        private bool initialDestinationIssued;
        private Vector3 lastLoggedDestination = new(float.NaN, float.NaN, float.NaN);

        public bool IsActive => isActive;
        public Vector3 SnapshotTarget => snapshotTarget;
        public FailureReason LastFailureReason => lastFailureReason;

        public event System.Action<FailureReason, Door> OnMovementFailed;
        public event System.Action<Door> OnReachedOpenDoor;

        public void ApplyRuntimeSettings(RuntimeGameSettings settings) {
            if (settings == null) return;
            moveSpeed = settings.GetFloat("lostGirl.moveSpeed");
            if (agent != null) agent.speed = moveSpeed;
        }

        private void Awake() {
            if (agent == null) agent = GetComponent<NavMeshAgent>();

            if (agent == null) {
                Debug.LogError("LostGirlMovement: Missing NavMeshAgent.", this);
                enabled = false;
                return;
            }

            agent.speed = moveSpeed;
            agent.stoppingDistance = stoppingDistance;
            agent.updateRotation = true;
            agent.autoBraking = true;
        }

        public bool WarpToSpawnPoint(Transform spawnPoint) {
            if (spawnPoint == null || agent == null || !agent.enabled) {
                LogWarning("Warp failed: null spawn or agent.");
                return false;
            }

            transform.rotation = spawnPoint.rotation;

            if (!NavMesh.SamplePosition(
                spawnPoint.position,
                out NavMeshHit hit,
                Mathf.Max(0.01f, spawnSnapDistance),
                NavMesh.AllAreas)) {
                LogWarning($"Warp failed: no NavMesh near {spawnPoint.position}");
                return false;
            }

            bool warped = agent.Warp(hit.position);
            Log($"Warp result={warped} pos={hit.position}");

            if (!warped) return false;

            transform.rotation = spawnPoint.rotation;
            agent.ResetPath();
            return true;
        }

        public void BeginMovement(Transform player, ActiveMoveMode mode) {
            playerTarget = player;
            moveMode = mode;
            isActive = true;
            lastFailureReason = FailureReason.None;

            invalidPathTimer = 0f;
            stuckTimer = 0f;
            nextRepathTime = 0f;
            nextStuckCheckTime = Time.time + stuckCheckInterval;
            lastSnapshotRefreshTime = Time.time;
            lastStuckCheckPosition = transform.position;
            initialDestinationIssued = false;
            lastLoggedDestination = new Vector3(float.NaN, float.NaN, float.NaN);

            if (playerTarget != null) {
                snapshotTarget = playerTarget.position;
            }

            if (!agent.isOnNavMesh) {
                Fail(FailureReason.SpawnNotOnNavMesh, null);
                return;
            }

            agent.isStopped = false;
            agent.speed = moveSpeed;
            agent.stoppingDistance = stoppingDistance;

            UpdateDestination(force: true);
        }

        public void StopMovement() {
            isActive = false;
            playerTarget = null;
            invalidPathTimer = 0f;
            stuckTimer = 0f;
            initialDestinationIssued = false;

            if (agent != null && agent.enabled) {
                agent.isStopped = true;
                agent.ResetPath();
            }
        }

        public void NotifyDoorContact(Door door) {
            if (!isActive || door == null) return;

            if (door.isOpen) {
                LogWarning($"Open door reached: {door.name}");
                StopMovement();
                OnReachedOpenDoor?.Invoke(door);
                return;
            }

            LogWarning($"Closed door collision on {door.name}");
            Fail(FailureReason.ClosedDoor, door);
        }

        private void Update() {
            if (!isActive || agent == null || !agent.enabled) return;

            RefreshSnapshotTargetIfNeeded();

            Vector3? target = GetCurrentTarget();
            if (!target.HasValue) {
                Fail(FailureReason.MissingTarget, null);
                return;
            }

            UpdateDestination(force: false);
            UpdatePathFailure();
            UpdateStuckFailure();
        }

        private void RefreshSnapshotTargetIfNeeded() {
            if (moveMode != ActiveMoveMode.ChargeSnapshot) return;
            if (playerTarget == null) return;

            if (Time.time - lastSnapshotRefreshTime >= snapshotRefreshInterval) {
                snapshotTarget = playerTarget.position;
                lastSnapshotRefreshTime = Time.time;
            }
        }

        private Vector3? GetCurrentTarget() {
            if (moveMode == ActiveMoveMode.ChargeSnapshot) {
                return snapshotTarget;
            }

            if (playerTarget == null) return null;
            return playerTarget.position;
        }

        private void UpdateDestination(bool force) {
            if (!force && Time.time < nextRepathTime && initialDestinationIssued) return;

            nextRepathTime = Time.time + Mathf.Max(0.02f, repathInterval);

            Vector3? target = GetCurrentTarget();
            if (!target.HasValue) return;

            Vector3 desired = target.Value;

            if (NavMesh.SamplePosition(desired, out NavMeshHit hit, destinationSampleDistance, NavMesh.AllAreas)) {
                agent.SetDestination(hit.position);
                LogDestinationIfChanged(hit.position);
            }
            else {
                agent.SetDestination(desired);
                LogDestinationIfChanged(desired);
            }

            initialDestinationIssued = true;
        }

        private void UpdatePathFailure() {
            if (agent.pathPending || !initialDestinationIssued) return;

            bool badPath =
                agent.pathStatus == NavMeshPathStatus.PathInvalid ||
                agent.pathStatus == NavMeshPathStatus.PathPartial;

            if (!badPath) {
                invalidPathTimer = 0f;
                return;
            }

            invalidPathTimer += Time.deltaTime;
            if (invalidPathTimer >= invalidPathGraceSeconds) {
                Fail(FailureReason.InvalidPath, null);
            }
        }

        private void UpdateStuckFailure() {
            if (Time.time < nextStuckCheckTime) return;
            nextStuckCheckTime = Time.time + Mathf.Max(0.05f, stuckCheckInterval);

            if (agent.pathPending || !agent.hasPath) {
                lastStuckCheckPosition = transform.position;
                stuckTimer = 0f;
                return;
            }

            if (agent.remainingDistance <= agent.stoppingDistance + 0.05f) {
                lastStuckCheckPosition = transform.position;
                stuckTimer = 0f;
                return;
            }

            float moved = Vector3.Distance(transform.position, lastStuckCheckPosition);
            lastStuckCheckPosition = transform.position;

            if (moved < minProgressDistance) {
                stuckTimer += stuckCheckInterval;
                if (stuckTimer >= stuckSecondsBeforeFail) {
                    Fail(FailureReason.Stuck, null);
                }
            }
            else {
                stuckTimer = 0f;
            }
        }

        private void Fail(FailureReason reason, Door door) {
            if (!isActive) return;

            lastFailureReason = reason;
            LogWarning($"Fail reason={reason} door={(door != null ? door.name : "null")}");

            StopMovement();
            OnMovementFailed?.Invoke(reason, door);
        }

        private void LogDestinationIfChanged(Vector3 destination) {
            if (!verboseLogging) return;

            bool first = float.IsNaN(lastLoggedDestination.x);
            bool changed = Vector3.Distance(lastLoggedDestination, destination) > 0.25f;

            if (first || changed) {
                lastLoggedDestination = destination;
                Debug.Log($"[LostGirlMovement] destination={destination}", this);
            }
        }

        private void Log(string msg) {
            if (!verboseLogging) return;
            Debug.Log($"[LostGirlMovement] {msg}", this);
        }

        private void LogWarning(string msg) {
            if (!verboseLogging) return;
            Debug.LogWarning($"[LostGirlMovement] {msg}", this);
        }
    }
}