using UnityEngine;

namespace FNaS.MasterNodes {
    public class EntityAnchorSet : MonoBehaviour {

        [System.Serializable]
        public class StalkerAnchorSlot {
            public Transform anchor;
            public View requiredView;   // null = any view
            public Door doorBlocker;    // null = no door blocker
        }

        [Header("Anchors (local children)")]
        public StalkerAnchorSlot[] stalkerAnchors;

        public StalkerAnchorSlot GetRandomStalkerAnchor() {
            if (stalkerAnchors == null || stalkerAnchors.Length == 0) return null;
            return stalkerAnchors[Random.Range(0, stalkerAnchors.Length)];
        }
    }
}