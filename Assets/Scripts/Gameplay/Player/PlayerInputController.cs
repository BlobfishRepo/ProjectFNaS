using UnityEngine;
using UnityEngine.InputSystem;

namespace FNaS.Gameplay {
    public class PlayerInputController : MonoBehaviour {
        [Header("Cursor")]
        [SerializeField] private bool lockCursorOnEnable = false;
        [SerializeField] private bool hideCursorWhenLocked = true;

        [Header("Runtime (read-only)")]
        [SerializeField] private Vector2 move;
        [SerializeField] private Vector2 lookDelta;

        private PlayerInputActions input;

        public Vector2 Move => move;
        public Vector2 LookDelta => lookDelta;

        private void Awake() {
            input = new PlayerInputActions();
        }

        private void OnEnable() {
            input.Player.Enable();

            if (lockCursorOnEnable) {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = !hideCursorWhenLocked;
            }
        }

        private void OnDisable() {
            input.Player.Disable();
        }

        private void Update() {
            move = input.Player.Move.ReadValue<Vector2>();

            lookDelta = Mouse.current != null
                ? Mouse.current.delta.ReadValue()
                : Vector2.zero;
        }

        public void SetCursorLocked(bool locked) {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}