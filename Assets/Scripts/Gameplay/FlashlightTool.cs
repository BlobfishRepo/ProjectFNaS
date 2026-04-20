using UnityEngine;
using UnityEngine.InputSystem;
using FNaS.Settings;
using UnityEngine.UI;

namespace FNaS.Systems {
    public class FlashlightTool : MonoBehaviour, IRuntimeSettingsConsumer {
        [Header("Battery")]
        public float maxBatterySeconds = 30f;

        [Header("Indicator (optional)")]
        public Light flashlightLight;
        public GameObject indicatorObject;

        [Header("Battery UI")]
        public Image batteryImage;

        public Sprite spriteFull;
        public Sprite spriteMedium;
        public Sprite spriteLow;
        public Sprite spriteEmpty;

        [Header("Audio")]
        public AudioSource uiSource;
        public AudioClip lightSwitchClip;

        [Header("Low Battery Flicker")]
        [Range(0f, 1f)] public float flickerStartPercent = 0.25f;
        public float flickerMinHz = 2f;
        public float flickerMaxHz = 18f;
        [Range(0f, 1f)] public float flickerOffChance = 0.35f;
        public float flickerMinOffSeconds = 0.03f;
        public float flickerMaxOffSeconds = 0.10f;

        [Header("Aiming")]
        [Tooltip("Transform that rotates to aim the beam. Usually Flashlight_Light.")]
        public Transform beamOrigin;

        public enum AimMode { FacePlayerForward, CursorRay }
        public AimMode aimMode = AimMode.FacePlayerForward;

        [Tooltip("Camera used for cursor ray aiming. Defaults to Camera.main.")]
        public Camera aimCamera;

        [Tooltip("Max distance for cursor ray aim. This is only for aiming direction.")]
        public float cursorAimMaxDistance = 100f;

        [Tooltip("If true, beamOrigin smoothly rotates toward target direction.")]
        public bool smoothAim = true;

        [Tooltip("Degrees/sec for smooth aim.")]
        public float aimRotateSpeed = 720f;

        [Header("Cone Check Fallbacks")]
        [Tooltip("Used only if flashlightLight is null or not a Spot light.")]
        public float fallbackRange = 10f;

        [Header("Occlusion (Walls / Blocking)")]
        [Tooltip("Layers that can block the flashlight beam (e.g., Walls, Doors, Props).")]
        public LayerMask occlusionMask = ~0;

        [Tooltip("Include trigger colliders as blockers? Usually Ignore.")]
        public QueryTriggerInteraction occlusionTriggerInteraction = QueryTriggerInteraction.Ignore;

        [Tooltip("If true, requires a clear ray to the target (no wall blocking).")]
        public bool useOcclusion = true;

        [Tooltip("Used only if flashlightLight is null or not a Spot light.")]
        [Range(1f, 180f)] public float fallbackSpotAngle = 35f;

        [Header("Game State")]
        public LoseState loseState;
        public WinState winState;

        public bool isOn { get; private set; }

        private float batteryRemaining;
        private PlayerInputActions input;

        private float flickerTimer;
        private float offTimer;

        public void ApplyRuntimeSettings(RuntimeGameSettings settings) {
            if (settings == null) return;
            ApplyMaxBatterySeconds(settings.GetFloat("flashlight.maxBatterySeconds"));
        }

        private void Awake() {
            input = new PlayerInputActions();

            if (loseState == null) {
                loseState = FindFirstObjectByType<LoseState>();
            }

            if (winState == null) {
                winState = FindFirstObjectByType<WinState>();
            }
        }

        private void OnEnable() {
            input.Player.Enable();
            input.Player.Flashlight.performed += OnFlashlightPressed;
        }

        private void OnDisable() {
            input.Player.Flashlight.performed -= OnFlashlightPressed;
            input.Player.Disable();
            ForceOff();
        }

        private void Start() {
            batteryRemaining = maxBatterySeconds;

            if (beamOrigin == null) {
                if (flashlightLight != null) beamOrigin = flashlightLight.transform;
                else beamOrigin = transform;
            }

            if (aimCamera == null) aimCamera = Camera.main;

            ApplyIndicator(forceOff: false);
        }

