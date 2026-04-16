using FNaS.Settings;
using UnityEngine;

namespace FNaS.Systems {
    public enum NightPlayMode {
        None,
        Campaign,
        CustomNight,
        Presentation
    }

    public class NightSessionManager : MonoBehaviour {
        public static NightSessionManager Instance { get; private set; }

        [Header("Scenes")]
        public string gameplaySceneName = "SceneGameplay";
        public string introSceneName = "SceneSettings";

        [Header("Custom Night Star Thresholds")]
        [SerializeField] private int customNightStar2MinimumAI = 10;
        [SerializeField] private int customNightStar3MinimumAI = 20;

        [Header("Runtime")]
        [SerializeField] private NightPlayMode playMode = NightPlayMode.None;
        [SerializeField] private int currentCampaignNight = 1;

        public NightPlayMode PlayMode => playMode;
        public int CurrentCampaignNight => currentCampaignNight;
        public int CustomNightStar2MinimumAI => customNightStar2MinimumAI;
        public int CustomNightStar3MinimumAI => customNightStar3MinimumAI;

        private void Awake() {
            if (Instance != null && Instance != this) {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void BeginNewCampaign(RuntimeGameSettings settings) {
            NightProgressSave.ResetCampaignProgressPreserveUnlocks();

            playMode = NightPlayMode.Campaign;
            currentCampaignNight = 1;

            ApplyCampaignNight(settings);
        }

        public void BeginContinueCampaign(RuntimeGameSettings settings) {
            playMode = NightPlayMode.Campaign;
            currentCampaignNight = NightProgressSave.GetContinueNightNumber();

            ApplyCampaignNight(settings);
        }

        public void BeginCustomNight(RuntimeGameSettings settings) {
            playMode = NightPlayMode.CustomNight;
            CustomNightPresets.Apply(settings);
        }

        public void BeginPresentationNight(RuntimeGameSettings settings) {
            playMode = NightPlayMode.Presentation;
            PresentationNightPresets.ApplyForPercent(settings, 0);
        }

        public void ClearSession() {
            playMode = NightPlayMode.None;
            currentCampaignNight = 1;
        }

        public void AdvanceCampaign(RuntimeGameSettings settings) {
            currentCampaignNight = Mathf.Clamp(currentCampaignNight + 1, 1, 5);
            ApplyCampaignNight(settings);
        }

        public void ApplyCampaignNight(RuntimeGameSettings settings) {
            if (settings == null) return;
            if (playMode != NightPlayMode.Campaign) return;

            CampaignNightPresets.ApplyNight(settings, currentCampaignNight);
            settings.SaveToJson();
        }

        public string GetIntroText() {
            if (playMode == NightPlayMode.Campaign) {
                return $"Night {currentCampaignNight}\n12:00 AM";
            }

            if (playMode == NightPlayMode.Presentation) {
                return "Presentation Mode\n12:00 AM";
            }

            return "Custom Night\n12:00 AM";
        }

        public bool CanContinueCampaign() {
            return NightProgressSave.CanContinueCampaign();
        }

        public int GetContinueNightNumber() {
            return NightProgressSave.GetContinueNightNumber();
        }

        public bool IsCustomNightUnlocked() {
            return NightProgressSave.IsCustomNightUnlocked();
        }

        public int GetStarsEarned() {
            return NightProgressSave.GetStarsEarned();
        }

        public bool IsCurrentRunStarEligible(RuntimeGameSettings settings) {
            if (settings == null) return false;

            if (playMode == NightPlayMode.Campaign) {
                return settings.AreStarRelevantSettingsAtDefaults();
            }

            if (playMode == NightPlayMode.CustomNight) {
                bool AllowCustomNightKeys(string key) =>
                    key == "stalker.ai" ||
                    key == "lostGirl.ai" ||
                    key == "mimic.ai" ||
                    key == "mold.ai" ||
                    key == "paper.secondsToWin" ||
                    key == "paper.glyphScale" ||
                    key == "paper.textPreset";

                return settings.AreStarRelevantSettingsAtDefaults(AllowCustomNightKeys);
            }

            // Presentation mode does not currently award stars.
            return false;
        }

        public bool DoesCurrentCustomNightMeetThreshold(int minimumAI, RuntimeGameSettings settings) {
            if (settings == null) return false;

            return settings.GetInt("stalker.ai") >= minimumAI
                && settings.GetInt("lostGirl.ai") >= minimumAI
                && settings.GetInt("mimic.ai") >= minimumAI
                && settings.GetInt("mold.ai") >= minimumAI;
        }
    }
}