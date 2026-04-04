using UnityEngine;

namespace FNaS.Gameplay {
    public class PlayerSlowZoneTracker : MonoBehaviour {
        [Range(0.1f, 1f)]
        public float slowMultiplier = 0.5f;

        private int zonesInside = 0;

        public float Multiplier => zonesInside > 0 ? slowMultiplier : 1f;

        private void OnTriggerEnter(Collider other) {
            if (!other.enabled) return;

            if (other.CompareTag("MoldSlowZone")) {
                zonesInside++;
            }
        }

        private void OnTriggerExit(Collider other) {
            if (!other.enabled) return;

            if (other.CompareTag("MoldSlowZone")) {
                zonesInside = Mathf.Max(0, zonesInside - 1);
            }
        }
    }
}