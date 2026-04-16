using UnityEngine;

public enum EdgeDir { Left, Right, Up, Down }

public class View : MonoBehaviour {
    [Header("Identity")]
    [Tooltip("Unique name within this node (Monitor, Paper, Window, Door, etc.)")]
    public string viewId = "View";

    [Header("Facing Center")]
    [Tooltip("If set, yaw faces this target. Otherwise yaw can use explicitYaw, then this transform's forward.")]
    public Transform lookTarget;

    [Tooltip("If enabled and lookTarget is not set, use explicitYawDegrees for the horizontal center.")]
    public bool useExplicitYaw = false;

    [Tooltip("Explicit yaw center in degrees, used only when lookTarget is null and useExplicitYaw is enabled.")]
    public float explicitYawDegrees = 0f;

    [Header("Pitch Center")]
    [Tooltip("Pitch center in degrees. Negative = look down, positive = look up.")]
    public float pitchDegrees = 0f;

    [Header("Look Clamp")]
    [Tooltip("How many degrees left of center the camera may rotate.")]
    [Min(0f)] public float clampLeft = 0f;

    [Tooltip("How many degrees right of center the camera may rotate.")]
    [Min(0f)] public float clampRight = 0f;

    [Tooltip("How many degrees downward from center the camera may rotate.")]
    [Min(0f)] public float clampDown = 0f;

    [Tooltip("How many degrees upward from center the camera may rotate.")]
    [Min(0f)] public float clampUp = 0f;

    public enum LinkAction { None, GoToView, Back }

    [System.Serializable]
    public struct EdgeLink {
        public LinkAction action;
        public View targetView;
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
        [Tooltip("If enabled, this key uses targetWaypoint instead of the node's default key transition.")]
        public bool enabled;

        [Tooltip("Where this key should move from this view.")]
        public Waypoint targetWaypoint;

        [Tooltip("Optional: force a specific view upon arriving. Leave null to use angle-based selection.")]
        public View enterView;
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

    public float GetCenterYaw(Vector3 origin) {
        if (lookTarget != null) {
            Vector3 toTarget = lookTarget.position - origin;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude > 0.0001f) {
                return Quaternion.LookRotation(toTarget.normalized, Vector3.up).eulerAngles.y;
            }
        }

        if (useExplicitYaw) {
            return Normalize360(explicitYawDegrees);
        }

        Vector3 forward = transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude > 0.0001f) {
            return Quaternion.LookRotation(forward.normalized, Vector3.up).eulerAngles.y;
        }

        return 0f;
    }

    public float GetCenterPitch() {
        return pitchDegrees;
    }

    public float GetMinYaw(Vector3 origin) => Normalize180(GetCenterYaw(origin) - clampLeft);
    public float GetMaxYaw(Vector3 origin) => Normalize180(GetCenterYaw(origin) + clampRight);
    public float GetMinPitch() => GetCenterPitch() - clampDown;
    public float GetMaxPitch() => GetCenterPitch() + clampUp;

    public bool ContainsAngles(Vector3 origin, float yaw, float pitch) {
        float centerYaw = GetCenterYaw(origin);
        float centerPitch = GetCenterPitch();

        float yawOffset = Mathf.DeltaAngle(centerYaw, yaw);
        float pitchOffset = pitch - centerPitch;

        bool withinYaw = yawOffset >= -clampLeft && yawOffset <= clampRight;
        bool withinPitch = pitchOffset >= -clampDown && pitchOffset <= clampUp;

        return withinYaw && withinPitch;
    }

    public void ClampAngles(Vector3 origin, ref float yaw, ref float pitch) {
        float centerYaw = GetCenterYaw(origin);
        float centerPitch = GetCenterPitch();

        float yawOffset = Mathf.DeltaAngle(centerYaw, yaw);
        float pitchOffset = pitch - centerPitch;

        yawOffset = Mathf.Clamp(yawOffset, -clampLeft, clampRight);
        pitchOffset = Mathf.Clamp(pitchOffset, -clampDown, clampUp);

        yaw = Normalize360(centerYaw + yawOffset);
        pitch = centerPitch + pitchOffset;
    }

    public float GetCenterDistanceScore(Vector3 origin, float yaw, float pitch, float pitchWeight = 1f) {
        float yawDelta = Mathf.Abs(Mathf.DeltaAngle(GetCenterYaw(origin), yaw));
        float pitchDelta = Mathf.Abs(GetCenterPitch() - pitch);
        return yawDelta * yawDelta + (pitchDelta * pitchWeight) * (pitchDelta * pitchWeight);
    }

    public float GetClampDistanceScore(Vector3 origin, float yaw, float pitch, float pitchWeight = 1f) {
        float cy = yaw;
        float cp = pitch;
        ClampAngles(origin, ref cy, ref cp);

        float yawDelta = Mathf.Abs(Mathf.DeltaAngle(yaw, cy));
        float pitchDelta = Mathf.Abs(pitch - cp);
        return yawDelta * yawDelta + (pitchDelta * pitchWeight) * (pitchDelta * pitchWeight);
    }

    public bool IsAtDirectionalClamp(Vector3 origin, float yaw, float pitch, EdgeDir dir, float epsilon = 0.5f) {
        float centerYaw = GetCenterYaw(origin);
        float centerPitch = GetCenterPitch();

        float yawOffset = Mathf.DeltaAngle(centerYaw, yaw);
        float pitchOffset = pitch - centerPitch;

        return dir switch {
            EdgeDir.Left => yawOffset <= (-clampLeft + epsilon),
            EdgeDir.Right => yawOffset >= (clampRight - epsilon),
            EdgeDir.Down => pitchOffset <= (-clampDown + epsilon),
            EdgeDir.Up => pitchOffset >= (clampUp - epsilon),
            _ => false
        };
    }

    private static float Normalize360(float angle) {
        angle %= 360f;
        if (angle < 0f) angle += 360f;
        return angle;
    }

    private static float Normalize180(float angle) {
        angle = Normalize360(angle);
        if (angle > 180f) angle -= 360f;
        return angle;
    }

#if UNITY_EDITOR
    private void OnValidate() {
        clampLeft = Mathf.Max(0f, clampLeft);
        clampRight = Mathf.Max(0f, clampRight);
        clampDown = Mathf.Max(0f, clampDown);
        clampUp = Mathf.Max(0f, clampUp);
    }
#endif
}