using System;
using System.IO;
using UnityEngine;

namespace FNaS.Systems {
    [Serializable]
    public class NightProgressSaveData {
        public int highestUnlockedNight = 1;
        public bool completedNight5 = false;
        public bool customNightUnlocked = false;

        public bool earnedStar1_Night5 = false;
        public bool earnedStar2_CustomThreshold1 = false;
        public bool earnedStar3_CustomThreshold2 = false;

        public int starsEarned = 0;
    }

    public static class NightProgressSave {
        private static string SavePath =>
            Path.Combine(Application.persistentDataPath, "night_progress.json");

        public static NightProgressSaveData Load() {
            try {
                if (!File.Exists(SavePath)) {
                    NightProgressSaveData fresh = new NightProgressSaveData();
                    RecalculateStars(fresh);
                    Save(fresh);
                    return fresh;
                }

                string json = File.ReadAllText(SavePath);
                NightProgressSaveData data = JsonUtility.FromJson<NightProgressSaveData>(json);

                if (data == null) {
                    data = new NightProgressSaveData();
                    RecalculateStars(data);
                    Save(data);
                }

                data.highestUnlockedNight = Mathf.Clamp(data.highestUnlockedNight, 1, 5);
                RecalculateStars(data);
                return data;
            }
            catch (Exception ex) {
                Debug.LogError($"NightProgressSave load failed: {ex}");
                return new NightProgressSaveData();
            }
        }

        public static void Save(NightProgressSaveData data) {
            try {
                RecalculateStars(data);
                File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception ex) {
                Debug.LogError($"NightProgressSave save failed: {ex}");
            }
        }

        private static void RecalculateStars(NightProgressSaveData data) {
            if (data == null) return;

            int stars = 0;
            if (data.earnedStar1_Night5) stars++;
            if (data.earnedStar2_CustomThreshold1) stars++;
            if (data.earnedStar3_CustomThreshold2) stars++;

            data.starsEarned = stars;
            data.customNightUnlocked |= data.earnedStar1_Night5 || data.completedNight5;
        }

        public static void ResetCampaignProgressPreserveUnlocks() {
            NightProgressSaveData old = Load();

            Save(new NightProgressSaveData {
                highestUnlockedNight = 1,
                completedNight5 = false,
                customNightUnlocked = old.customNightUnlocked,
                earnedStar1_Night5 = old.earnedStar1_Night5,
                earnedStar2_CustomThreshold1 = old.earnedStar2_CustomThreshold1,
                earnedStar3_CustomThreshold2 = old.earnedStar3_CustomThreshold2
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
                data.customNightUnlocked = true;
                data.earnedStar1_Night5 = true;
            }

            Save(data);
        }

        public static void AwardCustomNightThreshold1Star() {
            NightProgressSaveData data = Load();
            data.earnedStar2_CustomThreshold1 = true;
            Save(data);
        }

        public static void AwardCustomNightThreshold2Star() {
            NightProgressSaveData data = Load();
            data.earnedStar3_CustomThreshold2 = true;
            Save(data);
        }

        public static bool CanContinueCampaign() {
            NightProgressSaveData data = Load();
            return data.highestUnlockedNight > 1 || data.completedNight5;
        }

        public static int GetContinueNightNumber() {
            return Mathf.Clamp(Load().highestUnlockedNight, 1, 5);
        }

        public static bool IsCustomNightUnlocked() {
            return Load().customNightUnlocked;
        }

        public static int GetStarsEarned() {
            return Load().starsEarned;
        }

        public static bool HasStar1() => Load().earnedStar1_Night5;
        public static bool HasStar2() => Load().earnedStar2_CustomThreshold1;
        public static bool HasStar3() => Load().earnedStar3_CustomThreshold2;
    }
}