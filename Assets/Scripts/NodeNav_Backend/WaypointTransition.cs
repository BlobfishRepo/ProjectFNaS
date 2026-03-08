using UnityEngine;

public enum Direction { W, A, S, D }

public enum MoveFacingMode {
    KeepFacing,
    FaceMoveDirection,
    FaceLookTarget
}

public enum TransitionTag {
    Forward,
    Back,
    Left,
    Right,
    Other
}

[System.Serializable]
public class TransitionWaypoint {
    public Transform point;

    [Header("Optional turn near this point")]
    public Transform lookTarget;
    public float turnDuration = 0.12f;
}

[System.Serializable]
public class WaypointTransition {
    public Direction input;
    public Waypoint target;

    public TransitionTag tag = TransitionTag.Other;

    [Header("Waypoints (in order)")]
    public TransitionWaypoint[] waypoints;

    [Header("Facing during travel")]
    public MoveFacingMode facingMode = MoveFacingMode.KeepFacing;

    public Transform travelLookTarget;
    public float travelTurnDuration = 0.12f;

    [Header("Door (optional)")]
    public Door doorToUse;
    public float doorCloseDelay = 0.3f;
}