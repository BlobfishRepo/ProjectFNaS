using UnityEngine;
using FNaS.MasterNodes;

namespace FNaS.Systems {
    public class GameAttentionState : MonoBehaviour {
        [Header("Monitor state")]
        public bool isMonitorInUse;

        [Header("Camera feed state")]
        public bool isCameraActive;
        public MasterNode activeCameraNode;

        [Header("World-space look target for AI")]
        public Transform activeCameraLookTarget;
    }
}