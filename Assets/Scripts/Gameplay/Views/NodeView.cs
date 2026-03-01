using UnityEngine;

public enum EdgeDir { Left, Right, Up, Down }

public class NodeView : MonoBehaviour {
    [Header("Identity")]
    [Tooltip("Unique name within this node (Monitor, Paper, Window, Door, etc.)")]
    public string viewId = "View";

    [Header("Facing")]
    [Tooltip("If set, yaw faces this target. Otherwise yaw matches this transform rotation.")]
    public Transform lookTarget;

    [Header("Pitch (optional)")]
    [Tooltip("Pitch angle in degrees. Negative = look down, positive = look up.")]
    public float pitchDegrees = 0f;

    public enum LinkAction { None, GoToView, Back }

    [System.Serializable]
    public struct EdgeLink {
        public LinkAction action;
        public NodeView targetView; // used when action = GoToView
    }

    [Header("Framing (optional)")]
    [Tooltip("Local-space offset applied to the player's rig while in this view (e.g., lean in toward monitor/paper).")]
    public Vector3 rigLocalOffset = Vector3.zero;

    [Header("Edge transitions")]
    public EdgeLink left;
    public EdgeLink right;
    public EdgeLink up;
    public EdgeLink down;

    [System.Serializable]
    public struct MoveOverride {
        [Tooltip("If enabled, this key uses targetNode instead of the node's default key transition.")]
        public bool enabled;

        [Tooltip("Where this key should move from this view.")]
        public Node targetNode;

        [Tooltip("Optional: force a specific view upon arriving. Leave null to use entry rules/defaultView.")]
        public NodeView enterView;
    }

    [Header("WASD overrides (optional)")]
    public MoveOverride W;
    public MoveOverride A;
    public MoveOverride S;
    public MoveOverride D;

    public MoveOverride GetOverride(Direction dir) => dir switch {
        Direction.W => W,
        Direction.A => A,
        Direction.S => S,
        Direction.D => D,
        _ => default
    };

    public EdgeLink GetEdge(EdgeDir dir) => dir switch {
        EdgeDir.Left => left,
        EdgeDir.Right => right,
        EdgeDir.Up => up,
        EdgeDir.Down => down,
        _ => default
    };
}