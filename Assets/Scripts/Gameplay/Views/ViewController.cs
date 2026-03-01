using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FNaS.Gameplay {
    public class ViewController : MonoBehaviour {
        [Header("References")]
        [SerializeField] private PlayerWaypointController mover;

        [Header("Edge detection")]
        [SerializeField] private float edgePixels = 60f;
        [SerializeField] private float dwellTime = 0.08f;
        [SerializeField] private float switchCooldown = 0.20f;
        [SerializeField] private bool preferHorizontalInCorners = true;

        [Header("Rotation feel")]
        [SerializeField] private float rotateSpeed = 14f;
        [SerializeField] private bool snapOnEnter = true;

        [Header("Runtime (read-only)")]
        [SerializeField] private Waypoint currentWaypoint;
        [SerializeField] private View currentView;
        public Waypoint CurrentWaypoint => currentWaypoint;
        public View CurrentView => currentView;
        public System.Action OnEnteredWaypointOrView;
        public System.Action OnBeginMove;

        public Direction? ActiveMoveDir { get; private set; }

        private readonly Stack<View> history = new();

        private PlayerInputActions input;
        private float dwellTimer;
        private float cooldownTimer;
        private EdgeDir? pendingEdge;
        private Quaternion targetYaw;
        private float targetPitch;
        private Vector3 waypointBaseRigPos;
        private bool hasWaypointBaseRigPos = false;
        private Vector3 targetRigPos;
        [SerializeField] private float offsetLerpSpeed = 14f;

        private void Awake() {
            if (!mover) mover = FindFirstObjectByType<PlayerWaypointController>();
            input = new PlayerInputActions();
        }

        private void OnEnable() {
            if (input == null) input = new PlayerInputActions();
            input.Player.Enable();
            input.Player.Interact.performed += OnInteract;
        }

        private void OnDisable() {
            input?.Player.Disable();
            if (input != null) input.Player.Interact.performed -= OnInteract;
        }

        private IEnumerator Start() {
            if (mover == null) {
                Debug.LogError("ViewController: mover not assigned.");
                enabled = false;
                yield break;
            }

            // Wait until PlayerWaypointController has initialized CurrentWaypoint
            while (mover.CurrentWaypoint == null)
                yield return null;

            EnterWaypoint(mover.CurrentWaypoint, fromWaypoint: null);
        }

        private void OnInteract(InputAction.CallbackContext ctx) {
            if (mover == null || mover.IsMoving) return;
            if (currentWaypoint == null) return;

            var door = currentWaypoint.linkedDoor;
            if (door != null) door.Toggle();
        }

        private void Update() {
            if (mover == null || mover.yawPivot == null) return;
            if (mover.IsMoving) return; // don't switch views mid-move

            // Ensure waypoint stays synced
            if (mover.CurrentWaypoint != null && mover.CurrentWaypoint != currentWaypoint)
                EnterWaypoint(mover.CurrentWaypoint, fromWaypoint: null);

            cooldownTimer -= Time.deltaTime;

            // 1) Edge view switching
            if (currentView != null) {
                var edge = GetEdgeDir();
                if (edge.HasValue) {
                    if (pendingEdge != edge) {
                        pendingEdge = edge;
                        dwellTimer = 0f;
                    }

                    dwellTimer += Time.deltaTime;

                    if (dwellTimer >= dwellTime) {
                        TryEdge(edge.Value);
                        dwellTimer = 0f;
                    }
                }
                else {
                    pendingEdge = null;
                    dwellTimer = 0f;
                }
            }

            // 2) WASD movement (new input system)
            Vector2 move = input.Player.Move.ReadValue<Vector2>();

            if (move.sqrMagnitude > 0.25f) {
                Direction dir = VectorToDir(move);
                // Only trigger once per press (deadzone crossing)
                // We'll do a simple latch:
                if (!_moveLatched) {
                    _moveLatched = true;
                    TryMove(dir);
                }
            }
            else {
                _moveLatched = false;
            }

            // 3) Smooth yaw toward target
            if (currentView != null) {
                mover.yawPivot.rotation = Quaternion.Slerp(
                    mover.yawPivot.rotation,
                    targetYaw,
                    1f - Mathf.Exp(-rotateSpeed * Time.deltaTime)
                );
            }

            // Keep pitch neutral (matches your waypoint system)
            if (mover.pitchPivot != null) {
                Quaternion desired = Quaternion.Euler(targetPitch, 0f, 0f);
                mover.pitchPivot.localRotation = Quaternion.Slerp(
                    mover.pitchPivot.localRotation,
                    desired,
                    1f - Mathf.Exp(-rotateSpeed * Time.deltaTime)
                );
            }

            // 4) Smooth rig position toward target offset
            if (hasWaypointBaseRigPos && mover.rigTransform != null) {
                float k = 1f - Mathf.Exp(-offsetLerpSpeed * Time.deltaTime);
                mover.rigTransform.position = Vector3.Lerp(mover.rigTransform.position, targetRigPos, k);
            }
        }

        private bool _moveLatched = false;

        // ---------- Waypoint / View control ----------

        public void EnterWaypoint(Waypoint waypoint, Waypoint fromWaypoint) {
            currentWaypoint = waypoint;
            history.Clear();
            pendingEdge = null;
            dwellTimer = 0f;
            cooldownTimer = 0f;

            // Stable base for this waypoint (do NOT use rigTransform.position — it may already include a view offset)
            if (mover != null && mover.rigTransform != null && waypoint != null) {
                waypointBaseRigPos = waypoint.transform.position;
                waypointBaseRigPos.y = mover.rigTransform.position.y; // keep current player height
                hasWaypointBaseRigPos = true;

                // Optional but recommended: snap rig back to base when entering Waypoint
                mover.rigTransform.position = waypointBaseRigPos;
                targetRigPos = waypointBaseRigPos;
            }

            View start = waypoint != null ? waypoint.GetEntryView(fromWaypoint) : null;
            SetView(start, pushHistory: false, snap: snapOnEnter);
        }

        private void SetView(View view, bool pushHistory, bool snap) {
            if (view == null || mover == null || mover.yawPivot == null) return;

            if (pushHistory && currentView != null)
                history.Push(currentView);

            currentView = view;
            ApplyViewOffset(view);
            targetYaw = ComputeYawForView(view);
            targetPitch = view.pitchDegrees;

            if (snap)
                mover.yawPivot.rotation = targetYaw;

            OnEnteredWaypointOrView?.Invoke();
        }

        private Quaternion ComputeYawForView(View view) {
            Vector3 pos = hasWaypointBaseRigPos ? waypointBaseRigPos
                : (mover.rigTransform != null ? mover.rigTransform.position : mover.transform.position);

            if (view.lookTarget != null) {
                Vector3 d = view.lookTarget.position - pos;
                d.y = 0f;
                if (d.sqrMagnitude > 0.0001f)
                    return Quaternion.LookRotation(d.normalized, Vector3.up);
            }

            Vector3 f = view.transform.forward;
            f.y = 0f;
            if (f.sqrMagnitude > 0.0001f)
                return Quaternion.LookRotation(f.normalized, Vector3.up);

            return mover.yawPivot.rotation;
        }

        // ---------- Edge logic ----------

        private void TryEdge(EdgeDir dir) {
            if (cooldownTimer > 0f || currentView == null) return;

            var link = currentView.GetEdge(dir);
            if (link.action == View.LinkAction.None) return;

            if (link.action == View.LinkAction.Back) {
                if (history.Count > 0) {
                    SetView(history.Pop(), pushHistory: false, snap: false);
                    cooldownTimer = switchCooldown;
                }
                return;
            }

            if (link.action == View.LinkAction.GoToView && link.targetView != null) {
                SetView(link.targetView, pushHistory: true, snap: false);
                cooldownTimer = switchCooldown;
            }
        }

        private EdgeDir? GetEdgeDir() {
            Vector2 m = Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition;
            float w = Screen.width;
            float h = Screen.height;

            bool L = m.x <= edgePixels;
            bool R = m.x >= w - edgePixels;
            bool D = m.y <= edgePixels;
            bool U = m.y >= h - edgePixels;

            if (preferHorizontalInCorners) {
                if (L && !R) return EdgeDir.Left;
                if (R && !L) return EdgeDir.Right;
                if (U && !D) return EdgeDir.Up;
                if (D && !U) return EdgeDir.Down;
            }
            else {
                if (U && !D) return EdgeDir.Up;
                if (D && !U) return EdgeDir.Down;
                if (L && !R) return EdgeDir.Left;
                if (R && !L) return EdgeDir.Right;
            }

            return null;
        }

        // ---------- Movement logic ----------

        private void TryMove(Direction dir) {
            if (mover == null || mover.IsMoving) return;
            if (mover.CurrentWaypoint == null) return;

            currentWaypoint = mover.CurrentWaypoint;

            WaypointTransition tr = null;

            // View override: map key -> target waypoint -> waypoint transition
            if (currentView != null) {
                var ov = currentView.GetOverride(dir);
                if (ov.enabled && ov.targetWaypoint != null) {
                    tr = currentWaypoint.GetTransitionTo(ov.targetWaypoint);
                    // If you want to allow "override exists but no transition", you can Debug.Log here.
                }
            }

            // Fallback to waypoint mapping
            if (tr == null)
                tr = currentWaypoint.GetTransition(dir);

            if (tr == null || tr.target == null) return;

            ActiveMoveDir = dir;
            StartCoroutine(MoveAndApplyEntry(tr));
        }

        private IEnumerator MoveAndApplyEntry(WaypointTransition tr) {
            Waypoint from = mover.CurrentWaypoint;

            OnBeginMove?.Invoke();
            mover.BeginTransition(tr);

            while (mover.IsMoving)
                yield return null;

            // mover.CurrentWaypoint is now updated
            EnterWaypoint(mover.CurrentWaypoint, from);
            ActiveMoveDir = null;
        }

        private void ApplyViewOffset(View view) {
            if (!hasWaypointBaseRigPos) return;
            if (mover == null || mover.rigTransform == null) return;
            if (view == null) return;

            // Use the view's basis (stable) and ignore pitch/roll
            Vector3 f = view.transform.forward; f.y = 0f;
            Vector3 r = view.transform.right; r.y = 0f;

            if (f.sqrMagnitude < 1e-6f) f = Vector3.forward;
            if (r.sqrMagnitude < 1e-6f) r = Vector3.right;

            f.Normalize();
            r.Normalize();

            Vector3 local = view.rigLocalOffset;
            Vector3 worldOffset = r * local.x + Vector3.up * local.y + f * local.z;

            targetRigPos = waypointBaseRigPos + worldOffset;
        }

        private static Direction VectorToDir(Vector2 v) {
            if (Mathf.Abs(v.x) > Mathf.Abs(v.y)) return v.x > 0 ? Direction.D : Direction.A;
            return v.y > 0 ? Direction.W : Direction.S;
        }
    }
}