using UnityEngine;

namespace FNaS.MasterNodes {
    public class EntityAnchorSet : MonoBehaviour {

        [System.Serializable]
        public class StalkerAnchorSlot {
            public Transform anchor;
        }

        [Header("Anchors (local children)")]
        public StalkerAnchorSlot[] stalkerAnchors;

        public StalkerAnchorSlot GetRandomStalkerAnchor() {
            if (stalkerAnchors == null || stalkerAnchors.Length == 0) return null;
            return stalkerAnchors[Random.Range(0, stalkerAnchors.Length)];
        }
    }
}