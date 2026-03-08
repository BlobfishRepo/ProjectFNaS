using UnityEngine;

namespace FNaS.Settings {
    public class RuntimeGameSettings : MonoBehaviour {
        public static RuntimeGameSettings Instance { get; private set; }

        [Header("Standard Settings")]
        public float playerMoveSpeed = 5f;
        public float doorMaxDistance = 6f;

        [Header("Stalker - Core AI")]
        [Range(0, 20)] public int stalkerAI = 20;
        public float opportunityInterval = 5f;

        [Header("Debug Settings")]
        public PlayerMovementMode playerMovementMode = PlayerMovementMode.NodeBased;
        public StalkerMovementMode stalkerMovementMode = StalkerMovementMode.NodeBased;

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