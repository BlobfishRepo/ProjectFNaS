using UnityEngine;

namespace FNaS.Entities.LostGirl {
    public class LostGirlDoorBumpSensor : MonoBehaviour {
        public LostGirlMovement movement;

        private void Awake() {
            if (movement == null) {
                movement = GetComponentInParent<LostGirlMovement>();
            }
        }

        private void OnTriggerEnter(Collider other) {
            if (movement == null) return;

            Door door = other.GetComponentInParent<Door>();
            if (door == null) return;

            movement.NotifyDoorContact(door);
        }
    }
}