        private void Update() {
            if (ShouldBlockInput()) {
                ForceOff();
                return;
            }

            if (!isOn) {
                offTimer = 0f;
                flickerTimer = 0f;
                return;
            }

            AimBeam();
            DrainBattery();
            UpdateBatteryUI();
            UpdateLowBatteryFlicker();
        }

        private void OnFlashlightPressed(InputAction.CallbackContext ctx) {
            if (ShouldBlockInput()) {
                ForceOff();
                return;
            }

            Toggle();
        }

        public void Toggle() {
            if (ShouldBlockInput()) {
                ForceOff();
                return;
            }

            if (batteryRemaining <= 0f) return;

            if (uiSource != null && lightSwitchClip != null) {
                uiSource.PlayOneShot(lightSwitchClip);
            }

            isOn = !isOn;
            ApplyIndicator(forceOff: false);
        }

        public void ForceOff() {
            isOn = false;
            offTimer = 0f;
            flickerTimer = 0f;
            ApplyIndicator(forceOff: false);
        }

        private void DrainBattery() {
            batteryRemaining -= Time.deltaTime;
            if (batteryRemaining <= 0f) {
                batteryRemaining = 0f;
                isOn = false;
                ApplyIndicator(forceOff: false);
            }
        }

        private void ApplyIndicator(bool forceOff) {
            bool on = isOn && !forceOff;

            if (flashlightLight != null)
                flashlightLight.enabled = on;

            if (indicatorObject != null)
                indicatorObject.SetActive(true); // always visible (important change)

            UpdateBatteryUI();
        }

        private void UpdateBatteryUI() {
            if (batteryImage == null) return;

            float pct = (maxBatterySeconds > 0f)
                ? batteryRemaining / maxBatterySeconds
                : 0f;

            Sprite target;

            if (pct <= 0f) {
                target = spriteEmpty;
            }
            else if (pct < 0.20f) {
                target = spriteLow;
            }
            else if (pct < 0.65f) {
                target = spriteMedium;
            }
            else {
                target = spriteFull;
            }

            if (batteryImage.sprite != target) {
                batteryImage.sprite = target;
            }
        }

        private void AimBeam() {
            if (beamOrigin == null) return;

            Vector3 desiredForward;

            if (aimMode == AimMode.CursorRay && aimCamera != null) {
                Vector2 mouse = Mouse.current != null
                    ? Mouse.current.position.ReadValue()
                    : (Vector2)Input.mousePosition;

                Ray ray = aimCamera.ScreenPointToRay(mouse);
                Vector3 targetPoint = ray.origin + ray.direction * Mathf.Max(0.01f, cursorAimMaxDistance);

                Vector3 dir = targetPoint - beamOrigin.position;
                if (dir.sqrMagnitude < 0.0001f) return;

                desiredForward = dir.normalized;
            }
            else {
                desiredForward = (aimCamera != null) ? aimCamera.transform.forward : beamOrigin.forward;
            }

            Quaternion desiredRot = Quaternion.LookRotation(desiredForward, Vector3.up);

            if (!smoothAim) {
                beamOrigin.rotation = desiredRot;
            }
            else {
                beamOrigin.rotation = Quaternion.RotateTowards(
                    beamOrigin.rotation,
                    desiredRot,
                    aimRotateSpeed * Time.deltaTime
                );
            }
        }

        private void UpdateLowBatteryFlicker() {
            if (maxBatterySeconds <= 0f) return;

            float pct = batteryRemaining / maxBatterySeconds;
            if (pct > flickerStartPercent) {
                offTimer = 0f;
                ApplyIndicator(forceOff: false);
                return;
            }

            float t = Mathf.InverseLerp(flickerStartPercent, 0f, pct);

            float hz = Mathf.Lerp(flickerMinHz, flickerMaxHz, t);
            flickerTimer += Time.deltaTime;

            if (offTimer > 0f) {
                offTimer -= Time.deltaTime;
                ApplyIndicator(forceOff: true);
                return;
            }

            ApplyIndicator(forceOff: false);

            float period = 1f / Mathf.Max(0.01f, hz);
            if (flickerTimer >= period) {
                flickerTimer = 0f;

                float chance = Mathf.Lerp(0.10f, flickerOffChance, t);
                if (Random.value < chance) {
                    offTimer = Random.Range(flickerMinOffSeconds, flickerMaxOffSeconds);
                }
            }
        }

