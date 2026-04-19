using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using FNaS.Entities.Stalker;
using FNaS.Systems;

namespace FNaS.Gameplay {
    public class ViewController : MonoBehaviour {
        [Header("References")]
        [SerializeField] private PlayerEntity player;
        [SerializeField] private PlayerWaypointController mover;
        [SerializeField] private PlayerInputController inputController;

        [Header("Look Input")]
        [SerializeField] private float mouseYawSensitivity = 0.12f;
        [SerializeField] private float mousePitchSensitivity = 0.12f;

        [Tooltip("If true, mouse look only works while holding left click.")]
        [SerializeField] private bool requireLeftClickToPan = true;

        [Header("Edge Detection")]
        [SerializeField] private float edgePixels = 60f;
        [SerializeField] private float switchCooldown = 0.20f;
        [SerializeField] private bool preferHorizontalInCorners = true;

        [Header("Edge Switching")]
        [SerializeField] private float edgeLockoutAfterViewChange = 0.50f;

        private float edgeLockoutTimer;

        [Header("Rotation Feel")]
        [SerializeField] private float rotateSpeed = 14f;
        [SerializeField] private bool snapOnEnter = true;

        [Header("Rig Offset")]
        [SerializeField] private float offsetLerpSpeed = 14f;

        [Header("Look Delay")]
        [SerializeField] private float verticalLookDelayOnViewEnter = 0.20f;

        [Header("Runtime (read-only)")]
        [SerializeField] private Waypoint currentWaypoint;
        [SerializeField] private View currentView;
        [SerializeField] private Direction? activeMoveDir;

        [SerializeField] private LoseState loseState;
        [SerializeField] private StalkerJumpscareController stalkerJumpscare;

        public Waypoint CurrentWaypoint => currentWaypoint;
        public View CurrentView => currentView;
        public Direction? ActiveMoveDir => activeMoveDir;

        // UI-facing helpers
        public bool RequiresLeftClickToPan => requireLeftClickToPan;
        public bool IsPanHeld =>
            Mouse.current != null && Mouse.current.leftButton.isPressed;

        public bool CanPanCurrentView {
            get {
                if (mover == null || mover.viewPivot == null) return false;
                if (currentView == null) return false;
                if (!currentView.HasPanRange) return false;
                if (mover.IsMoving) return false;
                if (loseState != null && loseState.hasLost) return false;
                if (stalkerJumpscare != null && stalkerJumpscare.IsPlaying) return false;
                return true;
            }
        }

        // True specifically when the player is in a view where left click does something useful.
        public bool ShouldShowPanIndicator => requireLeftClickToPan && CanPanCurrentView;

        public System.Action OnEnteredWaypointOrView;

        private readonly Stack<View> history = new();

        private float cooldownTimer;
        private float verticalLookDelayTimer;

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

            float incomingYaw = GetCurrentCameraYaw();
            float incomingPitch = GetCurrentCameraPitch();
            EnterWaypoint(mover.CurrentWaypoint, fromWaypoint: null, incomingYaw, incomingPitch);
        }

        private void Update() {
            if (mover == null || mover.rigTransform == null || mover.viewPivot == null) return;
            if (mover.IsMoving) return;
            if (loseState != null && loseState.hasLost) return;
            if (stalkerJumpscare != null && stalkerJumpscare.IsPlaying) return;

            SyncWaypointFromMover();
            UpdateLookInput();
            UpdateEdgeSwitching();
            UpdateNodeMovementInput();
            UpdateViewRotation();
            UpdateRigOffset();
        }

        private void SyncWaypointFromMover() {
            if (mover.CurrentWaypoint != null && mover.CurrentWaypoint != currentWaypoint) {
                float incomingYaw = GetCurrentCameraYaw();
                float incomingPitch = GetCurrentCameraPitch();
                EnterWaypoint(mover.CurrentWaypoint, fromWaypoint: null, incomingYaw, incomingPitch);
            }
        }

        private void UpdateLookInput() {
            if (currentView == null || inputController == null) return;

            if (verticalLookDelayTimer > 0f) {
                verticalLookDelayTimer -= Time.deltaTime;
            }

            bool canPan = !requireLeftClickToPan
                || (Mouse.current != null && Mouse.current.leftButton.isPressed);

            if (!canPan) return;

            Vector2 look = inputController.LookDelta;
            if (look.sqrMagnitude <= 0.000001f) return;

            targetYaw += look.x * mouseYawSensitivity;

            if (verticalLookDelayTimer <= 0f) {
                targetPitch -= look.y * mousePitchSensitivity;
            }

            ClampTargetAnglesToCurrentView();
        }

