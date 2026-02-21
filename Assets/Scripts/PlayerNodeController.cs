using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerNodeController : MonoBehaviour {
    [Header("References")]
    public Transform cameraTransform;
    public Node startingNode;

    [Header("State (read-only)")]
    public Node CurrentNode;

    private PlayerInputActions input;
    private Vector2 lastMove;

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

        SetCurrentNode(startingNode);
    }

    private void Update() {
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
        Node next = CurrentNode.GetNeighbor(dir);
        if (next == null) return;

        SetCurrentNode(next);
    }

    private void SetCurrentNode(Node node) {
        CurrentNode = node;

        if (cameraTransform != null) {
            cameraTransform.position = node.transform.position;

            if (node.lookTarget != null) {
                Vector3 forward = (node.lookTarget.position - cameraTransform.position).normalized;
                if (forward.sqrMagnitude > 0.0001f)
                    cameraTransform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }
        }
    }

    private static Direction VectorToDir(Vector2 v) {
        if (Mathf.Abs(v.x) > Mathf.Abs(v.y))
            return v.x > 0 ? Direction.D : Direction.A;
        else
            return v.y > 0 ? Direction.W : Direction.S;
    }
}
