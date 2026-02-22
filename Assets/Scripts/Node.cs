#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public class Node : MonoBehaviour {
    [Header("Look")]
    public Transform lookTarget;

    [Header("Directed transitions (per key)")]
    public NodeTransition[] transitions;

    public NodeTransition GetTransition(Direction dir) {
        if (transitions == null) return null;
        for (int i = 0; i < transitions.Length; i++) {
            if (transitions[i] != null && transitions[i].input == dir) return transitions[i];
        }
        return null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos() {
        // Always-visible marker
        Vector3 pos = transform.position;
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(pos, 0.15f);

        if (lookTarget != null) {
            Vector3 forward = (lookTarget.position - pos);
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f) {
                forward.Normalize();
                Gizmos.DrawLine(pos, pos + forward * 0.8f);
            }
        }
    }

    private void OnDrawGizmosSelected() {
        if (transitions == null) return;

        foreach (var tr in transitions) {
            if (tr == null || tr.target == null) continue;

            // Color per key (optional)
            Gizmos.color = tr.input switch {
                Direction.W => Color.blue,
                Direction.A => Color.red,
                Direction.S => Color.yellow,
                Direction.D => Color.cyan,
                _ => Color.white
            };

            // Draw polyline: this node -> waypoints -> target node
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