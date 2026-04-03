using System.Collections;
using UnityEngine;
using FNaS.MasterNodes;
using FNaS.Entities.Stalker;
using FNaS.Systems;
using FNaS.Settings;

namespace FNaS.Gameplay {
    public class PlayerWaypointController : PlayerMovementBase, IRuntimeSettingsConsumer {
        [Header("References")]
        public Transform rigTransform;
        public Transform viewPivot;
        public Waypoint startingWaypoint;

        [Header("Systems")]
        public BlockerRegistry blockerRegistry;
        public LoseState loseState;

        [Header("Movement")]
        public float moveSpeed = 6.0f;

        [Header("View Bob")]
        [Tooltip("Enable subtle bobbing while moving between waypoints.")]
        public bool enableMoveBob = true;

        [Tooltip("Vertical bob height in local units.")]
        public float bobHeight = 0.035f;

        [Tooltip("Side-to-side sway in local units.")]
        public float bobSideAmount = 0.015f;

        [Tooltip("How many bob cycles happen during one movement transition.")]
        public float bobCyclesPerMove = 1.5f;

        [Tooltip("How quickly the viewPivot returns to neutral after movement.")]
        public float bobReturnSpeed = 14f;

        [Header("State (read-only)")]
        public Waypoint CurrentWaypoint;

        [Header("Audio")]
        public AudioSource sfxSource;
        public AudioClip footstepClip;

        [Header("Stalker")]
        [SerializeField] private StalkerJumpscareController stalkerJumpscare;

        [SerializeField] private MasterNode currentMasterNode;
        public override MasterNode CurrentMasterNode => currentMasterNode;

        private bool isMoving;
        public override bool IsMoving => isMoving;

        public override Transform RigTransform => rigTransform;
        public override Transform ViewTransform => viewPivot != null ? viewPivot : transform;
        private Coroutine activeMoveRoutine;

        private Vector3 baseViewPivotLocalPos;
        private bool cachedBaseViewPivotLocalPos;

        private bool movementPaused;
        private bool footstepsWerePlayingBeforePause;

        private readonly System.Collections.Generic.Dictionary<Door, int> doorTraversalTokens = new System.Collections.Generic.Dictionary<Door, int>();

        public override void Initialize(PlayerEntity player, PlayerInputController input) {
            if (startingWaypoint == null) {
                Debug.LogError("PlayerWaypointController: startingWaypoint is not set.");
                enabled = false;
                return;
            }

            CacheBaseViewPivotLocalPos();
            SetCurrentWaypointInstant(startingWaypoint);
        }

        private void Start() {
            CacheBaseViewPivotLocalPos();

            if (CurrentWaypoint == null) {
                Initialize(null, null);
            }
        }

        private void Update() {
            if (!isMoving) {
                RestoreViewPivotBob();
            }
        }

        public void ApplyRuntimeSettings(RuntimeGameSettings settings) {
            if (settings == null) {
                Debug.LogWarning("PlayerWaypointController: RuntimeGameSettings not found. Using inspector moveSpeed.");
                return;
            }

            moveSpeed = settings.GetFloat("player.moveSpeed");
        }

        public bool BeginTransition(WaypointTransition tr) {
            if (isMoving) return false;
            if (tr == null || tr.target == null) return false;
            if (loseState != null && loseState.hasLost) return false;

            MasterNode fromMaster = ResolveMasterNode(CurrentWaypoint);
            MasterNode toMaster = ResolveMasterNode(tr.target);

            if (tr.tag == TransitionTag.Forward && blockerRegistry != null && fromMaster != null) {
                if (blockerRegistry.IsForwardExitBlockedAt(fromMaster)) {
                    if (stalkerJumpscare != null) {
                        stalkerJumpscare.PlayJumpscare("Tried to move past a blocking entity.");
                    }
                    else {
                        loseState?.TriggerLose("Tried to move past a blocking entity.");
                    }
                    return false;
                }
            }

            activeMoveRoutine = StartCoroutine(MoveAlongTransition(tr, toMaster));
            return true;
        }

