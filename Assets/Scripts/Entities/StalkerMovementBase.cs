using UnityEngine;
using FNaS.MasterNodes;

namespace FNaS.Entities.Stalker {
    public abstract class StalkerMovementBase : MonoBehaviour {
        public abstract MasterNode CurrentMasterNode { get; }
        public abstract bool AtDoor { get; }

        public abstract void Initialize();
        public abstract void TickMovementVisuals();
        public abstract void RefreshOccupancy();
        public abstract bool TryAdvance(MasterNode playerNode, bool allowShareNodeWithPlayer);
        public abstract bool PushBack(int steps);
        public abstract void ReappearInFirstNNodes(int firstNNodes);
        public abstract void ClearOccupancy();
    }
}