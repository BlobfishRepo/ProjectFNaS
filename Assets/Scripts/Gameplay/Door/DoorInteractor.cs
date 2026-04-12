using FNaS.Gameplay;
using FNaS.Settings;
using FNaS.Systems;
using UnityEngine;
using UnityEngine.InputSystem;

public class DoorInteractor : MonoBehaviour, IRuntimeSettingsConsumer {
    [Header("Raycast")]
    public Camera cam;
    public float maxDistance = 6f;
    public LayerMask mask = ~0;
    public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Input")]
    public bool useLeftClick = true;

    [Header("References")]
    public PlayerWaypointController waypointMover;

    private PlayerInputActions input;
    private Door heldDoor;

    private void Awake() {
        input = new PlayerInputActions();

        if (cam == null) cam = Camera.main;

        if (waypointMover == null) {
            waypointMover = GetComponentInParent<PlayerWaypointController>();
        }
    }

    public void ApplyRuntimeSettings(RuntimeGameSettings settings) {
        if (settings == null) return;
        maxDistance = settings.GetFloat("door.maxDistance");
    }

    private void OnEnable() {
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

    private void Update() {
        if (IsPlayerMoving()) {
            ReleaseDoor();
        }
    }

    private void OnApplicationFocus(bool hasFocus) {
        if (!hasFocus) ReleaseDoor();
    }

    private void OnHoldStarted(InputAction.CallbackContext ctx) {
        if (IsPlayerMoving()) {
            ReleaseDoor();
            return;
        }

        TryAcquireDoorUnderCursor();
    }

    private void OnHoldCanceled(InputAction.CallbackContext ctx) {
        ReleaseDoor();
    }

    private void TryAcquireDoorUnderCursor() {
        if (cam == null) return;

        if (IsPlayerMoving()) {
            ReleaseDoor();
            return;
        }

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

        ReleaseDoor();
    }

    private void HoldDoor(Door d) {
        if (d == null) {
            ReleaseDoor();
            return;
        }

        if (IsPlayerMoving()) {
            ReleaseDoor();
            return;
        }

        if (heldDoor == d) return;

        ReleaseDoor();

        heldDoor = d;
        heldDoor.SetManualHeld(true);
    }

    private void ReleaseDoor() {
        if (heldDoor == null) return;
        heldDoor.SetManualHeld(false);
        heldDoor = null;
    }

    private bool IsPlayerMoving() {
        return waypointMover != null && waypointMover.IsMoving;
    }
}