using UnityEngine;

public class Node : MonoBehaviour {
    [Header("Look")]
    public Transform lookTarget;

    [Header("Neighbors (WASD)")]
    public Node w;
    public Node a;
    public Node s;
    public Node d;

    public Node GetNeighbor(Direction dir) {
        return dir switch {
            Direction.W => w,
            Direction.A => a,
            Direction.S => s,
            Direction.D => d,
            _ => null
        };
    }

#if UNITY_EDITOR
    private void OnDrawGizmos() {
        Vector3 pos = transform.position;
        Vector3 forward = (lookTarget.position - pos).normalized;
        Gizmos.color = Color.green;
        float length = 0.8f;


        // Gizmos Marker
        Gizmos.DrawSphere(pos, 0.15f);
        Vector3 tip = pos + forward * length;
        Gizmos.DrawLine(pos, tip);

        Vector3 right = Vector3.Cross(forward, Vector2.up).normalized;
        float headSize = 0.25f;

        Gizmos.DrawLine(tip, tip - forward * headSize + right * headSize * 0.5f);
        Gizmos.DrawLine(tip, tip - forward * headSize - right * headSize * 0.5f);
    }

    private void OnDrawGizmosSelected() {
        // Neighbor Connections
        Vector3 pos = transform.position;
        if (w != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(pos, w.transform.position);
        }

        if (a != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(pos, a.transform.position);
        }

        if (s != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(pos, s.transform.position);
        }

        if (d != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(pos, d.transform.position);
        }
    }
#endif
}

public enum Direction { W, A, S, D }