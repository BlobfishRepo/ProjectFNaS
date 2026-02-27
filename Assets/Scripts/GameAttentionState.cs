using UnityEngine;
using FNaS.MasterNodes;

namespace FNaS.Systems {
    [CreateAssetMenu(menuName = "FNaS/Systems/Game Attention State", fileName = "GameAttentionState")]
    public class GameAttentionState : ScriptableObject {
        [Header("Attention Flags")]
        public bool isWorking;
        public bool isCameraActive;

        [Header("Camera Context")]
        public MasterNode activeCameraNode;

        public void ResetState() {
            isWorking = false;
            isCameraActive = false;
            activeCameraNode = null;
        }
    }
}