        private void UpdateEdgeSwitching() {
            cooldownTimer -= Time.deltaTime;

            if (cooldownTimer > 0f || currentView == null) {
                return;
            }

            EdgeDir? edge = GetEdgeDir();

            if (edgeLockoutTimer > 0f) {
                if (!edge.HasValue) {
                    edgeLockoutTimer = 0f;
                }
                else {
                    edgeLockoutTimer -= Time.deltaTime;
                    if (edgeLockoutTimer > 0f) {
                        return;
                    }
                }
            }

            if (!edge.HasValue) {
                return;
            }

            View.EdgeLink link = currentView.GetEdge(edge.Value);
            if (link.action == View.LinkAction.None) {
                return;
            }

            TryEdge(edge.Value);
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

        public void EnterWaypoint(Waypoint waypoint, Waypoint fromWaypoint, float incomingYaw, float incomingPitch) {
            currentWaypoint = waypoint;

            history.Clear();
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

            View startView = FindBestEntryView(waypoint, fromWaypoint, incomingYaw, incomingPitch);
            bool preserveAngles = startView != null && startView.ContainsAngles(GetViewOrigin(), incomingYaw, incomingPitch);

            targetYaw = incomingYaw;
            targetPitch = incomingPitch;

            SetView(startView, pushHistory: false, snap: snapOnEnter, preserveAngles: preserveAngles);
        }

        private void SetView(View view, bool pushHistory, bool snap, bool preserveAngles) {
            if (view == null || mover == null || mover.viewPivot == null) return;

            if (pushHistory && currentView != null) {
                history.Push(currentView);
            }

            currentView = view;
            verticalLookDelayTimer = verticalLookDelayOnViewEnter;
            edgeLockoutTimer = edgeLockoutAfterViewChange;

            ApplyViewOffset(view);

            Vector3 origin = GetViewOrigin();

            if (!preserveAngles) {
                targetYaw = view.GetCenterYaw(origin);
                targetPitch = view.GetCenterPitch();
            }

            view.ClampAngles(origin, ref targetYaw, ref targetPitch);

            if (snap) {
                mover.viewPivot.rotation = Quaternion.Euler(targetPitch, targetYaw, 0f);
            }

            OnEnteredWaypointOrView?.Invoke();
        }

        private View FindBestEntryView(Waypoint waypoint, Waypoint fromWaypoint, float incomingYaw, float incomingPitch) {
            if (waypoint == null) return null;

            if (waypoint.defaultView != null) {
                return waypoint.defaultView;
            }

            View ruleView = waypoint.GetEntryRuleView(fromWaypoint);
            if (ruleView != null) {
                return ruleView;
            }

            View[] views = waypoint.GetViews();
            Vector3 origin = GetViewOrigin();

            View bestContaining = null;
            float bestContainingScore = float.PositiveInfinity;

            View bestClamped = null;
            float bestClampedScore = float.PositiveInfinity;

            if (views != null) {
                for (int i = 0; i < views.Length; i++) {
                    View v = views[i];
                    if (v == null) continue;

                    bool contains = v.ContainsAngles(origin, incomingYaw, incomingPitch);
                    if (contains) {
                        float score = v.GetCenterDistanceScore(origin, incomingYaw, incomingPitch, pitchWeight: 1.35f);
                        if (score < bestContainingScore) {
                            bestContaining = v;
                            bestContainingScore = score;
                        }
                    }
                    else {
                        float score = v.GetClampDistanceScore(origin, incomingYaw, incomingPitch, pitchWeight: 1.35f);
                        if (score < bestClampedScore) {
                            bestClamped = v;
                            bestClampedScore = score;
                        }
                    }
                }
            }

            if (bestContaining != null) return bestContaining;
            if (bestClamped != null) return bestClamped;

            return waypoint.GetFallbackView(fromWaypoint);
        }

        private void ClampTargetAnglesToCurrentView() {
            if (currentView == null) return;
            Vector3 origin = GetViewOrigin();
            currentView.ClampAngles(origin, ref targetYaw, ref targetPitch);
        }

        private void TryEdge(EdgeDir dir) {
            if (cooldownTimer > 0f || currentView == null) return;

            View.EdgeLink link = currentView.GetEdge(dir);
            if (link.action == View.LinkAction.None) return;

            if (link.action == View.LinkAction.Back) {
                if (history.Count > 0) {
                    SetView(history.Pop(), pushHistory: false, snap: false, preserveAngles: false);
                    cooldownTimer = switchCooldown;
                }
                return;
            }

            if (link.action == View.LinkAction.GoToView && link.targetView != null) {
                SetView(link.targetView, pushHistory: true, snap: false, preserveAngles: false);
                cooldownTimer = switchCooldown;
            }
        }

        private void TryMove(Direction dir) {
            if (mover == null || mover.IsMoving) return;
            if (mover.CurrentWaypoint == null) return;

            Waypoint waypoint = mover.CurrentWaypoint;
            WaypointTransition transition = null;
            View forcedEnterView = null;

            if (currentView != null) {
                var overrideMove = currentView.GetOverride(dir);

                if (overrideMove.enabled) {
                    if (overrideMove.targetWaypoint == null) {
                        return;
                    }

                    transition = waypoint.GetTransitionTo(overrideMove.targetWaypoint);

                    if (transition != null) {
                        forcedEnterView = overrideMove.enterView;
                    }
                }
            }

            if (transition == null) {
                transition = waypoint.GetTransition(dir);
                forcedEnterView = null;
            }

            if (transition == null || transition.target == null) return;

            activeMoveDir = dir;
            StartCoroutine(MoveAndApplyEntry(transition, forcedEnterView));
        }

        private IEnumerator MoveAndApplyEntry(WaypointTransition transition, View forcedEnterView) {
            Waypoint fromWaypoint = mover.CurrentWaypoint;

            bool started = mover.BeginTransition(transition);
            if (!started) {
                activeMoveDir = null;
                yield break;
            }

            while (mover.IsMoving) {
                yield return null;
            }

            float incomingYaw = GetCurrentCameraYaw();
            float incomingPitch = GetCurrentCameraPitch();

            if (forcedEnterView != null) {
                currentWaypoint = mover.CurrentWaypoint;

                history.Clear();
                cooldownTimer = 0f;

                if (currentWaypoint != null && mover != null && mover.rigTransform != null) {
                    waypointBaseRigPos = currentWaypoint.transform.position;
                    waypointBaseRigPos.y = mover.rigTransform.position.y;

                    hasWaypointBaseRigPos = true;
                    mover.rigTransform.position = waypointBaseRigPos;
                    targetRigPos = waypointBaseRigPos;
                }
                else {
                    hasWaypointBaseRigPos = false;
                }

                targetYaw = incomingYaw;
                targetPitch = incomingPitch;

                bool preserveAngles = forcedEnterView.ContainsAngles(GetViewOrigin(), incomingYaw, incomingPitch);
                SetView(forcedEnterView, pushHistory: false, snap: snapOnEnter, preserveAngles: preserveAngles);
                activeMoveDir = null;
                yield break;
            }

            EnterWaypoint(mover.CurrentWaypoint, fromWaypoint, incomingYaw, incomingPitch);
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

        private Vector3 GetViewOrigin() {
            if (hasWaypointBaseRigPos) {
                return waypointBaseRigPos;
            }

            if (mover != null && mover.rigTransform != null) {
                return mover.rigTransform.position;
            }

            return transform.position;
        }

        private float GetCurrentCameraYaw() {
            if (mover != null && mover.viewPivot != null) {
                return mover.viewPivot.rotation.eulerAngles.y;
            }

            return transform.rotation.eulerAngles.y;
        }

        private float GetCurrentCameraPitch() {
            if (mover != null && mover.viewPivot != null) {
                return NormalizePitch(mover.viewPivot.rotation.eulerAngles.x);
            }

            return NormalizePitch(transform.rotation.eulerAngles.x);
        }

        private static float NormalizePitch(float eulerX) {
            if (eulerX > 180f) eulerX -= 360f;
            return eulerX;
        }

        private static Direction VectorToDir(Vector2 move) {
            if (Mathf.Abs(move.x) > Mathf.Abs(move.y)) {
                return move.x > 0f ? Direction.D : Direction.A;
            }

            return move.y > 0f ? Direction.W : Direction.S;
        }
    }
}