        private IEnumerator MoveAlongTransition(WaypointTransition tr, MasterNode toMaster) {
            isMoving = true;
            CacheBaseViewPivotLocalPos();

            Door transitionDoor = tr.doorToUse;
            int transitionDoorToken = 0;

            if (transitionDoor != null) {
                transitionDoorToken = BumpDoorTraversalToken(transitionDoor);
                transitionDoor.SetTraversalOpen(true);
            }

            float minTurnDuration = 0.01f;
            float turnAnticipationSeconds = 0.15f;

            if (sfxSource != null && footstepClip != null) {
                sfxSource.clip = footstepClip;
                sfxSource.loop = true;
                sfxSource.Play();
            }

            static Vector3 FlattenY(Vector3 v) { v.y = 0f; return v; }

            Quaternion LookYawAt(Vector3 fromPos, Transform lookTarget, Quaternion fallback) {
                if (lookTarget == null) return fallback;
                Vector3 dir = lookTarget.position - fromPos;
                dir = FlattenY(dir);
                if (dir.sqrMagnitude < 0.0001f) return fallback;
                return Quaternion.LookRotation(dir.normalized, Vector3.up);
            }

            Quaternion LookYawDir(Vector3 dir, Quaternion fallback) {
                dir = FlattenY(dir);
                if (dir.sqrMagnitude < 0.0001f) return fallback;
                return Quaternion.LookRotation(dir.normalized, Vector3.up);
            }

            void ApplyTravelFacing(Vector3 pos, Vector3 moveDir) {
                if (viewPivot == null) return;

                Quaternion current = viewPivot.rotation;
                Quaternion desired = current;

                if (tr.facingMode == MoveFacingMode.FaceMoveDirection) {
                    desired = LookYawDir(moveDir, current);
                }
                else if (tr.facingMode == MoveFacingMode.FaceLookTarget) {
                    desired = LookYawAt(pos, tr.travelLookTarget, current);
                }
                else {
                    return;
                }

                float dur = Mathf.Max(0f, tr.travelTurnDuration);
                if (dur <= 0f) {
                    viewPivot.rotation = desired;
                    return;
                }

                float k = 1f - Mathf.Exp(-Time.deltaTime / dur);
                viewPivot.rotation = Quaternion.Slerp(current, desired, k);
            }

            Vector3 startPos = rigTransform.position;
            Quaternion startYaw = viewPivot != null ? viewPivot.rotation : rigTransform.rotation;

            Vector3 endPos = tr.target.transform.position;
            endPos.y = startPos.y;

            int midCount = tr.waypoints != null ? tr.waypoints.Length : 0;
            int totalPts = midCount + 2;

            Vector3[] pts = new Vector3[totalPts];
            pts[0] = startPos;

            for (int i = 0; i < midCount; i++) {
                Transform pT = tr.waypoints[i].point;
                Vector3 p = pT != null ? pT.position : pts[i];
                p.y = startPos.y;
                pts[i + 1] = p;
            }

            pts[totalPts - 1] = endPos;

            float[] segLen = new float[totalPts - 1];
            float totalLen = 0f;

            for (int i = 0; i < totalPts - 1; i++) {
                float d = Vector3.Distance(pts[i], pts[i + 1]);
                segLen[i] = d;
                totalLen += d;
            }

            if (totalLen < 0.0001f) {
                rigTransform.position = endPos;
                CurrentWaypoint = tr.target;
                currentMasterNode = toMaster;

                if (sfxSource != null) sfxSource.Stop();
                isMoving = false;
                RestoreViewPivotBobImmediate();

                if (transitionDoor != null) {
                    StartCoroutine(CloseTraversalDoorAfterDelay(
                        transitionDoor,
                        tr.doorCloseDelay,
                        transitionDoorToken
                    ));
                }

                yield break;
            }

            float speed = Mathf.Max(0.01f, moveSpeed);

            int nextTurnIndex = 0;
            Quaternion turnFrom = startYaw;
            Quaternion turnTo = startYaw;
            float turnT = 1f;
            float turnDuration = minTurnDuration;
            bool turningActive = false;

            void BeginWaypointTurn(Transform lookT, Vector3 pos, float dur) {
                Quaternion current = viewPivot != null ? viewPivot.rotation : rigTransform.rotation;
                turnFrom = current;
                turnTo = LookYawAt(pos, lookT, current);
                turnDuration = Mathf.Max(minTurnDuration, dur);
                turnT = 0f;
                turningActive = true;
            }

            float[] distAtPoint = new float[totalPts];
            distAtPoint[0] = 0f;
            for (int i = 1; i < totalPts; i++) {
                distAtPoint[i] = distAtPoint[i - 1] + segLen[i - 1];
            }

            float AnticipationDist() => speed * Mathf.Max(0f, turnAnticipationSeconds);

            float traveled = 0f;

            while (traveled < totalLen) {
                if (movementPaused) {
                    yield return null;
                    continue;
                }

                traveled += speed * Time.deltaTime;
                traveled = Mathf.Min(traveled, totalLen);

                int seg = 0;
                float segStartDist = 0f;
                for (int i = 0; i < segLen.Length; i++) {
                    float segEndDist = segStartDist + segLen[i];
                    if (traveled <= segEndDist || i == segLen.Length - 1) {
                        seg = i;
                        break;
                    }
                    segStartDist = segEndDist;
                }

                float segD = Mathf.Max(0.0001f, segLen[seg]);
                float u = (traveled - segStartDist) / segD;

                Vector3 p0 = pts[seg];
                Vector3 p1 = pts[seg + 1];
                Vector3 pos = Vector3.Lerp(p0, p1, u);
                rigTransform.position = pos;

                Vector3 moveDir = p1 - p0;
                moveDir.y = 0f;
                ApplyTravelFacing(pos, moveDir);

                if (viewPivot != null) {
                    while (nextTurnIndex < midCount) {
                        int ptIdx = nextTurnIndex + 1;
                        float dToWaypoint = distAtPoint[ptIdx] - traveled;

                        if (dToWaypoint <= AnticipationDist()) {
                            Transform lt = tr.waypoints[nextTurnIndex].lookTarget;
                            float dur = tr.waypoints[nextTurnIndex].turnDuration;

                            if (lt != null) {
                                BeginWaypointTurn(lt, pos, dur);
                            }

                            nextTurnIndex++;
                        }
                        else break;
                    }

                    if (turningActive) {
                        turnT += Time.deltaTime / Mathf.Max(minTurnDuration, turnDuration);
                        float a = Mathf.Clamp01(turnT);
                        viewPivot.rotation = Quaternion.Slerp(turnFrom, turnTo, a);
                        if (a >= 1f) turningActive = false;
                    }

                    ApplyMoveBob(traveled / totalLen);
                }

                yield return null;
            }

            rigTransform.position = endPos;
            CurrentWaypoint = tr.target;
            currentMasterNode = toMaster;

            if (sfxSource != null) sfxSource.Stop();
            isMoving = false;
            RestoreViewPivotBobImmediate();

            if (transitionDoor != null) {
                StartCoroutine(CloseTraversalDoorAfterDelay(
                    transitionDoor,
                    tr.doorCloseDelay,
                    transitionDoorToken
                ));
            }

            activeMoveRoutine = null;
        }

