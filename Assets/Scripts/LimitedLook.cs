using UnityEngine;
using UnityEngine.InputSystem;

public class LimitedLook : MonoBehaviour
{
    [Header("Sensitivity")]
    public float sensitivity = 1.0f;

    [Header("Limits (degrees)")]
    public float yawLimit = 15f;    //left/right
    public float pitchUpLimit = 10f; //look up
    public float pitchDownLimit = 10f; //look down

    [Header("Smoothing (optional)")]
    public float smooth = 12f;

    float yaw, pitch;
    float targetYaw, targetPitch;

    private PlayerInputActions inputActions;
    private Vector2 lookInput;

    void Awake() {
        inputActions = new PlayerInputActions();
    }

    void OnEnable() {
        inputActions.Enable();
        inputActions.Player.Look.performed += OnLook;
        inputActions.Player.Look.canceled += OnLook;
        Cursor.lockState = CursorLockMode.Locked;
    }

    void OnDisable() {
        inputActions.Player.Look.performed -= OnLook;
        inputActions.Player.Look.canceled -= OnLook;
        inputActions.Disable();
    }

    void OnLook(InputAction.CallbackContext context) {
        lookInput = context.ReadValue<Vector2>();
    }

    void Update() {
        float mx = lookInput.x * sensitivity;
        float my = lookInput.y * sensitivity;

        targetYaw += mx;
        targetPitch -= my;

        targetYaw = Mathf.Clamp(targetYaw, -yawLimit, yawLimit);
        targetPitch = Mathf.Clamp(targetPitch, -pitchDownLimit, pitchUpLimit);

        //smooth
        float t = 1f - Mathf.Exp(-smooth * Time.deltaTime);
        yaw = Mathf.Lerp(yaw, targetYaw, t);
        pitch = Mathf.Lerp(pitch, targetPitch, t);

        transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    static float NormalizeAngle(float a) {
        while (a > 180f) a -= 360f;
        while (a < -180f) a += 360f;
        return a;
    }
}
