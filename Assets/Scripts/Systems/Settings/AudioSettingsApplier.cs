using UnityEngine;
using UnityEngine.Audio;
using FNaS.Settings;

namespace FNaS.Systems {
    public class AudioSettingsApplier : MonoBehaviour {
        [SerializeField] private AudioMixer mixer;

        private void OnEnable() {
            if (RuntimeGameSettings.Instance != null) {
                RuntimeGameSettings.Instance.OnSettingsChanged += ApplyAudioSettings;
                ApplyAudioSettings();
            }
        }

        private void Start() {
            ApplyAudioSettings();
        }

        private void OnDisable() {
            if (RuntimeGameSettings.Instance != null) {
                RuntimeGameSettings.Instance.OnSettingsChanged -= ApplyAudioSettings;
            }
        }

        public void ApplyAudioSettings() {
            RuntimeGameSettings settings = RuntimeGameSettings.Instance;
            if (settings == null || mixer == null) return;

            Apply(settings, "audio.masterVolume", "MasterVolume");
            Apply(settings, "audio.ambienceVolume", "AmbienceVolume");
            Apply(settings, "audio.sfxVolume", "SFXVolume");
            Apply(settings, "audio.uiVolume", "UIVolume");
            Apply(settings, "audio.monsterVolume", "MonsterVolume");
            Apply(settings, "audio.monitorVolume", "MonitorVolume");
        }

        private void Apply(RuntimeGameSettings settings, string key, string parameter) {
            float percent = Mathf.Clamp(settings.GetInt(key), 0, 100);
            float linear = percent / 100f;

            float db = linear <= 0.0001f ? -80f : Mathf.Log10(linear) * 20f;
            mixer.SetFloat(parameter, db);
        }
    }
}