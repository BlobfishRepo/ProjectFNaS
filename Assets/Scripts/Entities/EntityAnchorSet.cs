using UnityEngine;

namespace FNaS.MasterNodes {
    public class EntityAnchorSet : MonoBehaviour {

        [System.Serializable]
        public class AnchorSlot {
            public Transform anchor;
        }

        [System.Serializable]
        public class MimicAnchorSlot {
            [Tooltip("Where the Mimic should appear within this node.")]
            public Transform anchor;

            [Tooltip("Which Mimic model/object should be enabled for this anchor.")]
            public GameObject mimicVariant;
        }

        [Header("Anchors (local children)")]
        public AnchorSlot[] stalkerAnchors;
        public MimicAnchorSlot[] mimicAnchors;

        public AnchorSlot GetRandomStalkerAnchor() {
            return GetRandomFrom(stalkerAnchors);
        }

        public MimicAnchorSlot GetRandomMimicAnchor() {
            return GetRandomFrom(mimicAnchors);
        }

        private AnchorSlot GetRandomFrom(AnchorSlot[] slots) {
            if (slots == null || slots.Length == 0) return null;
            return slots[Random.Range(0, slots.Length)];
        }

        private MimicAnchorSlot GetRandomFrom(MimicAnchorSlot[] slots) {
            if (slots == null || slots.Length == 0) return null;
            return slots[Random.Range(0, slots.Length)];
        }
    }
}