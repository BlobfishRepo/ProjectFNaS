using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FNaS.Settings {
    [Serializable]
    public class SettingValueEntry {
        public string key;
        public string value;
    }

    [Serializable]
    public class RuntimeGameSettingsSaveData {
        public bool debugMenuUnlocked = false;
        public bool playerSettingsDebugUnlocked = false;
        public List<SettingValueEntry> entries = new();
    }

    public class RuntimeGameSettings : MonoBehaviour {
        public static RuntimeGameSettings Instance { get; private set; }
        public event System.Action OnSettingsChanged;

        public static string SettingsPath =>
            Path.Combine(Application.persistentDataPath, "runtime_game_settings.json");

        private readonly Dictionary<string, float> floatValues = new();
        private readonly Dictionary<string, int> intValues = new();
        private readonly Dictionary<string, bool> boolValues = new();

        [SerializeField] private bool debugMenuUnlocked;
        [SerializeField] private bool playerSettingsDebugUnlocked;

        public bool DebugMenuUnlocked {
            get => debugMenuUnlocked;
            set => debugMenuUnlocked = value;
        }

        public bool PlayerSettingsDebugUnlocked {
            get => playerSettingsDebugUnlocked;
            set => playerSettingsDebugUnlocked = value;
        }

        private void Awake() {
            if (Instance != null && Instance != this) {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadFromJson();
            Debug.Log($"RuntimeGameSettings path: {SettingsPath}");
        }

        public void ResetToDefaults() {
            floatValues.Clear();
            intValues.Clear();
            boolValues.Clear();

            foreach (var def in SettingsSchema.Definitions) {
                switch (def.controlType) {
                    case SettingControlType.FloatSlider:
                        floatValues[def.key] = def.defaultFloat;
                        break;

                    case SettingControlType.IntSlider:
                    case SettingControlType.Dropdown:
                        intValues[def.key] = def.defaultInt;
                        break;

                    case SettingControlType.Toggle:
                        boolValues[def.key] = def.defaultBool;
                        break;
                }
            }

            debugMenuUnlocked = false;
            playerSettingsDebugUnlocked = false;
        }

        public void LoadFromJson() {
            ResetToDefaults();

            try {
                if (!File.Exists(SettingsPath)) {
                    SaveToJson();
                    return;
                }

                string json = File.ReadAllText(SettingsPath);
                RuntimeGameSettingsSaveData saveData = JsonUtility.FromJson<RuntimeGameSettingsSaveData>(json);

                if (saveData == null) {
                    SaveToJson();
                    return;
                }

                debugMenuUnlocked = saveData.debugMenuUnlocked;
                playerSettingsDebugUnlocked = saveData.playerSettingsDebugUnlocked;

                if (saveData.entries == null)
                    return;

                foreach (var entry in saveData.entries) {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                        continue;

                    if (!SettingsSchema.TryGetDefinition(entry.key, out var def))
                        continue;

                    switch (def.controlType) {
                        case SettingControlType.FloatSlider:
                            if (float.TryParse(entry.value, out float floatValue)) {
                                floatValues[entry.key] = floatValue;
                            }
                            break;

                        case SettingControlType.IntSlider:
                        case SettingControlType.Dropdown:
                            if (int.TryParse(entry.value, out int intValue)) {
                                intValues[entry.key] = intValue;
                            }
                            break;

                        case SettingControlType.Toggle:
                            if (bool.TryParse(entry.value, out bool boolValue)) {
                                boolValues[entry.key] = boolValue;
                            }
                            break;
                    }
                }
            }
            catch (Exception ex) {
                Debug.LogError($"Failed to load RuntimeGameSettings JSON: {ex}");
                ResetToDefaults();
            }
        }

        public void SaveToJson() {
            try {
                RuntimeGameSettingsSaveData saveData = new() {
                    debugMenuUnlocked = debugMenuUnlocked,
                    playerSettingsDebugUnlocked = playerSettingsDebugUnlocked,
                    entries = BuildEntries()
                };

                string json = JsonUtility.ToJson(saveData, true);
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex) {
                Debug.LogError($"Failed to save RuntimeGameSettings JSON: {ex}");
            }
        }

        private List<SettingValueEntry> BuildEntries() {
            List<SettingValueEntry> entries = new();

            foreach (var def in SettingsSchema.Definitions) {
                switch (def.controlType) {
                    case SettingControlType.FloatSlider:
                        entries.Add(new SettingValueEntry {
                            key = def.key,
                            value = GetFloat(def.key).ToString()
                        });
                        break;

                    case SettingControlType.IntSlider:
                    case SettingControlType.Dropdown:
                        entries.Add(new SettingValueEntry {
                            key = def.key,
                            value = GetInt(def.key).ToString()
                        });
                        break;

                    case SettingControlType.Toggle:
                        entries.Add(new SettingValueEntry {
                            key = def.key,
                            value = GetBool(def.key).ToString()
                        });
                        break;
                }
            }

            return entries;
        }

        public float GetFloat(string key) {
            if (floatValues.TryGetValue(key, out float value))
                return value;

            if (SettingsSchema.TryGetDefinition(key, out var def))
                return def.defaultFloat;

            Debug.LogWarning($"No float setting found for key '{key}'.");
            return 0f;
        }

        public int GetInt(string key) {
            if (intValues.TryGetValue(key, out int value))
                return value;

            if (SettingsSchema.TryGetDefinition(key, out var def))
                return def.defaultInt;

            Debug.LogWarning($"No int setting found for key '{key}'.");
            return 0;
        }

        public bool GetBool(string key) {
            if (boolValues.TryGetValue(key, out bool value))
                return value;

            if (SettingsSchema.TryGetDefinition(key, out var def))
                return def.defaultBool;

            Debug.LogWarning($"No bool setting found for key '{key}'.");
            return false;
        }

        public void SetFloat(string key, float value) {
            floatValues[key] = value;
            OnSettingsChanged?.Invoke();
        }

        public void SetInt(string key, int value) {
            intValues[key] = value;
            OnSettingsChanged?.Invoke();
        }

        public void SetBool(string key, bool value) {
            boolValues[key] = value;
            OnSettingsChanged?.Invoke();
        }

        public PlayerMovementMode GetPlayerMovementMode() {
            return (PlayerMovementMode)GetInt("debug.playerMovementMode");
        }

        public StalkerMovementMode GetStalkerMovementMode() {
            return (StalkerMovementMode)GetInt("debug.stalkerMovementMode");
        }

        public bool AreStarRelevantSettingsAtDefaults(Predicate<string> allowedKeyOverride = null) {
            foreach (var def in SettingsSchema.Definitions) {
                if (!def.affectsStarEligibility)
                    continue;

                if (allowedKeyOverride != null && allowedKeyOverride(def.key))
                    continue;

                switch (def.controlType) {
                    case SettingControlType.FloatSlider:
                        if (!Mathf.Approximately(GetFloat(def.key), def.defaultFloat))
                            return false;
                        break;

                    case SettingControlType.IntSlider:
                    case SettingControlType.Dropdown:
                        if (GetInt(def.key) != def.defaultInt)
                            return false;
                        break;

                    case SettingControlType.Toggle:
                        if (GetBool(def.key) != def.defaultBool)
                            return false;
                        break;
                }
            }

            return true;
        }

        public bool HasNonDefaultStarRelevantDevGameplaySettings() {
            foreach (var def in SettingsSchema.Definitions) {
                // Only care about settings that appear on the hidden dev gameplay screen.
                if ((def.screens & SettingScreen.DevGameplay) == 0)
                    continue;

                // Only care about settings that affect star eligibility.
                if (!def.affectsStarEligibility)
                    continue;

                switch (def.controlType) {
                    case SettingControlType.FloatSlider:
                        if (!Mathf.Approximately(GetFloat(def.key), def.defaultFloat))
                            return true;
                        break;

                    case SettingControlType.IntSlider:
                    case SettingControlType.Dropdown:
                        if (GetInt(def.key) != def.defaultInt)
                            return true;
                        break;

                    case SettingControlType.Toggle:
                        if (GetBool(def.key) != def.defaultBool)
                            return true;
                        break;
                }
            }

            return false;
        }

        public bool HasNonDefaultFunSettingsEnabled() {
            foreach (var def in SettingsSchema.Definitions) {
                if ((def.screens & SettingScreen.PlayerSettingsFun) == 0)
                    continue;

                switch (def.controlType) {
                    case SettingControlType.FloatSlider:
                        if (!Mathf.Approximately(GetFloat(def.key), def.defaultFloat))
                            return true;
                        break;

                    case SettingControlType.IntSlider:
                    case SettingControlType.Dropdown:
                        if (GetInt(def.key) != def.defaultInt)
                            return true;
                        break;

                    case SettingControlType.Toggle:
                        if (GetBool(def.key) != def.defaultBool)
                            return true;
                        break;
                }
            }

            return false;
        }
    }
}