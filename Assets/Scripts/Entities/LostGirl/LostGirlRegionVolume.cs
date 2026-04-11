using UnityEngine;

namespace FNaS.Entities.LostGirl {
    [RequireComponent(typeof(Collider))]
    public class LostGirlRegionVolume : MonoBehaviour {
        public LostGirlRegionId region = LostGirlRegionId.None;

        private Collider cachedCollider;

        public LostGirlRegionId Region => region;

        private void Awake() {
            cachedCollider = GetComponent<Collider>();
            if (cachedCollider != null) {
                cachedCollider.isTrigger = true;
            }
        }

        public bool Contains(Vector3 worldPoint) {
            if (cachedCollider == null) return false;

            Vector3 closest = cachedCollider.ClosestPoint(worldPoint);
            return (closest - worldPoint).sqrMagnitude <= 0.0001f;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected() {
            Gizmos.color = Color.magenta;
            Gizmos.matrix = transform.localToWorldMatrix;

            if (TryGetComponent<BoxCollider>(out var box)) {
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (TryGetComponent<SphereCollider>(out var sphere)) {
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            }
        }
#endif
    }
}