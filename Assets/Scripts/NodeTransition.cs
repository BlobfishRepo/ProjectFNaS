using UnityEngine;

public enum Direction { W, A, S, D }

[System.Serializable]
public class TransitionWaypoint {
    public Transform point;

    [Header("Optional turn at this point")]
    public Transform lookTarget;     // if set, we face this direction
    public float turnDuration = 0.12f; // 0 = snap instantly
    public bool turnInPlace = false; // if true, pause movement to rotate here
}

[System.Serializable]
public class NodeTransition {
    public Direction input;
    public Node target;

    [Header("Waypoints (in order)")]
    public TransitionWaypoint[] waypoints;

    [Header("Final facing override (optional)")]
    public Transform endLookTarget;
}