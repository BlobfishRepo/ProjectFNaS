using System.Collections.Generic;
using UnityEngine;

namespace FNaS.Entities.LostGirl {
    public class LostGirlSpawnGate : MonoBehaviour {
        [Tooltip("If null, uses LostGirlGlassAnchor on the same GameObject.")]
        public LostGirlGlassAnchor anchor;

        [Tooltip("If empty and allowAnyIfEmpty is true, this anchor can spawn for any player region.")]
        public List<LostGirlRegionId> allowedPlayerRegions = new();

        public bool allowAnyIfEmpty = true;

        public LostGirlGlassAnchor Anchor {
            get {
                if (anchor == null) anchor = GetComponent<LostGirlGlassAnchor>();
                return anchor;
            }
        }

        public bool Allows(LostGirlRegionId playerRegion) {
            if (allowedPlayerRegions == null || allowedPlayerRegions.Count == 0) {
                return allowAnyIfEmpty;
            }

            for (int i = 0; i < allowedPlayerRegions.Count; i++) {
                if (allowedPlayerRegions[i] == playerRegion) return true;
            }

            return false;
        }
    }
}