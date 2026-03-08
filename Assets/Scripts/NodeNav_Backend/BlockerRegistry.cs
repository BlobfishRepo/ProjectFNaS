using System.Collections.Generic;
using UnityEngine;
using FNaS.MasterNodes;

namespace FNaS.Systems {
    [CreateAssetMenu(menuName = "FNaS/Systems/Blocker Registry", fileName = "BlockerRegistry")]
    public class BlockerRegistry : ScriptableObject {
        private readonly HashSet<MasterNode> blockedForwardAt = new HashSet<MasterNode>();

        public void SetBlockedForward(MasterNode node, bool blocked) {
            if (node == null) return;
            if (blocked) blockedForwardAt.Add(node);
            else blockedForwardAt.Remove(node);
        }

        public bool IsForwardExitBlockedAt(MasterNode node) {
            if (node == null) return false;
            return blockedForwardAt.Contains(node);
        }

        public void ClearAll() {
            blockedForwardAt.Clear();
        }
    }
}