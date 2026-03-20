using System.Collections.Generic;
using UnityEngine;
using FNaS.Entities;
using FNaS.MasterNodes;
using FNaS.Systems;

namespace FNaS.Entities.Stalker {
    public class StalkerNodeMovement : StalkerMovementBase {
        [Header("Path")]
        public LinearPathDefinition pathDef;

        [Header("Placement")]
        [Tooltip("Teleport to a per-node anchor (EntityAnchorSet) on node changes.")]
        public bool teleportToAnchors = true;

        [Tooltip("If null, uses this GameObject's transform.")]
        public Transform teleportTarget;

        [Tooltip("If no stalker anchors exist on the node, snap to the node transform.")]
        public bool fallbackToNodeTransform = true;

        [Header("Node Systems")]
        public BlockerRegistry blockerRegistry;

        [Header("Runtime")]
        [SerializeField] private int currentIndex = 0;

        private readonly List<MasterNode> resolvedPath = new();
        private MasterNode lastBlockedNode;
        private int lastTeleportedIndex = int.MinValue;
        private bool initialized;

        public override MasterNode CurrentMasterNode =>
            (currentIndex >= 0 && currentIndex < resolvedPath.Count) ? resolvedPath[currentIndex] : null;

        public override bool AtDoor =>
            resolvedPath.Count > 0 && currentIndex >= resolvedPath.Count - 1;

        public int CurrentIndex => currentIndex;
        public int PathCount => resolvedPath.Count;

        private void Awake() {
            if (teleportTarget == null) teleportTarget = transform;
        }

        public override void Initialize() {
            if (initialized) return;
            initialized = true;

            ResolvePath();

            if (resolvedPath.Count == 0) {
                Debug.LogError("StalkerNodeMovement: Resolved path is empty. Check pathDef + registry.", this);
                return;
            }

            RefreshOccupancy();
            SnapToCurrentNode(force: true);
        }

        public override void RefreshOccupancy() {
            if (blockerRegistry == null) return;

            var node = CurrentMasterNode;
            if (node == null) return;

            if (lastBlockedNode != null && lastBlockedNode != node) {
                blockerRegistry.SetBlockedForward(lastBlockedNode, false);
            }

            blockerRegistry.SetBlockedForward(node, true);
            lastBlockedNode = node;
        }

        public override void ClearOccupancy() {
            if (blockerRegistry != null && lastBlockedNode != null) {
                blockerRegistry.SetBlockedForward(lastBlockedNode, false);
            }
            lastBlockedNode = null;
        }

        public override bool TryAdvance(MasterNode playerNode, bool allowShareNodeWithPlayer) {
            if (resolvedPath.Count == 0) return false;

            int next = Mathf.Min(currentIndex + 1, resolvedPath.Count - 1);
            if (next == currentIndex) return false;

            if (!allowShareNodeWithPlayer && playerNode != null) {
                var nextNode = resolvedPath[next];
                if (nextNode == playerNode) return false;
            }

            currentIndex = next;
            OnChangedNode();
            return true;
        }

        public override bool PushBack(int steps) {
            if (resolvedPath.Count == 0) return false;

            int next = Mathf.Max(0, currentIndex - Mathf.Max(1, steps));
            if (next == currentIndex) return false;

            currentIndex = next;
            OnChangedNode();
            return true;
        }

        public override void ReappearInFirstNNodes(int firstNNodes) {
            if (resolvedPath.Count == 0) return;

            int max = Mathf.Clamp(firstNNodes, 1, Mathf.Max(1, resolvedPath.Count));
            currentIndex = Mathf.Clamp(Random.Range(0, max), 0, resolvedPath.Count - 1);

            lastTeleportedIndex = int.MinValue;
            SnapToCurrentNode(force: true);
            RefreshOccupancy();
        }

        public override void TickMovementVisuals() {
            SnapToCurrentNode();
        }

        private void SnapToCurrentNode(bool force = false) {
            if (!teleportToAnchors) return;
            if (!force && currentIndex == lastTeleportedIndex) return;

            lastTeleportedIndex = currentIndex;

            var node = CurrentMasterNode;
            if (node == null || teleportTarget == null) return;

            Transform anchor = null;
            var anchorSet = node.GetComponent<EntityAnchorSet>();
            if (anchorSet != null) {
                var slot = anchorSet.GetRandomStalkerAnchor();
                anchor = slot != null ? slot.anchor : null;
            }

            if (anchor != null) {
                teleportTarget.SetPositionAndRotation(anchor.position, anchor.rotation);
                return;
            }

            if (fallbackToNodeTransform) {
                teleportTarget.SetPositionAndRotation(node.transform.position, node.transform.rotation);
            }
        }

        private void ResolvePath() {
            resolvedPath.Clear();

            if (pathDef == null) {
                Debug.LogError("StalkerNodeMovement: pathDef not assigned.", this);
                return;
            }

            if (MasterNodeRegistry.Instance == null) {
                Debug.LogError("StalkerNodeMovement: No MasterNodeRegistry in scene.", this);
                return;
            }

            foreach (var guid in pathDef.nodeGuids) {
                var node = MasterNodeRegistry.Instance.GetOrNull(guid);
                if (node == null) {
                    Debug.LogError($"StalkerNodeMovement: Could not resolve MasterNode GUID '{guid}'.", this);
                    resolvedPath.Clear();
                    return;
                }
                resolvedPath.Add(node);
            }

            currentIndex = resolvedPath.Count == 0
                ? 0
                : Mathf.Clamp(currentIndex, 0, resolvedPath.Count - 1);
        }

        private void OnChangedNode() {
            RefreshOccupancy();
            SnapToCurrentNode(force: true);
        }

        private void OnDisable() {
            ClearOccupancy();
        }
    }
}