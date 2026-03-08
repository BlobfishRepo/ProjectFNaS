using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FNaS.Gameplay {
    public class ViewController : MonoBehaviour {
        [Header("References")]
        [SerializeField] private PlayerEntity player;
        [SerializeField] private PlayerWaypointController mover;
        [SerializeField] private PlayerInputController inputController;

        [Header("Edge Detection")]
        [SerializeField] private float edgePixels = 60f;
        [SerializeField] private float dwellTime = 0.08f;
        [SerializeField] private float switchCooldown = 0.20f;
        [SerializeField] private bool preferHorizontalInCorners = true;

        [Header("Rotation Feel")]
        [SerializeField] private float rotateSpeed = 14f;
        [SerializeField] private bool snapOnEnter = true;

        [Header("Rig Offset")]
        [SerializeField] private float offsetLerpSpeed = 14f;

        [Header("Runtime (read-only)")]
        [SerializeField] private Waypoint currentWaypoint;
        [SerializeField] private View currentView;
        [SerializeField] private Direction? activeMoveDir;

        public Waypoint CurrentWaypoint => currentWaypoint;
        public View CurrentView => currentView;
        public Direction? ActiveMoveDir => activeMoveDir;

        public System.Action OnEnteredWaypointOrView;

        private readonly Stack<View> history = new();

        private float dwellTimer;
        private float cooldownTimer;
        private EdgeDir? pendingEdge;

        private float targetYaw;
        private float targetPitch;

        private Vector3 waypointBaseRigPos;
        private Vector3 targetRigPos;
        private bool hasWaypointBaseRigPos;

        private void Awake() {
            if (player == null) player = GetComponent<PlayerEntity>();
            if (mover == null) mover = GetComponent<PlayerWaypointController>();
            if (inputController == null) inputController = GetComponent<PlayerInputController>();
        }

        private IEnumerator Start() {
            if (mover == null) {
                Debug.LogError("ViewController: PlayerWaypointController is missing.", this);
                enabled = false;
                yield break;
            }

            while (mover.CurrentWaypoint == null) {
                yield return null;
            }

            EnterWaypoint(mover.CurrentWaypoint, fromWaypoint: null);
        }

        private void Update() {
            if (mover == null || mover.rigTransform == null || mover.viewPivot == null) return;
            if (mover.IsMoving) return;

            SyncWaypointFromMover();
            UpdateEdgeSwitching();
            UpdateNodeMovementInput();
            UpdateViewRotation();
            UpdateRigOffset();
        }

        private void SyncWaypointFromMover() {
            if (mover.CurrentWaypoint != null && mover.CurrentWaypoint != currentWaypoint) {
                EnterWaypoint(mover.CurrentWaypoint, fromWaypoint: null);
            }
        }

        private void UpdateEdgeSwitching() {
            cooldownTimer -= Time.deltaTime;

            if (currentView == null) return;

            EdgeDir? edge = GetEdgeDir();

            if (!edge.HasValue) {
                pendingEdge = null;
                dwellTimer = 0f;
                return;
            }

            if (pendingEdge != edge.Value) {
                pendingEdge = edge.Value;
                dwellTimer = 0f;
            }

            dwellTimer += Time.deltaTime;

            if (dwellTimer >= dwellTime) {
                TryEdge(edge.Value);
                dwellTimer = 0f;
            }
        }

        private void UpdateNodeMovementInput() {
            if (inputController == null || mover == null) {
                activeMoveDir = null;
                return;
            }

            Vector2 move = inputController.Move;
            if (move.sqrMagnitude <= 0.25f) {
                activeMoveDir = null;
                return;
            }

            Direction dir = VectorToDir(move);
            TryMove(dir);
        }

        private void UpdateViewRotation() {
            if (mover.viewPivot == null) return;

            Quaternion desired = Quaternion.Euler(targetPitch, targetYaw, 0f);
            float k = 1f - Mathf.Exp(-rotateSpeed * Time.deltaTime);

            mover.viewPivot.rotation = Quaternion.Slerp(
                mover.viewPivot.rotation,
                desired,
                k
            );
        }

        public void EnterWaypoint(Waypoint waypoint, Waypoint fromWaypoint) {
            currentWaypoint = waypoint;

            history.Clear();
            pendingEdge = null;
            dwellTimer = 0f;
            cooldownTimer = 0f;
            activeMoveDir = null;

            if (waypoint != null && mover != null && mover.rigTransform != null) {
                waypointBaseRigPos = waypoint.transform.position;
                waypointBaseRigPos.y = mover.rigTransform.position.y;

                hasWaypointBaseRigPos = true;
                mover.rigTransform.position = waypointBaseRigPos;
                targetRigPos = waypointBaseRigPos;
            }
            else {
                hasWaypointBaseRigPos = false;
            }

            View startView = waypoint != null ? waypoint.GetEntryView(fromWaypoint) : null;
            SetView(startView, pushHistory: false, snap: snapOnEnter);
        }

        private void SetView(View view, bool pushHistory, bool snap) {
            if (view == null || mover == null || mover.viewPivot == null) return;

            if (pushHistory && currentView != null) {
                history.Push(currentView);
            }

            currentView = view;
            ApplyViewOffset(view);

            targetYaw = ComputeYawForView(view);
            targetPitch = view.pitchDegrees;

            if (snap) {
                mover.viewPivot.rotation = Quaternion.Euler(targetPitch, targetYaw, 0f);
            }

            OnEnteredWaypointOrView?.Invoke();
        }

        private float ComputeYawForView(View view) {
            Vector3 origin = hasWaypointBaseRigPos
                ? waypointBaseRigPos
                : (mover.rigTransform != null ? mover.rigTransform.position : mover.transform.position);

            if (view.lookTarget != null) {
                Vector3 toTarget = view.lookTarget.position - origin;
                toTarget.y = 0f;

                if (toTarget.sqrMagnitude > 0.0001f) {
                    return Quaternion.LookRotation(toTarget.normalized, Vector3.up).eulerAngles.y;
                }
            }

            Vector3 forward = view.transform.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude > 0.0001f) {
                return Quaternion.LookRotation(forward.normalized, Vector3.up).eulerAngles.y;
            }

            return mover.viewPivot.rotation.eulerAngles.y;
        }

        private void TryEdge(EdgeDir dir) {
            if (cooldownTimer > 0f || currentView == null) return;

            View.EdgeLink link = currentView.GetEdge(dir);
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

        private void TryMove(Direction dir) {
            if (mover == null || mover.IsMoving) return;
            if (mover.CurrentWaypoint == null) return;

            Waypoint waypoint = mover.CurrentWaypoint;
            WaypointTransition transition = null;

            if (currentView != null) {
                var overrideMove = currentView.GetOverride(dir);
                if (overrideMove.enabled && overrideMove.targetWaypoint != null) {
                    transition = waypoint.GetTransitionTo(overrideMove.targetWaypoint);
                }
            }

            if (transition == null) {
                transition = waypoint.GetTransition(dir);
            }

            if (transition == null || transition.target == null) return;

            activeMoveDir = dir;
            StartCoroutine(MoveAndApplyEntry(transition));
        }

        private IEnumerator MoveAndApplyEntry(WaypointTransition transition) {
            Waypoint fromWaypoint = mover.CurrentWaypoint;

            mover.BeginTransition(transition);

            while (mover.IsMoving) {
                yield return null;
            }

            EnterWaypoint(mover.CurrentWaypoint, fromWaypoint);
            activeMoveDir = null;
        }

        private EdgeDir? GetEdgeDir() {
            Vector2 mousePos = Mouse.current != null
                ? Mouse.current.position.ReadValue()
                : (Vector2)Input.mousePosition;

            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            bool left = mousePos.x <= edgePixels;
            bool right = mousePos.x >= screenWidth - edgePixels;
            bool down = mousePos.y <= edgePixels;
            bool up = mousePos.y >= screenHeight - edgePixels;

            if (preferHorizontalInCorners) {
                if (left && !right) return EdgeDir.Left;
                if (right && !left) return EdgeDir.Right;
                if (up && !down) return EdgeDir.Up;
                if (down && !up) return EdgeDir.Down;
            }
            else {
                if (up && !down) return EdgeDir.Up;
                if (down && !up) return EdgeDir.Down;
                if (left && !right) return EdgeDir.Left;
                if (right && !left) return EdgeDir.Right;
            }

            return null;
        }

        private void UpdateRigOffset() {
            if (!hasWaypointBaseRigPos || mover == null || mover.rigTransform == null) return;

            float k = 1f - Mathf.Exp(-offsetLerpSpeed * Time.deltaTime);
            mover.rigTransform.position = Vector3.Lerp(
                mover.rigTransform.position,
                targetRigPos,
                k
            );
        }

        private void ApplyViewOffset(View view) {
            if (!hasWaypointBaseRigPos || mover == null || mover.rigTransform == null || view == null) return;

            Vector3 forward = view.transform.forward;
            Vector3 right = view.transform.right;

            forward.y = 0f;
            right.y = 0f;

            if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;
            if (right.sqrMagnitude < 1e-6f) right = Vector3.right;

            forward.Normalize();
            right.Normalize();

            Vector3 local = view.rigLocalOffset;
            Vector3 worldOffset = right * local.x + Vector3.up * local.y + forward * local.z;

            targetRigPos = waypointBaseRigPos + worldOffset;
        }

        private static Direction VectorToDir(Vector2 move) {
            if (Mathf.Abs(move.x) > Mathf.Abs(move.y)) {
                return move.x > 0f ? Direction.D : Direction.A;
            }

            return move.y > 0f ? Direction.W : Direction.S;
        }
    }
}