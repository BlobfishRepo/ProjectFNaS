using UnityEngine;
using FNaS.Settings;

namespace FNaS.Systems {
    public class RuntimeSettingsApplier : MonoBehaviour {
        [SerializeField] private bool applyOnStart = true;
        [SerializeField] private bool includeInactiveObjects = true;

        private void Start() {
            if (applyOnStart) {
                ApplyAllSettings();
            }
        }

        [ContextMenu("Apply All Runtime Settings")]
        public void ApplyAllSettings() {
            var settings = RuntimeGameSettings.Instance;
            if (settings == null) {
                Debug.LogWarning("RuntimeSettingsApplier: RuntimeGameSettings not found.");
                return;
            }

            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(
                includeInactiveObjects ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );

            int appliedCount = 0;

            foreach (var behaviour in behaviours) {
                if (behaviour == null || behaviour == this)
                    continue;

                if (behaviour is IRuntimeSettingsConsumer consumer) {
                    consumer.ApplyRuntimeSettings(settings);
                    appliedCount++;
                }
            }

            Debug.Log($"RuntimeSettingsApplier: Applied settings to {appliedCount} consumer(s).");
        }
    }
}