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


        [Header("Debug Settings")]
        public PlayerMovementMode playerMovementMode = PlayerMovementMode.NodeBased;
        public StalkerMovementMode stalkerMovementMode = StalkerMovementMode.NodeBased;
        public bool debugMenuUnlocked = false;

        private void Awake() {
            if (Instance != null && Instance != this) {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
}