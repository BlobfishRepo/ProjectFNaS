using UnityEngine;

namespace FNaS.Settings {
    public class GameSettingsManager : MonoBehaviour {
        public static GameSettingsManager Instance { get; private set; }

        [Header("Defaults")]
        [SerializeField] private GameSettingsAsset defaults;

        public PlayerMovementMode PlayerMovementMode { get; private set; }
        public StalkerMovementMode StalkerMovementMode { get; private set; }
        public bool DebugMenuUnlocked { get; private set; }

        private const string PlayerMovementModeKey = "FNaS.PlayerMovementMode";
        private const string StalkerMovementModeKey = "FNaS.StalkerMovementMode";
        private const string DebugMenuUnlockedKey = "FNaS.DebugMenuUnlocked";

        private void Awake() {
            if (Instance != null && Instance != this) {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadFromDefaultsThenPrefs();
            Debug.Log($"Loaded PlayerMovementMode = {PlayerMovementMode}", this);
        }

        private void LoadFromDefaultsThenPrefs() {
            if (defaults != null) {
                PlayerMovementMode = defaults.playerMovementMode;
                StalkerMovementMode = defaults.stalkerMovementMode;
                DebugMenuUnlocked = defaults.debugMenuUnlocked;
            }
            else {
                PlayerMovementMode = PlayerMovementMode.NodeBased;
                StalkerMovementMode = StalkerMovementMode.NodeBased;
                DebugMenuUnlocked = false;
            }

            if (PlayerPrefs.HasKey(PlayerMovementModeKey)) {
                PlayerMovementMode = (PlayerMovementMode)PlayerPrefs.GetInt(
                    PlayerMovementModeKey,
                    (int)PlayerMovementMode
                );
            }

            if (PlayerPrefs.HasKey(StalkerMovementModeKey)) {
                StalkerMovementMode = (StalkerMovementMode)PlayerPrefs.GetInt(
                    StalkerMovementModeKey,
                    (int)StalkerMovementMode
                );
            }

            if (PlayerPrefs.HasKey(DebugMenuUnlockedKey)) {
                DebugMenuUnlocked = PlayerPrefs.GetInt(DebugMenuUnlockedKey, DebugMenuUnlocked ? 1 : 0) != 0;
            }
        }

        public void SetPlayerMovementMode(PlayerMovementMode mode) {
            PlayerMovementMode = mode;
        }

        public void SetStalkerMovementMode(StalkerMovementMode mode) {
            StalkerMovementMode = mode;
        }

        public void SetDebugMenuUnlocked(bool unlocked) {
            DebugMenuUnlocked = unlocked;
        }

        public void SaveToPrefs() {
            PlayerPrefs.SetInt(PlayerMovementModeKey, (int)PlayerMovementMode);
            PlayerPrefs.SetInt(StalkerMovementModeKey, (int)StalkerMovementMode);
            PlayerPrefs.SetInt(DebugMenuUnlockedKey, DebugMenuUnlocked ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void ResetToDefaults() {
            if (defaults != null) {
                PlayerMovementMode = defaults.playerMovementMode;
                StalkerMovementMode = defaults.stalkerMovementMode;
                DebugMenuUnlocked = defaults.debugMenuUnlocked;
            }
            else {
                PlayerMovementMode = PlayerMovementMode.NodeBased;
                StalkerMovementMode = StalkerMovementMode.NodeBased;
                DebugMenuUnlocked = false;
            }
        }
    }
}