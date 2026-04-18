using System;
using System.Collections.Generic;
using UnityEngine;

namespace FNaS.Systems {
    [Serializable]
    public class PaperTextPresetEntry {
        public int id;
        [TextArea(3, 20)] public string text;
    }

    [Serializable]
    public class PaperTextPresetDatabase {
        public List<PaperTextPresetEntry> presets = new();
    }

    public static class PaperTextPresets {
        private const string ResourcePath = "PaperTextPresets";

        private static bool loaded;
        private static readonly Dictionary<int, string> presetMap = new();

        public static string ResolveText(int presetIndex) {
            EnsureLoaded();

            if (presetMap.TryGetValue(presetIndex, out string text)) {
                return text ?? string.Empty;
            }

            if (presetMap.TryGetValue(0, out string fallback)) {
                return fallback ?? string.Empty;
            }

            return string.Empty;
        }

        private static void EnsureLoaded() {
            if (loaded) return;
            loaded = true;

            presetMap.Clear();

            TextAsset jsonAsset = Resources.Load<TextAsset>(ResourcePath);
            if (jsonAsset == null) {
                Debug.LogWarning($"PaperTextPresets: Could not find Data/{ResourcePath}.json");
                return;
            }

            PaperTextPresetDatabase db = null;

            try {
                db = JsonUtility.FromJson<PaperTextPresetDatabase>(jsonAsset.text);
            }
            catch (Exception ex) {
                Debug.LogError($"PaperTextPresets: Failed to parse JSON. {ex}");
                return;
            }

            if (db == null || db.presets == null) {
                Debug.LogWarning("PaperTextPresets: JSON loaded, but no presets were found.");
                return;
            }

            for (int i = 0; i < db.presets.Count; i++) {
                PaperTextPresetEntry entry = db.presets[i];
                if (entry == null) continue;

                presetMap[entry.id] = entry.text ?? string.Empty;
            }
        }

        public static void Reload() {
            loaded = false;
            presetMap.Clear();
            EnsureLoaded();
        }
    }
}