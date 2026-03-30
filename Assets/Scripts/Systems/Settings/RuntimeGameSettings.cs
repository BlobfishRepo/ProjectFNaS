using System;
using System.IO;
using UnityEngine;

namespace FNaS.Settings {
    public enum PlayerMovementMode {
        NodeBased,
        FreeRoam
    }

    public enum StalkerMovementMode {
        NodeBased,
        RoamTest
    }

    [Serializable]
    public class RuntimeGameSettingsData {
        public float playerMoveSpeed = 5f;
        public float doorMaxDistance = 6f;

        [Range(0, 20)] public int stalkerAI = 20;
        public float opportunityInterval = 5f;

        public float maxBatterySeconds = 30f;

        public bool freezeIfSeenOnCamera = false;
        public bool freezeIfSeenInPerson = true;
        public bool allowShareNodeWithPlayer = true;

        [Range(0, 20)] public int lostGirlAI = 10;
        public float lostGirlMoveSpeed = 8f;

        public PlayerMovementMode playerMovementMode = PlayerMovementMode.NodeBased;
        public StalkerMovementMode stalkerMovementMode = StalkerMovementMode.NodeBased;
        public bool debugMenuUnlocked = false;
    }

    public class RuntimeGameSettings : MonoBehaviour {
        public static RuntimeGameSettings Instance { get; private set; }

        [Header("Standard Settings")]
        public float playerMoveSpeed = 5f;
        public float doorMaxDistance = 6f;

        [Header("Stalker - Core AI")]
        [Range(0, 20)] public int stalkerAI = 20;
        public float opportunityInterval = 5f;

        [Header("Flashlight")]
        public float maxBatterySeconds = 30f;

        [Header("Stalker - Freeze Rules")]
        public bool freezeIfSeenOnCamera = false;
        public bool freezeIfSeenInPerson = true;
        public bool allowShareNodeWithPlayer = true;

        [Header("Lost Girl")]
        [Range(0, 20)] public int lostGirlAI = 10;
        public float lostGirlMoveSpeed = 8f;

        [Header("Debug Settings")]
        public PlayerMovementMode playerMovementMode = PlayerMovementMode.NodeBased;
        public StalkerMovementMode stalkerMovementMode = StalkerMovementMode.NodeBased;
        public bool debugMenuUnlocked = false;

        public static string SettingsPath =>
            Path.Combine(Application.persistentDataPath, "runtime_game_settings.json");

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

        public void LoadDefaults() {
            ApplyData(new RuntimeGameSettingsData());
        }

        public void SaveToJson() {
            try {
                RuntimeGameSettingsData data = ToData();
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(SettingsPath, json);
                Debug.Log($"RuntimeGameSettings saved to: {SettingsPath}");
            }
            catch (Exception ex) {
                Debug.LogError($"Failed to save RuntimeGameSettings JSON: {ex}");
            }
        }

        public void LoadFromJson() {
            try {
                if (!File.Exists(SettingsPath)) {
                    Debug.Log($"Settings file not found. Creating default JSON at: {SettingsPath}");
                    LoadDefaults();
                    SaveToJson();
                    return;
                }

                string json = File.ReadAllText(SettingsPath);
                RuntimeGameSettingsData data = JsonUtility.FromJson<RuntimeGameSettingsData>(json);

                if (data == null) {
                    Debug.LogWarning("Settings JSON was empty or invalid. Reverting to defaults.");
                    LoadDefaults();
                    SaveToJson();
                    return;
                }

                ApplyData(data);
                Debug.Log($"RuntimeGameSettings loaded from: {SettingsPath}");
            }
            catch (Exception ex) {
                Debug.LogError($"Failed to load RuntimeGameSettings JSON: {ex}");
                LoadDefaults();
            }
        }

        public RuntimeGameSettingsData ToData() {
            return new RuntimeGameSettingsData {
                playerMoveSpeed = playerMoveSpeed,
                doorMaxDistance = doorMaxDistance,
                stalkerAI = stalkerAI,
                opportunityInterval = opportunityInterval,
                maxBatterySeconds = maxBatterySeconds,
                freezeIfSeenOnCamera = freezeIfSeenOnCamera,
                freezeIfSeenInPerson = freezeIfSeenInPerson,
                allowShareNodeWithPlayer = allowShareNodeWithPlayer,
                lostGirlAI = lostGirlAI,
                lostGirlMoveSpeed = lostGirlMoveSpeed,
                playerMovementMode = playerMovementMode,
                stalkerMovementMode = stalkerMovementMode,
                debugMenuUnlocked = debugMenuUnlocked
            };
        }

        public void ApplyData(RuntimeGameSettingsData data) {
            playerMoveSpeed = data.playerMoveSpeed;
            doorMaxDistance = data.doorMaxDistance;
            stalkerAI = data.stalkerAI;
            opportunityInterval = data.opportunityInterval;
            maxBatterySeconds = data.maxBatterySeconds;
            freezeIfSeenOnCamera = data.freezeIfSeenOnCamera;
            freezeIfSeenInPerson = data.freezeIfSeenInPerson;
            allowShareNodeWithPlayer = data.allowShareNodeWithPlayer;
            lostGirlAI = data.lostGirlAI;
            lostGirlMoveSpeed = data.lostGirlMoveSpeed;
            playerMovementMode = data.playerMovementMode;
            stalkerMovementMode = data.stalkerMovementMode;
            debugMenuUnlocked = data.debugMenuUnlocked;
        }
    }
}