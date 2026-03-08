using System.Collections.Generic;
using UnityEngine;
using FNaS.MasterNodes;

namespace FNaS.Systems {
    public class MasterNodeRegistry : MonoBehaviour {
        public static MasterNodeRegistry Instance { get; private set; }

        private readonly Dictionary<string, MasterNode> byGuid = new();

        private void Awake() {
            if (Instance != null && Instance != this) {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            Rebuild();
        }

        public void Rebuild() {
            byGuid.Clear();

            var nodes = FindObjectsByType<MasterNode>(FindObjectsSortMode.None);
            foreach (var n in nodes) {
                if (n == null) continue;

                if (string.IsNullOrWhiteSpace(n.Guid)) {
                    Debug.LogError($"MasterNode '{n.name}' has empty GUID. Select it to trigger OnValidate, or re-save scene.");
                    continue;
                }

                if (byGuid.ContainsKey(n.Guid)) {
                    Debug.LogError($"Duplicate MasterNode GUID detected: {n.Guid} (node '{n.name}').");
                    continue;
                }

                byGuid.Add(n.Guid, n);
            }
        }

        public bool TryGet(string guid, out MasterNode node) => byGuid.TryGetValue(guid, out node);

        public MasterNode GetOrNull(string guid) {
            if (string.IsNullOrWhiteSpace(guid)) return null;
            return byGuid.TryGetValue(guid, out var n) ? n : null;
        }
    }
}