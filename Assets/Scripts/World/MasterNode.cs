using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FNaS.MasterNodes {
    [DisallowMultipleComponent]
    public class MasterNode : MonoBehaviour {
        [Header("Identity")]
        [Tooltip("Optional human-friendly name; if empty, GameObject name is used.")]
        public string nodeId;

        [SerializeField, Tooltip("Stable unique identifier for this node (do not edit).")]
        private string guid;

        public string Id => string.IsNullOrWhiteSpace(nodeId) ? gameObject.name : nodeId;
        public string Guid => guid;

        private void OnValidate() {
#if UNITY_EDITOR
            // Auto-generate GUID once, persist in scene.
            if (string.IsNullOrWhiteSpace(guid)) {
                guid = System.Guid.NewGuid().ToString("N");
                EditorUtility.SetDirty(this);
            }
#endif
        }
    }
}