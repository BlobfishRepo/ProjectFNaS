using UnityEngine;
using UnityEngine.InputSystem;
using FNaS.Systems;
using FNaS.Settings;
using FNaS.Visuals;

namespace FNaS.Gameplay {
    public class BatteryPackRefill : MonoBehaviour, IRuntimeSettingsConsumer {
        [Header("References")]
        [SerializeField] private FlashlightTool flashlight;
        [SerializeField] private Camera clickCamera;

        [Header("Click")]
        [SerializeField] private LayerMask clickMask = ~0;
        [SerializeField] private float maxClickDistance = 20f;

        [Header("Rules")]
        [Range(0f, 1f)]
        [SerializeField] private float lowBatteryThreshold = 0.20f;

        [SerializeField] private bool consumeAfterUse = true;

        [Header("Pulse")]
        [SerializeField] private WhitePulseMaterialDriver pulseDriver;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip refillClip;
        [Range(0f, 1f)]
        [SerializeField] private float refillVolume = 1f;

        [Header("Settings")]
        [SerializeField] private string enabledSettingKey = "batteryPack.enabled";

        private bool used;

        private void Awake() {
            if (flashlight == null) {
                flashlight = FindFirstObjectByType<FlashlightTool>();
            }

            if (clickCamera == null) {
                clickCamera = Camera.main;
            }

            if (pulseDriver == null) {
                pulseDriver = GetComponentInChildren<WhitePulseMaterialDriver>(true);
            }

            if (audioSource == null) {
                audioSource = GetComponent<AudioSource>();
            }
        }

        private void Start() {
            ApplyRuntimeSettings(RuntimeGameSettings.Instance);
        }

        public void ApplyRuntimeSettings(RuntimeGameSettings settings) {
            if (settings == null) return;

            bool enabled = settings.GetBool(enabledSettingKey);
            gameObject.SetActive(enabled);
        }

        private void Update() {
            if (used) {
                UpdatePulse();
                return;
            }

            UpdatePulse();

            if (GameplayPauseManager.IsPausedGlobal) return;
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

            TryClick();
        }

        private void UpdatePulse() {
            if (pulseDriver == null) return;

            bool usable =
                !used &&
                flashlight != null &&
                flashlight.IsBatteryLowOrEmpty(lowBatteryThreshold);

            pulseDriver.SetPulseEnabled(usable);
        }

        private void TryClick() {
            if (flashlight == null) return;

            Camera cam = clickCamera != null ? clickCamera : Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (!Physics.Raycast(ray, out RaycastHit hit, maxClickDistance, clickMask, QueryTriggerInteraction.Ignore)) {
                return;
            }

            if (!hit.transform.IsChildOf(transform) && hit.transform != transform) {
                return;
            }

            if (!flashlight.IsBatteryLowOrEmpty(lowBatteryThreshold)) {
                return;
            }

            flashlight.RefillBattery();

            if (audioSource != null && refillClip != null) {
                audioSource.PlayOneShot(refillClip, refillVolume);
            }

            if (consumeAfterUse) {
                used = true;

                if (pulseDriver != null) {
                    pulseDriver.SetPulseEnabled(false);
                }

                gameObject.SetActive(false);
            }
        }
    }
}