using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FNaS.MasterNodes {
    public enum ExitTag { Forward, Back, Left, Right, Custom }

    [DisallowMultipleComponent]
    public class MasterNode : MonoBehaviour {
        [Serializable]
        public class Exit {
            public string name;
            public ExitTag tag = ExitTag.Custom;
            public MasterNode target;
        }

        [Header("Identity")]
        [Tooltip("Optional human-friendly name; if empty, GameObject name is used.")]
        public string nodeId;

        [SerializeField, Tooltip("Stable unique identifier for this node (do not edit).")]
        private string guid;

        [Header("Graph Exits")]
        public List<Exit> exits = new List<Exit>();

        public string Id => string.IsNullOrWhiteSpace(nodeId) ? gameObject.name : nodeId;
        public string Guid => guid;

        public bool TryGetExitByTag(ExitTag tag, out Exit exit) {
            for (int i = 0; i < exits.Count; i++) {
                if (exits[i] != null && exits[i].tag == tag && exits[i].target != null) {
                    exit = exits[i];
                    return true;
                }
            }
            exit = null;
            return false;
        }

        private void OnValidate() {
            if (exits == null) exits = new List<Exit>();

#if UNITY_EDITOR
            // Auto-generate GUID once, persist in scene.
            if (string.IsNullOrWhiteSpace(guid))
            {
                guid = System.Guid.NewGuid().ToString("N");
                EditorUtility.SetDirty(this);
            }
#endif
        }
    }
}