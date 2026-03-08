using UnityEngine;

namespace FNaS.Settings {
    public enum StalkerMovementMode {
        NodeBased,
        RoamTest
    }
    public enum PlayerMovementMode {
        NodeBased,
        FreeRoam
    }

    [CreateAssetMenu(menuName = "FNaS/Settings/Game Settings Asset", fileName = "GameSettingsAsset")]
    public class GameSettingsAsset : ScriptableObject {
        [Header("Gameplay Defaults")]
        public PlayerMovementMode playerMovementMode = PlayerMovementMode.NodeBased;
        public StalkerMovementMode stalkerMovementMode = StalkerMovementMode.NodeBased;

        [Header("Debug")]
        public bool debugMenuUnlocked = false;
    }
}