        private void ApplyMoveBob(float normalizedMoveT) {
            if (!enableMoveBob || viewPivot == null) return;
            CacheBaseViewPivotLocalPos();

            float t = Mathf.Clamp01(normalizedMoveT);

            // Zero bob at start/end, strongest in the middle.
            float envelope = Mathf.Sin(t * Mathf.PI);

            float phase = t * Mathf.PI * 2f * Mathf.Max(0.01f, bobCyclesPerMove);

            float vertical = Mathf.Sin(phase) * bobHeight * envelope;
            float side = Mathf.Sin(phase * 0.5f) * bobSideAmount * envelope;

            viewPivot.localPosition = baseViewPivotLocalPos + new Vector3(side, vertical, 0f);
        }

        private void RestoreViewPivotBob() {
            if (viewPivot == null) return;
            CacheBaseViewPivotLocalPos();

            float k = 1f - Mathf.Exp(-bobReturnSpeed * Time.deltaTime);
            viewPivot.localPosition = Vector3.Lerp(viewPivot.localPosition, baseViewPivotLocalPos, k);
        }

        private void RestoreViewPivotBobImmediate() {
            if (viewPivot == null) return;
            CacheBaseViewPivotLocalPos();
            viewPivot.localPosition = baseViewPivotLocalPos;
        }

        private void CacheBaseViewPivotLocalPos() {
            if (viewPivot == null || cachedBaseViewPivotLocalPos) return;
            baseViewPivotLocalPos = viewPivot.localPosition;
            cachedBaseViewPivotLocalPos = true;
        }

        private void SetCurrentWaypointInstant(Waypoint waypoint) {
            CurrentWaypoint = waypoint;
            currentMasterNode = ResolveMasterNode(waypoint);

            Vector3 pos = waypoint.transform.position;
            pos.y = rigTransform.position.y;
            rigTransform.position = pos;
        }

        private MasterNode ResolveMasterNode(Waypoint waypoint) {
            return waypoint != null ? waypoint.masterNode : null;
        }

        public void CancelActiveMovementImmediate() {
            if (activeMoveRoutine != null) {
                StopCoroutine(activeMoveRoutine);
                activeMoveRoutine = null;
            }

            if (sfxSource != null) {
                sfxSource.Stop();
            }

            movementPaused = false;
            footstepsWerePlayingBeforePause = false;
            isMoving = false;
            RestoreViewPivotBobImmediate();
        }

        public void PauseActiveMovement() {
            movementPaused = true;

            if (sfxSource != null) {
                footstepsWerePlayingBeforePause = sfxSource.isPlaying;
                sfxSource.Pause();
            }
        }

        public void ResumeActiveMovement() {
            movementPaused = false;

            if (sfxSource != null && footstepsWerePlayingBeforePause) {
                sfxSource.UnPause();
            }

            footstepsWerePlayingBeforePause = false;
        }

        private int BumpDoorTraversalToken(Door door) {
            if (door == null) return 0;

            int next = 1;
            if (doorTraversalTokens.TryGetValue(door, out int current)) {
                next = current + 1;
            }

            doorTraversalTokens[door] = next;
            return next;
        }

        private bool IsLatestDoorTraversalToken(Door door, int token) {
            if (door == null) return false;
            return doorTraversalTokens.TryGetValue(door, out int current) && current == token;
        }

        private IEnumerator CloseTraversalDoorAfterDelay(Door door, float delay, int token) {
            if (door == null) yield break;

            if (delay > 0f) {
                yield return new WaitForSeconds(delay);
            }

            if (!IsLatestDoorTraversalToken(door, token)) {
                yield break;
            }

            door.SetTraversalOpen(false);
        }
    }
}