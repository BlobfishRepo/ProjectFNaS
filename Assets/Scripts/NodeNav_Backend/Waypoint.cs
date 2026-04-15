#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using FNaS.MasterNodes;

public class Waypoint : MonoBehaviour {

    [Header("Master Node Link")]
    public MasterNode masterNode;

    [Header("Views")]
    public View defaultView;

    [Header("Directed Transitions (per key)")]
    public WaypointTransition[] transitions;

    public WaypointTransition GetTransition(Direction dir) {
        if (transitions == null) return null;
        for (int i = 0; i < transitions.Length; i++) {
            if (transitions[i] != null && transitions[i].input == dir)
                return transitions[i];
        }
        return null;
    }

    public WaypointTransition GetTransitionTo(Waypoint target) {
        if (transitions == null || target == null) return null;
        for (int i = 0; i < transitions.Length; i++) {
            var tr = transitions[i];
            if (tr != null && tr.target == target)
                return tr;
        }
        return null;
    }

    [Header("Entry View Rules (legacy fallback / optional special-cases)")]
    public EntryRule[] entryRules;

    [System.Serializable]
    public class EntryRule {
        public Waypoint fromWaypoint;
        public View startView;
    }

    public View[] GetViews() {
        return GetComponentsInChildren<View>(includeInactive: false);
    }

    public View GetEntryRuleView(Waypoint fromWaypoint) {
        if (fromWaypoint != null && entryRules != null) {
            for (int i = 0; i < entryRules.Length; i++) {
                var r = entryRules[i];
                if (r != null && r.fromWaypoint == fromWaypoint)
                    return r.startView;
            }
        }

        return null;
    }

    public View GetFallbackView(Waypoint fromWaypoint) {
        View byRule = GetEntryRuleView(fromWaypoint);
        if (byRule != null) return byRule;

        return defaultView != null
            ? defaultView
            : GetComponentInChildren<View>();
    }

#if UNITY_EDITOR
    private void OnDrawGizmos() {
        Vector3 pos = transform.position;
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(pos, 0.15f);
    }

    private void OnDrawGizmosSelected() {
        if (transitions == null) return;

        foreach (var tr in transitions) {
            if (tr == null || tr.target == null) continue;

            Gizmos.color = tr.input switch {
                Direction.W => Color.blue,
                Direction.A => Color.red,
                Direction.S => Color.yellow,
                Direction.D => Color.cyan,
                _ => Color.white
            };

            Vector3 prev = transform.position;

            if (tr.waypoints != null) {
                for (int i = 0; i < tr.waypoints.Length; i++) {
                    var wp = tr.waypoints[i];
                    if (wp == null || wp.point == null) continue;

                    Vector3 p = wp.point.position;
                    Gizmos.DrawLine(prev, p);
                    Gizmos.DrawSphere(p, 0.08f);
                    prev = p;
                }
            }

            Gizmos.DrawLine(prev, tr.target.transform.position);
        }
    }
#endif
}