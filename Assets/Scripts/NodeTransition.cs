using UnityEngine;

public enum Direction { W, A, S, D }

public enum MoveFacingMode {
    KeepFacing,
    FaceMoveDirection,
    FaceLookTarget
}

[System.Serializable]
public class TransitionWaypoint {
    public Transform point;

    [Header("Optional turn near this point")]
    public Transform lookTarget;        // if set, we face this direction when approaching
    public float turnDuration = 0.12f;  // 0 = snap instantly
}

[System.Serializable]
public class NodeTransition {
    public Direction input;
    public Node target;

    [Header("Waypoints (in order)")]
    public TransitionWaypoint[] waypoints;

    [Header("Facing during travel")]
    public MoveFacingMode facingMode = MoveFacingMode.KeepFacing;

    [Tooltip("Used when Facing Mode = FaceLookTarget")]
    public Transform travelLookTarget;

    [Tooltip("How quickly to turn while traveling. 0 = snap instantly.")]
    public float travelTurnDuration = 0.12f;
}