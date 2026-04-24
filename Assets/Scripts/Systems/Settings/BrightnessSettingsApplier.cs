using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using FNaS.Settings;

namespace FNaS.Systems {
    [RequireComponent(typeof(Volume))]
    public class BrightnessSettingsApplier : MonoBehaviour, IRuntimeSettingsConsumer {
        [SerializeField] private Volume volume;

        [Header("Mapping")]
        [SerializeField] private float basePostExposure = 0.7f;
        [SerializeField] private float maxExtraPostExposure = 2.5f;

        private ColorAdjustments colorAdjustments;

        private void Awake() {
            ResolveVolume();
        }

        private void OnEnable() {
            ResolveVolume();

            if (RuntimeGameSettings.Instance != null) {
                RuntimeGameSettings.Instance.OnSettingsChanged += ApplyCurrentSettings;
                ApplyCurrentSettings();
            }
        }

        private void Start() {
            ApplyCurrentSettings();
        }

        private void OnDisable() {
            if (RuntimeGameSettings.Instance != null) {
                RuntimeGameSettings.Instance.OnSettingsChanged -= ApplyCurrentSettings;
            }
        }

        public void ApplyRuntimeSettings(RuntimeGameSettings settings) {
            Apply(settings);
        }

        private void ApplyCurrentSettings() {
            Apply(RuntimeGameSettings.Instance);
        }

        private void Apply(RuntimeGameSettings settings) {
            if (settings == null) return;

            ResolveVolume();
            if (colorAdjustments == null) return;

            int brightness = Mathf.Clamp(settings.GetInt("video.brightness"), 0, 10);
            float t = brightness / 10f;

            colorAdjustments.postExposure.Override(
                basePostExposure + maxExtraPostExposure * t
            );
        }

        private void ResolveVolume() {
            if (volume == null) {
                volume = GetComponent<Volume>();
            }

            colorAdjustments = null;

            if (volume != null && volume.profile != null) {
                volume.profile.TryGet(out colorAdjustments);
            }
        }
    }
}