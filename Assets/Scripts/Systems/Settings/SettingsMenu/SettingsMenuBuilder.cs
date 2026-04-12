using System.Collections.Generic;
using FNaS.Settings;
using UnityEngine;

namespace FNaS.UI.Settings {
    public class SettingsMenuBuilder : MonoBehaviour {
        [Header("References")]
        [SerializeField] private Transform contentRoot;
        [SerializeField] private SettingsSectionHeader sectionHeaderPrefab;
        [SerializeField] private SliderSettingRow sliderRowPrefab;
        [SerializeField] private ToggleSettingRow toggleRowPrefab;
        [SerializeField] private DropdownSettingRow dropdownRowPrefab;

        private readonly List<GameObject> spawnedObjects = new();

        public void Rebuild(RuntimeGameSettings runtimeSettings, bool showDebugSettings, SettingScreen includedScreens) {
            Clear();

            SettingCategory? lastCategory = null;

            foreach (var def in SettingsSchema.Definitions) {
                if (!SettingsSchema.ShouldShowOnScreen(def, includedScreens))
                    continue;

                if (def.debugOnly && !showDebugSettings)
                    continue;

                if (lastCategory != def.category) {
                    CreateSectionHeader(SettingsSchema.GetCategoryLabel(def.category));
                    lastCategory = def.category;
                }

                switch (def.controlType) {
                    case SettingControlType.FloatSlider:
                        CreateFloatSlider(def, runtimeSettings);
                        break;

                    case SettingControlType.IntSlider:
                        CreateIntSlider(def, runtimeSettings);
                        break;

                    case SettingControlType.Toggle:
                        CreateToggle(def, runtimeSettings);
                        break;

                    case SettingControlType.Dropdown:
                        CreateDropdown(def, runtimeSettings);
                        break;
                }
            }
        }

        private void CreateSectionHeader(string title) {
            var header = Instantiate(sectionHeaderPrefab, contentRoot);
            header.Setup(title);
            spawnedObjects.Add(header.gameObject);
        }

        private void CreateFloatSlider(SettingDefinition def, RuntimeGameSettings runtimeSettings) {
            var row = Instantiate(sliderRowPrefab, contentRoot);
            row.Setup(
                def.label,
                runtimeSettings.GetFloat(def.key),
                def.min,
                def.max,
                false,
                value => {
                    runtimeSettings.SetFloat(def.key, value);
                    runtimeSettings.SaveToJson();
                }
            );
            spawnedObjects.Add(row.gameObject);
        }

        private void CreateIntSlider(SettingDefinition def, RuntimeGameSettings runtimeSettings) {
            var row = Instantiate(sliderRowPrefab, contentRoot);
            row.Setup(
                def.label,
                runtimeSettings.GetInt(def.key),
                def.min,
                def.max,
                true,
                value => {
                    runtimeSettings.SetInt(def.key, Mathf.RoundToInt(value));
                    runtimeSettings.SaveToJson();
                }
            );
            spawnedObjects.Add(row.gameObject);
        }

        private void CreateToggle(SettingDefinition def, RuntimeGameSettings runtimeSettings) {
            var row = Instantiate(toggleRowPrefab, contentRoot);
            row.Setup(
                def.label,
                runtimeSettings.GetBool(def.key),
                value => {
                    runtimeSettings.SetBool(def.key, value);
                    runtimeSettings.SaveToJson();
                }
            );
            spawnedObjects.Add(row.gameObject);
        }

        private void CreateDropdown(SettingDefinition def, RuntimeGameSettings runtimeSettings) {
            var row = Instantiate(dropdownRowPrefab, contentRoot);
            row.Setup(
                def.label,
                runtimeSettings.GetInt(def.key),
                def.dropdownOptions,
                value => {
                    runtimeSettings.SetInt(def.key, value);
                    runtimeSettings.SaveToJson();
                }
            );
            spawnedObjects.Add(row.gameObject);
        }

        private void Clear() {
            for (int i = 0; i < spawnedObjects.Count; i++) {
                if (spawnedObjects[i] != null) {
                    Destroy(spawnedObjects[i]);
                }
            }

            spawnedObjects.Clear();
        }
    }
}