using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerNodeController : MonoBehaviour {
    [Header("References")]
    public Transform rigTransform;
    public Transform yawPivot;
    public Transform pitchPivot;
    public Node startingNode;

    [Header("Movement")]
    public float moveSpeed = 6.0f; // units/sec (tune in Inspector)

    [Header("State (read-only)")]
    public Node CurrentNode;

    private PlayerInputActions input;
    private Vector2 lastMove;
    private bool isMoving;

    private void Awake() {
        input = new PlayerInputActions();
    }

    private void OnEnable() {
        input.Enable();
    }

    private void OnDisable() {
        input.Disable();
    }

    private void Start() {
        if (startingNode == null) {
            Debug.LogError("PlayerNodeController: startingNode is not set.");
            enabled = false;
            return;
        }
        SetCurrentNodeInstant(startingNode);
    }

    private void Update() {
        if (isMoving) return;

        Vector2 move = input.Player.Move.ReadValue<Vector2>();
        bool pressedNow = move.sqrMagnitude > 0.25f;
        bool pressedBefore = lastMove.sqrMagnitude > 0.25f;

        if (pressedNow && !pressedBefore) {
            Direction dir = VectorToDir(move);
            TryMove(dir);
        }

        lastMove = move;
    }

    private void TryMove(Direction dir) {
        if (CurrentNode == null) return;

        NodeTransition tr = CurrentNode.GetTransition(dir);
        if (tr == null || tr.target == null) return;

        StartCoroutine(MoveAlongTransition(tr));
    }

    private IEnumerator MoveAlongTransition(NodeTransition tr) {
        isMoving = true;

        // --- Tunables (put these as public fields if you want to tweak in Inspector) ---
        float minTurnDuration = 0.01f;           // safety
        float turnAnticipationSeconds = 0.15f;   // start turning this many seconds BEFORE reaching a turn point
        float settleToNodeViewSeconds = 0.08f;   // small final "snap/settle" into node view

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

        // --- Capture start state ---
        Vector3 startPos = rigTransform.position;
        Quaternion startYaw = yawPivot != null ? yawPivot.rotation : rigTransform.rotation;

        // --- Build path points (start -> waypoints -> end) ---
        Vector3 endPos = tr.target.transform.position;
        endPos.y = startPos.y;

        int midCount = (tr.waypoints != null) ? tr.waypoints.Length : 0; // <- adjust name if yours differs
        int totalPts = midCount + 2;

        Vector3[] pts = new Vector3[totalPts];
        pts[0] = startPos;

        for (int i = 0; i < midCount; i++) {
            Transform pT = tr.waypoints[i].point; // <- adjust: your screenshot shows "Point"
            Vector3 p = (pT != null) ? pT.position : pts[i];
            p.y = startPos.y;
            pts[i + 1] = p;
        }

        pts[totalPts - 1] = endPos;

        // --- Compute cumulative lengths for constant-speed travel ---
        float[] segLen = new float[totalPts - 1];
        float totalLen = 0f;

        for (int i = 0; i < totalPts - 1; i++) {
            float d = Vector3.Distance(pts[i], pts[i + 1]);
            segLen[i] = d;
            totalLen += d;
        }

        if (totalLen < 0.0001f) {
            // nothing to do
            rigTransform.position = endPos;
            if (pitchPivot != null) pitchPivot.localRotation = Quaternion.identity;
            CurrentNode = tr.target;
            isMoving = false;
            yield break;
        }

        // --- Determine movement duration from speed (constant speed across entire path) ---
        float speed = Mathf.Max(0.01f, moveSpeed);   // units/sec

        // --- Rotation plan ---
        // Base rotation mode:
        // - FaceMoveDirection: always face instantaneous motion dir
        // - HoldStartRotation: never rotate while moving; only settle at end
        // - BlendStartToEnd: blend startYaw -> endYaw across whole path, BUT allow sharp waypoint turns too

        Transform endLook = tr.endLookTarget != null ? tr.endLookTarget : tr.target.lookTarget; // you can reuse lookTarget
        Quaternion endYaw = LookYawAt(endPos, endLook, startYaw);

        // Waypoint turn scheduling:
        // Each waypoint can optionally specify:
        //   - lookTarget (Transform)
        //   - turnDuration (float)
        //   - enabled (bool)  [optional]
        // Your screenshot shows: "Optional turn at this point", Look Target, Turn Duration, and a checkbox.
        // We'll honor them if present.
        int nextTurnIndex = 0; // index into tr.waypoints[]
        Quaternion turnFrom = startYaw;
        Quaternion turnTo = startYaw;
        float turnT = 1f;      // 1 means not currently turning
        float turnDuration = minTurnDuration;
        bool turningActive = false;

        // function to start a turn towards some look target
        void BeginTurn(Transform lookT, Vector3 pos, float dur) {
            Quaternion current = (yawPivot != null) ? yawPivot.rotation : rigTransform.rotation;
            turnFrom = current;
            turnTo = LookYawAt(pos, lookT, current);
            turnDuration = Mathf.Max(minTurnDuration, dur);
            turnT = 0f;
            turningActive = true;
        }

        // --- Travel loop by distance along path (no per-segment easing slowdown) ---
        float traveled = 0f;

        // Precompute distances-at-waypoints so we can anticipate turns by distance
        float[] distAtPoint = new float[totalPts];
        distAtPoint[0] = 0f;
        for (int i = 1; i < totalPts; i++) {
            distAtPoint[i] = distAtPoint[i - 1] + segLen[i - 1];
        }

        // Waypoint i in tr.waypoints corresponds to pts index i+1 in pts/distAtPoint
        // We will start a turn when we're within anticipation distance of that waypoint.
        float anticipationDist() { return speed * Mathf.Max(0f, turnAnticipationSeconds); }

        while (traveled < totalLen) {
            traveled += speed * Time.deltaTime;
            traveled = Mathf.Min(traveled, totalLen);

            // --- Sample position by traveled distance along polyline ---
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

            // --- Rotation while moving ---
            if (yawPivot != null) {
                // 1) Fire waypoint turns (sharp/rushed) with anticipation
                while (nextTurnIndex < midCount) {
                    // waypoint idx -> point idx in pts
                    int ptIdx = nextTurnIndex + 1;
                    float dToWaypoint = distAtPoint[ptIdx] - traveled;

                    // check enabled if you have it; otherwise treat as enabled
                    bool enabled = true;
                    // If your Waypoint struct has a bool like "turnHere", uncomment and rename:
                    // enabled = tr.waypoints[nextTurnIndex].turnHere;

                    if (enabled && dToWaypoint <= anticipationDist()) {
                        Transform lt = tr.waypoints[nextTurnIndex].lookTarget;     // <- adjust name if needed
                        float dur = tr.waypoints[nextTurnIndex].turnDuration;      // <- adjust name if needed
                        if (lt != null) {
                            BeginTurn(lt, pos, dur);
                        }
                        nextTurnIndex++;
                    }
                    else {
                        break;
                    }
                }

                // 2) Apply chosen base rotation mode
                Quaternion baseYaw = yawPivot.rotation;

                // 3) Blend in active sharp turn (overrides base while turning)
                if (turningActive) {
                    turnT += Time.deltaTime / Mathf.Max(minTurnDuration, turnDuration);
                    float a = Mathf.Clamp01(turnT);
                    yawPivot.rotation = Quaternion.Slerp(turnFrom, turnTo, a);

                    if (a >= 1f) turningActive = false;
                }
                else {
                    yawPivot.rotation = baseYaw;
                }
            }

            // keep pitch neutral for now (since you're doing node-based look)
            if (pitchPivot != null) pitchPivot.localRotation = Quaternion.identity;

            yield return null;
        }

        // --- Snap to final position ---
        rigTransform.position = endPos;

        // --- Final settle into node view (very short; makes it feel "locked in") ---
        if (yawPivot != null) {
            Quaternion from = yawPivot.rotation;
            Quaternion to = LookYawAt(endPos, endLook, from);

            float t = 0f;
            while (t < 1f) {
                t += Time.deltaTime / Mathf.Max(0.0001f, settleToNodeViewSeconds);
                yawPivot.rotation = Quaternion.Slerp(from, to, Mathf.Clamp01(t));
                yield return null;
            }
        }

        if (pitchPivot != null) pitchPivot.localRotation = Quaternion.identity;

        CurrentNode = tr.target;
        isMoving = false;
    }

    private float DistanceAlongPathToPoint(List<Vector3> pts, float[] segLen, Vector3 pointPos) {
        // Find exact waypoint position in pts and sum lengths up to it.
        // This assumes waypoint positions are exactly one of the pts entries.
        float accum = 0f;
        for (int i = 0; i < pts.Count; i++) {
            if ((pts[i] - pointPos).sqrMagnitude < 0.0001f) {
                return accum;
            }
            if (i < segLen.Length) accum += segLen[i];
        }
        return accum;
    }

    private IEnumerator TurnTo(Quaternion targetYaw, float duration) {
        if (yawPivot == null) yield break;

        if (duration <= 0f) {
            yawPivot.rotation = targetYaw;
            yield break;
        }

        Quaternion start = yawPivot.rotation;
        float t = 0f;
        while (t < 1f) {
            t += Time.deltaTime / Mathf.Max(0.0001f, duration);
            yawPivot.rotation = Quaternion.Slerp(start, targetYaw, Mathf.Clamp01(t));
            yield return null;
        }
    }

    private void SetCurrentNodeInstant(Node node) {
        CurrentNode = node;

        Vector3 pos = node.transform.position;
        pos.y = rigTransform.position.y;
        rigTransform.position = pos;

        if (yawPivot != null && node.lookTarget != null) {
            Vector3 d = node.lookTarget.position - pos;
            d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) yawPivot.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
        }

        if (pitchPivot != null) pitchPivot.localRotation = Quaternion.identity;
    }

    private static Direction VectorToDir(Vector2 v) {
        if (Mathf.Abs(v.x) > Mathf.Abs(v.y)) return v.x > 0 ? Direction.D : Direction.A;
        return v.y > 0 ? Direction.W : Direction.S;
    }
}