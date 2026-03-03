using System.Collections;
using UnityEngine;
using FNaS.MasterNodes;
using FNaS.Systems;

namespace FNaS.Gameplay {
    public class PlayerWaypointController : MonoBehaviour {
        [Header("References")]
        public Transform rigTransform;   // MOVE THIS (player root)
        public Transform yawPivot;       // ROTATE THIS (yaw)
        public Transform pitchPivot;     // ROTATE THIS (pitch) - kept neutral during movement
        public Waypoint startingWaypoint;

        [Header("Systems")]
        public BlockerRegistry blockerRegistry;
        public LoseState loseState;

        [Header("Movement")]
        public float moveSpeed = 6.0f; // units/sec

        [Header("State (read-only)")]
        public Waypoint CurrentWaypoint;

        [Header("Audio")]
        public AudioSource sfxSource;
        public AudioClip footstepClip;

        // Canonical location for gameplay systems
        [SerializeField] private MasterNode currentMasterNode;
        public MasterNode CurrentMasterNode => currentMasterNode;

        private bool isMoving;
        public bool IsMoving => isMoving;

        private void Start() {
            if (startingWaypoint == null) {
                Debug.LogError("PlayerWaypointController: startingWaypoint is not set.");
                enabled = false;
                return;
            }
            SetCurrentWaypointInstant(startingWaypoint);
        }

        public void BeginTransition(WaypointTransition tr) {
            if (isMoving) return;
            if (tr == null || tr.target == null) return;
            if (loseState != null && loseState.hasLost) return;

            // Resolve master nodes
            MasterNode fromMaster = ResolveMasterNode(CurrentWaypoint);
            MasterNode toMaster = ResolveMasterNode(tr.target);

            // If this is a forward move, and the forward exit is blocked at the *current* master node, you lose.
            if (tr.tag == TransitionTag.Forward && blockerRegistry != null && fromMaster != null) {
                if (blockerRegistry.IsForwardExitBlockedAt(fromMaster)) {
                    loseState?.TriggerLose("Tried to move past a blocking entity.");
                    return;
                }
            }

            StartCoroutine(MoveAlongTransition(tr, toMaster));
        }

        private IEnumerator MoveAlongTransition(WaypointTransition tr, MasterNode toMaster) {
            isMoving = true;

            // --- Tunables ---
            float minTurnDuration = 0.01f;         // safety for waypoint turns
            float turnAnticipationSeconds = 0.15f; // start turn slightly before waypoint

            if (sfxSource != null && footstepClip != null) {
                sfxSource.clip = footstepClip;
                sfxSource.loop = true;
                sfxSource.Play();
            }

            // --- Helpers ---
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
                if (yawPivot == null) return;

                Quaternion current = yawPivot.rotation;
                Quaternion desired = current;

                if (tr.facingMode == MoveFacingMode.FaceMoveDirection) {
                    desired = LookYawDir(moveDir, current);
                }
                else if (tr.facingMode == MoveFacingMode.FaceLookTarget) {
                    desired = LookYawAt(pos, tr.travelLookTarget, current);
                }
                else {
                    return; // KeepFacing
                }

                float dur = Mathf.Max(0f, tr.travelTurnDuration);
                if (dur <= 0f) {
                    yawPivot.rotation = desired;
                    return;
                }

                float k = 1f - Mathf.Exp(-Time.deltaTime / dur);
                yawPivot.rotation = Quaternion.Slerp(current, desired, k);
            }

            // --- Capture start state ---
            Vector3 startPos = rigTransform.position;
            Quaternion startYaw = yawPivot != null ? yawPivot.rotation : rigTransform.rotation;

            // --- Build path points (start -> waypoints -> end) ---
            Vector3 endPos = tr.target.transform.position;
            endPos.y = startPos.y;

            int midCount = (tr.waypoints != null) ? tr.waypoints.Length : 0;
            int totalPts = midCount + 2;

            Vector3[] pts = new Vector3[totalPts];
            pts[0] = startPos;

