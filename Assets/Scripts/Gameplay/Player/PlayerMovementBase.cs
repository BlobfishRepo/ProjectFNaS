using UnityEngine;
using FNaS.MasterNodes;

namespace FNaS.Gameplay {
    public abstract class PlayerMovementBase : MonoBehaviour {
        public abstract MasterNode CurrentMasterNode { get; }
        public abstract bool IsMoving { get; }

        public abstract Transform RigTransform { get; }
        public abstract Transform ViewTransform { get; }

        public abstract void Initialize(PlayerEntity player, PlayerInputController input);
    }
}