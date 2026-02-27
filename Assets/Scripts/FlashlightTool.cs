using UnityEngine;
using UnityEngine.InputSystem;

namespace FNaS.Systems {
    public class FlashlightTool : MonoBehaviour {
        [Header("Battery")]
        public float maxBatterySeconds = 30f;

        [Header("Indicator (optional)")]
        public Light flashlightLight;           // assign a Light if you want
        public GameObject indicatorObject;      // or assign any GameObject (quad, cone, etc.)

        public bool isOn { get; private set; }

        private float batteryRemaining;

        private void Start() {
            batteryRemaining = maxBatterySeconds;
            ApplyIndicator();
        }

        private void Update() {
            // New Input System key check (no old Input API)
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
                Toggle();

            if (!isOn) return;

            batteryRemaining -= Time.deltaTime;
            if (batteryRemaining <= 0f) {
                batteryRemaining = 0f;
                isOn = false;
                ApplyIndicator();
            }
        }

        public void Toggle() {
            Debug.Log($"Flashlight: {(isOn ? "ON" : "OFF")}, battery={batteryRemaining:F1}s");
            if (batteryRemaining <= 0f) return;
            isOn = !isOn;
            ApplyIndicator();
        }

        private void ApplyIndicator() {
            if (flashlightLight != null) flashlightLight.enabled = isOn;
            if (indicatorObject != null) indicatorObject.SetActive(isOn);
        }
    }
}