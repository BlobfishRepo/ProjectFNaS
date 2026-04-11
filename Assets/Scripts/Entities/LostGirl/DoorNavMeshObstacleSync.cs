using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshObstacle))]
public class DoorNavMeshObstacleSync : MonoBehaviour {
    public Door door;
    public NavMeshObstacle obstacle;

    [Tooltip("If true, the obstacle blocks the doorway whenever the door is closed.")]
    public bool blockWhenClosed = true;

    private void Awake() {
        if (door == null) {
            door = GetComponentInParent<Door>();
        }

        if (obstacle == null) {
            obstacle = GetComponent<NavMeshObstacle>();
        }

        if (obstacle != null) {
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;
        }

        Refresh();
    }

    private void Update() {
        Refresh();
    }

    private void Refresh() {
        if (door == null || obstacle == null) return;

        bool shouldBlock = blockWhenClosed && !door.isOpen;
        if (obstacle.enabled != shouldBlock) {
            obstacle.enabled = shouldBlock;
        }
    }
}