        public bool IsIlluminating(Transform targetRoot) {
            if (!isOn) return false;
            if (batteryRemaining <= 0f) return false;
            if (targetRoot == null) return false;
            if (ShouldBlockInput()) return false;

            Transform originT = beamOrigin != null ? beamOrigin
                : (flashlightLight != null ? flashlightLight.transform : transform);

            Vector3 origin = originT.position;
            Vector3 forward = originT.forward;

            if (aimMode == AimMode.CursorRay) {
                Camera cam = aimCamera != null ? aimCamera : Camera.main;
                if (cam != null) {
                    Vector2 mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition;
                    Ray r = cam.ScreenPointToRay(mouse);

                    if (Physics.Raycast(r, out RaycastHit hit, cursorAimMaxDistance, occlusionMask, occlusionTriggerInteraction)) {
                        Vector3 toHit = hit.point - origin;
                        toHit.y = 0f;
                        if (toHit.sqrMagnitude > 0.0001f) forward = toHit.normalized;
                    }
                    else {
                        Vector3 dir = r.direction;
                        dir.y = 0f;
                        if (dir.sqrMagnitude > 0.0001f) forward = dir.normalized;
                    }
                }
            }

            Vector3 targetPoint = targetRoot.position;
            Collider targetCol = targetRoot.GetComponentInChildren<Collider>();
            if (targetCol != null) targetPoint = targetCol.bounds.center;

            Vector3 toTarget = targetPoint - origin;
            float dist = toTarget.magnitude;
            if (dist < 0.0001f) return true;

            float maxDist = fallbackRange;
            float halfAngle = fallbackSpotAngle * 0.5f;

            if (flashlightLight != null && flashlightLight.type == LightType.Spot) {
                maxDist = flashlightLight.range;
                halfAngle = flashlightLight.spotAngle * 0.5f;
            }

            if (dist > maxDist) return false;

            Vector3 dirToTarget = toTarget / dist;
            float angle = Vector3.Angle(forward, dirToTarget);
            if (angle > halfAngle) return false;

            if (useOcclusion) {
                if (Physics.Raycast(origin, dirToTarget, out RaycastHit hit2, dist, occlusionMask, occlusionTriggerInteraction)) {
                    Debug.DrawLine(origin, hit2.point, Color.red, 0f);
                    return hit2.transform == targetRoot || hit2.transform.IsChildOf(targetRoot);
                }
            }

            return true;
        }

        private void GetConeParams(out float range, out float halfAngle) {
            range = Mathf.Max(0.01f, fallbackRange);
            halfAngle = Mathf.Clamp(fallbackSpotAngle * 0.5f, 0.5f, 89.9f);

            if (flashlightLight != null && flashlightLight.type == LightType.Spot) {
                range = Mathf.Max(0.01f, flashlightLight.range);
                halfAngle = Mathf.Clamp(flashlightLight.spotAngle * 0.5f, 0.5f, 89.9f);
            }
        }

        public void ApplyMaxBatterySeconds(float value) {
            maxBatterySeconds = value;
            batteryRemaining = value;
        }

        public void DrainPercent(float percent) {
            percent = Mathf.Clamp01(percent);
            float drain = maxBatterySeconds * percent;
            batteryRemaining = Mathf.Max(0f, batteryRemaining - drain);

            if (batteryRemaining <= 0f) {
                batteryRemaining = 0f;
                isOn = false;
                ApplyIndicator(forceOff: false);
            }
        }

        private bool ShouldBlockInput() {
            if (loseState != null && loseState.hasLost) return true;
            if (winState != null && winState.hasWon) return true;
            return false;
        }
    }
}