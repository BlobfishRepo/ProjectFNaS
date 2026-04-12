using System;
using System.IO;
using UnityEngine;

namespace FNaS.Systems {
    [Serializable]
    public class NightProgressSaveData {
        public int highestUnlockedNight = 1;
        public bool completedNight5 = false;
        public int starsEarned = 0;
    }

    public static class NightProgressSave {
        private static string SavePath =>
            Path.Combine(Application.persistentDataPath, "night_progress.json");

        public static NightProgressSaveData Load() {
            try {
                if (!File.Exists(SavePath)) {
                    NightProgressSaveData fresh = new NightProgressSaveData();
                    Save(fresh);
                    return fresh;
                }

                string json = File.ReadAllText(SavePath);
                NightProgressSaveData data = JsonUtility.FromJson<NightProgressSaveData>(json);

                if (data == null) {
                    data = new NightProgressSaveData();
                    Save(data);
                }

                data.highestUnlockedNight = Mathf.Clamp(data.highestUnlockedNight, 1, 5);
                return data;
            }
            catch (Exception ex) {
                Debug.LogError($"NightProgressSave load failed: {ex}");
                return new NightProgressSaveData();
            }
        }

        public static void Save(NightProgressSaveData data) {
            try {
                File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception ex) {
                Debug.LogError($"NightProgressSave save failed: {ex}");
            }
        }

        public static void ResetCampaignProgressPreserveStars() {
            NightProgressSaveData old = Load();

            Save(new NightProgressSaveData {
                highestUnlockedNight = 1,
                completedNight5 = false,
                starsEarned = old.starsEarned
            });
        }

        public static void MarkNightCompleted(int nightNumber) {
            NightProgressSaveData data = Load();

            if (nightNumber < 5) {
                data.highestUnlockedNight = Mathf.Max(data.highestUnlockedNight, nightNumber + 1);
            }
            else {
                data.highestUnlockedNight = 5;
                data.completedNight5 = true;
            }

            Save(data);
        }

        public static bool CanContinueCampaign() {
            NightProgressSaveData data = Load();
            return data.highestUnlockedNight > 1 || data.completedNight5;
        }

        public static int GetContinueNightNumber() {
            return Mathf.Clamp(Load().highestUnlockedNight, 1, 5);
        }
    }
}