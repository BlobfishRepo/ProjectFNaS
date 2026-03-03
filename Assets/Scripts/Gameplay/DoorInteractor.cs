using UnityEngine;
using UnityEngine.InputSystem;

public class DoorInteractor : MonoBehaviour {
    [Header("Raycast")]
    public Camera cam; // defaults to Camera.main
    public float maxDistance = 6f;
    public LayerMask mask = ~0;
    public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Input")]
    public bool useLeftClick = true;

    private PlayerInputActions input;
    private Door heldDoor;

    private void Awake() {
        input = new PlayerInputActions();
        if (cam == null) cam = Camera.main;
    }

    private void OnEnable() {
        // If you already have an input action for Interact, use that instead.
        // This assumes you have an "Interact" action; otherwise, see note below.
        input.Player.Enable();
        input.Player.Interact.started += OnHoldStarted;
        input.Player.Interact.canceled += OnHoldCanceled;
    }

    private void OnDisable() {
        ReleaseDoor();
        input.Player.Interact.started -= OnHoldStarted;
        input.Player.Interact.canceled -= OnHoldCanceled;
        input.Player.Disable();
    }

    private void OnApplicationFocus(bool hasFocus) {
        if (!hasFocus) ReleaseDoor();
    }

    private void OnHoldStarted(InputAction.CallbackContext ctx) {
        // Start holding whatever door is under cursor right now
        TryAcquireDoorUnderCursor();
    }

    private void OnHoldCanceled(InputAction.CallbackContext ctx) {
        ReleaseDoor();
    }

    private void TryAcquireDoorUnderCursor() {
        if (cam == null) return;

        Vector2 screenPos =
            Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition;

        Ray r = cam.ScreenPointToRay(screenPos);

        if (Physics.Raycast(r, out RaycastHit hit, maxDistance, mask, triggerInteraction)) {
            Door d = hit.collider.GetComponentInParent<Door>();
            if (d != null) {
                HoldDoor(d);
                return;
            }
        }

        // If click wasn't on a door, ensure nothing remains held
        ReleaseDoor();
    }

    private void HoldDoor(Door d) {
        if (heldDoor == d) return;

        // Close previous, open new
        ReleaseDoor();

        heldDoor = d;
        heldDoor.SetOpen(true);
    }

    private void ReleaseDoor() {
        if (heldDoor == null) return;
        heldDoor.SetOpen(false);
        heldDoor = null;
    }
}