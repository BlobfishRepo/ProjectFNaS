using System.Collections.Generic;
using UnityEngine;

namespace FNaS.Entities {
    [CreateAssetMenu(menuName = "FNaS/Paths/Linear Path Definition (GUID)", fileName = "LinearPathDefinition")]
    public class LinearPathDefinition : ScriptableObject {
        [Tooltip("Ordered list of MasterNode GUIDs in this scene.")]
        public List<string> nodeGuids = new();
    }
}