using UnityEngine;
using UnityEngine.InputSystem;

namespace FNaS.Systems {
    public class FlashlightTool : MonoBehaviour {

        [Header("Battery")]
        public float maxBatterySeconds = 30f;

        [Header("Indicator (optional)")]
        public Light flashlightLight;
        public GameObject indicatorObject;

        [Header("Audio")]
        public AudioSource uiSource;
        public AudioClip lightSwitchClip;

        [Header("Low Battery Flicker")]
        [Range(0f, 1f)] public float flickerStartPercent = 0.25f; // start flicker at last 25%
        public float flickerMinHz = 2f;   // slow flicker when just entered low battery
        public float flickerMaxHz = 18f;  // rapid flicker near empty
        [Range(0f, 1f)] public float flickerOffChance = 0.35f; // chance a flicker "pulse" turns off briefly
        public float flickerMinOffSeconds = 0.03f;
        public float flickerMaxOffSeconds = 0.10f;

        public bool isOn { get; private set; }

        private float batteryRemaining;
        private PlayerInputActions input;
        private float flickerTimer;
        private float offTimer;

        private void Awake() {
            input = new PlayerInputActions();
        }

        private void OnEnable() {
            input.Player.Enable();
            input.Player.Flashlight.performed += OnFlashlightPressed;
        }

        private void OnDisable() {
            input.Player.Flashlight.performed -= OnFlashlightPressed;
            input.Player.Disable();
        }

        private void Start() {
            batteryRemaining = maxBatterySeconds;
            ApplyIndicator(forceOff: false);
        }

        private void Update() {
            if (!isOn) return;

            batteryRemaining -= Time.deltaTime;
            if (batteryRemaining <= 0f) {
                batteryRemaining = 0f;
                isOn = false;
                ApplyIndicator();
            }
            UpdateLowBatteryFlicker();
        }

        private void OnFlashlightPressed(InputAction.CallbackContext ctx) {
            Toggle();
        }

        public void Toggle() {
            if (batteryRemaining <= 0f) return;

            if (uiSource != null && lightSwitchClip != null)
                uiSource.PlayOneShot(lightSwitchClip);

            isOn = !isOn;
            ApplyIndicator();
        }

        private void ApplyIndicator(bool forceOff = false) {
            bool on = isOn && !forceOff;

            if (flashlightLight != null) flashlightLight.enabled = on;
            if (indicatorObject != null) indicatorObject.SetActive(on);
        }

        private void UpdateLowBatteryFlicker() {
            if (!isOn) { // ensure stable when off
                offTimer = 0f;
                flickerTimer = 0f;
                return;
            }

            if (maxBatterySeconds <= 0f) return;

            float pct = batteryRemaining / maxBatterySeconds;
            if (pct > flickerStartPercent) {
                // Not low battery -> steady on
                offTimer = 0f;
                ApplyIndicator(forceOff: false);
                return;
            }

            // How deep into low-battery are we? 0 at threshold, 1 near empty.
            float t = Mathf.InverseLerp(flickerStartPercent, 0f, pct);

            // Increase flicker frequency as battery gets lower
            float hz = Mathf.Lerp(flickerMinHz, flickerMaxHz, t);
            flickerTimer += Time.deltaTime;

            // If we are currently in an "off pulse", count it down
            if (offTimer > 0f) {
                offTimer -= Time.deltaTime;
                ApplyIndicator(forceOff: true);
                return;
            }

            // Otherwise, keep it on, and occasionally create an off pulse
            ApplyIndicator(forceOff: false);

            float period = 1f / Mathf.Max(0.01f, hz);
            if (flickerTimer >= period) {
                flickerTimer = 0f;

                // Chance to blink off this cycle (more likely as battery gets lower)
                float chance = Mathf.Lerp(0.10f, flickerOffChance, t);
                if (Random.value < chance) {
                    offTimer = Random.Range(flickerMinOffSeconds, flickerMaxOffSeconds);
                }
            }
        }
    }
}