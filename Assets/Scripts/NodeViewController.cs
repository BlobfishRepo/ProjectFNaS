using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class NodeViewController : MonoBehaviour {
    [Header("References")]
    [SerializeField] private PlayerNodeController mover;

    [Header("Edge detection")]
    [SerializeField] private float edgePixels = 60f;
    [SerializeField] private float dwellTime = 0.08f;
    [SerializeField] private float switchCooldown = 0.20f;
    [SerializeField] private bool preferHorizontalInCorners = true;

    [Header("Rotation feel")]
    [SerializeField] private float rotateSpeed = 14f;
    [SerializeField] private bool snapOnEnter = true;

    [Header("Runtime (read-only)")]
    [SerializeField] private Node currentNode;
    [SerializeField] private NodeView currentView;
    public Node CurrentNode => currentNode;
    public NodeView CurrentView => currentView;
    public System.Action OnEnteredNodeOrView;
    public System.Action OnBeginMove;

    public Direction? ActiveMoveDir { get; private set; }

    private readonly Stack<NodeView> history = new();

    private PlayerInputActions input;
    private float dwellTimer;
    private float cooldownTimer;
    private EdgeDir? pendingEdge;
    private Quaternion targetYaw;
    private float targetPitch;

    private void Awake() {
        if (!mover) mover = FindFirstObjectByType<PlayerNodeController>();
        input = new PlayerInputActions();
    }

    private void OnEnable() {
        input.Enable();
    }

    private void OnDisable() {
        input.Disable();
    }

    private IEnumerator Start() {
        if (mover == null) {
            Debug.LogError("NodeViewController: mover not assigned.");
            enabled = false;
            yield break;
        }

        // Wait until PlayerNodeController has initialized CurrentNode
        while (mover.CurrentNode == null)
            yield return null;

        EnterNode(mover.CurrentNode, fromNode: null);
    }

    private void Update() {
        if (mover == null || mover.yawPivot == null) return;
        if (mover.IsMoving) return; // don't switch views mid-move

        // Ensure node stays synced
        if (mover.CurrentNode != null && mover.CurrentNode != currentNode)
            EnterNode(mover.CurrentNode, fromNode: null);

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

        // Keep pitch neutral (matches your node system)
        if (mover.pitchPivot != null) {
            Quaternion desired = Quaternion.Euler(targetPitch, 0f, 0f);
            mover.pitchPivot.localRotation = Quaternion.Slerp(
                mover.pitchPivot.localRotation,
                desired,
                1f - Mathf.Exp(-rotateSpeed * Time.deltaTime)
            );
        }
    }

    private bool _moveLatched = false;

    // ---------- Node / View control ----------

    public void EnterNode(Node node, Node fromNode) {
        currentNode = node;
        history.Clear();
        pendingEdge = null;
        dwellTimer = 0f;
        cooldownTimer = 0f;

        NodeView start = node != null ? node.GetEntryView(fromNode) : null;
        SetView(start, pushHistory: false, snap: snapOnEnter);
    }

    private void SetView(NodeView view, bool pushHistory, bool snap) {
        if (view == null || mover == null || mover.yawPivot == null) return;

        if (pushHistory && currentView != null)
            history.Push(currentView);

        currentView = view;
        targetYaw = ComputeYawForView(view);
        targetPitch = view.pitchDegrees;

        if (snap)
            mover.yawPivot.rotation = targetYaw;
        
        OnEnteredNodeOrView?.Invoke();
    }

    private Quaternion ComputeYawForView(NodeView view) {
        Vector3 pos = mover.rigTransform != null ? mover.rigTransform.position : mover.transform.position;

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
        Debug.Log($"Edge: {dir}, view: {currentView.name}");
        if (cooldownTimer > 0f || currentView == null) return;

        var link = currentView.GetEdge(dir);
        if (link.action == NodeView.LinkAction.None) return;

        if (link.action == NodeView.LinkAction.Back) {
            if (history.Count > 0) {
                SetView(history.Pop(), pushHistory: false, snap: false);
                cooldownTimer = switchCooldown;
            }
            return;
        }

        if (link.action == NodeView.LinkAction.GoToView && link.targetView != null) {
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
        if (mover.CurrentNode == null) return;

        currentNode = mover.CurrentNode;

        NodeTransition tr = null;

        // View override: map key -> target node -> node transition
        if (currentView != null) {
            var ov = currentView.GetOverride(dir);
            if (ov.enabled && ov.targetNode != null) {
                tr = currentNode.GetTransitionTo(ov.targetNode);
                // If you want to allow "override exists but no transition", you can Debug.Log here.
            }
        }

        // Fallback to node mapping
        if (tr == null)
            tr = currentNode.GetTransition(dir);

        if (tr == null || tr.target == null) return;

        ActiveMoveDir = dir;
        StartCoroutine(MoveAndApplyEntry(tr));
    }

    private IEnumerator MoveAndApplyEntry(NodeTransition tr) {
        Node from = mover.CurrentNode;

        OnBeginMove?.Invoke();
        mover.BeginTransition(tr);

        while (mover.IsMoving)
            yield return null;

        // mover.CurrentNode is now updated
        EnterNode(mover.CurrentNode, from);
        ActiveMoveDir = null;
    }

    private static Direction VectorToDir(Vector2 v) {
        if (Mathf.Abs(v.x) > Mathf.Abs(v.y)) return v.x > 0 ? Direction.D : Direction.A;
        return v.y > 0 ? Direction.W : Direction.S;
    }
}