            for (int i = 0; i < midCount; i++) {
                Transform pT = tr.waypoints[i].point;
                Vector3 p = (pT != null) ? pT.position : pts[i];
                p.y = startPos.y;
                pts[i + 1] = p;
            }

            pts[totalPts - 1] = endPos;

            // --- Compute segment lengths for constant-speed travel ---
            float[] segLen = new float[totalPts - 1];
            float totalLen = 0f;

            for (int i = 0; i < totalPts - 1; i++) {
                float d = Vector3.Distance(pts[i], pts[i + 1]);
                segLen[i] = d;
                totalLen += d;
            }

            if (totalLen < 0.0001f) {
                rigTransform.position = endPos;
                if (pitchPivot != null) pitchPivot.localRotation = Quaternion.identity;
                CurrentWaypoint = tr.target;
                currentMasterNode = toMaster;
                if (sfxSource != null) sfxSource.Stop();
                isMoving = false;
                yield break;
            }

            float speed = Mathf.Max(0.01f, moveSpeed);

            // --- Waypoint turn scheduling (optional) ---
            int nextTurnIndex = 0;
            Quaternion turnFrom = startYaw;
            Quaternion turnTo = startYaw;
            float turnT = 1f;
            float turnDuration = minTurnDuration;
            bool turningActive = false;

            void BeginWaypointTurn(Transform lookT, Vector3 pos, float dur) {
                Quaternion current = (yawPivot != null) ? yawPivot.rotation : rigTransform.rotation;
                turnFrom = current;
                turnTo = LookYawAt(pos, lookT, current);
                turnDuration = Mathf.Max(minTurnDuration, dur);
                turnT = 0f;
                turningActive = true;
            }

            float[] distAtPoint = new float[totalPts];
            distAtPoint[0] = 0f;
            for (int i = 1; i < totalPts; i++)
                distAtPoint[i] = distAtPoint[i - 1] + segLen[i - 1];

            float AnticipationDist() => speed * Mathf.Max(0f, turnAnticipationSeconds);

            // --- Travel loop by traveled distance ---
            float traveled = 0f;

            while (traveled < totalLen) {
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

                Vector3 moveDir = (p1 - p0);
                moveDir.y = 0f;
                ApplyTravelFacing(pos, moveDir);

                if (yawPivot != null) {
                    while (nextTurnIndex < midCount) {
                        int ptIdx = nextTurnIndex + 1;
                        float dToWaypoint = distAtPoint[ptIdx] - traveled;

                        if (dToWaypoint <= AnticipationDist()) {
                            Transform lt = tr.waypoints[nextTurnIndex].lookTarget;
                            float dur = tr.waypoints[nextTurnIndex].turnDuration;

                            if (lt != null)
                                BeginWaypointTurn(lt, pos, dur);

                            nextTurnIndex++;
                        }
                        else break;
                    }

                    if (turningActive) {
                        turnT += Time.deltaTime / Mathf.Max(minTurnDuration, turnDuration);
                        float a = Mathf.Clamp01(turnT);
                        yawPivot.rotation = Quaternion.Slerp(turnFrom, turnTo, a);
                        if (a >= 1f) turningActive = false;
                    }
                }

                if (pitchPivot != null)
                    pitchPivot.localRotation = Quaternion.identity;

                yield return null;
            }

            // snap to final position
            rigTransform.position = endPos;
            if (pitchPivot != null) pitchPivot.localRotation = Quaternion.identity;

            CurrentWaypoint = tr.target;
            currentMasterNode = toMaster;

            if (sfxSource != null) sfxSource.Stop();
            isMoving = false;
        }

        private void SetCurrentWaypointInstant(Waypoint waypoint) {
            CurrentWaypoint = waypoint;
            currentMasterNode = ResolveMasterNode(waypoint);

            Vector3 pos = waypoint.transform.position;
            pos.y = rigTransform.position.y;
            rigTransform.position = pos;

            if (pitchPivot != null)
                pitchPivot.localRotation = Quaternion.identity;
        }

        private MasterNode ResolveMasterNode(Waypoint waypoint) {
            if (waypoint == null) return null;
            return waypoint.masterNode;
        }
    }
}