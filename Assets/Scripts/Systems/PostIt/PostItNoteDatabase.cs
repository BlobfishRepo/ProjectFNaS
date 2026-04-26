using System;
using System.Collections.Generic;
using UnityEngine;

namespace FNaS.Systems {
    [Serializable]
    public class PostItNoteRecord {
        public string id;

        [TextArea(4, 20)]
        public string body;

        public List<int> campaignNights = new();
        public int presentationUnlockPercent = -1;
    }

    [Serializable]
    public class PostItNoteDatabase {
        public List<PostItNoteRecord> notes = new();

        public static PostItNoteDatabase FromJson(string json) {
            if (string.IsNullOrWhiteSpace(json)) {
                return new PostItNoteDatabase();
            }

            try {
                PostItNoteDatabase db = JsonUtility.FromJson<PostItNoteDatabase>(json);
                return db ?? new PostItNoteDatabase();
            }
            catch (Exception ex) {
                Debug.LogError($"Failed to parse PostIt note JSON: {ex}");
                return new PostItNoteDatabase();
            }
        }
    }
}