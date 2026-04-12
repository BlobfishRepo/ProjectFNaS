using FNaS.Settings;
using UnityEngine;

namespace FNaS.Systems {
    public enum NightPlayMode {
        None,
        Campaign,
        SingleNight
    }

    public class NightSessionManager : MonoBehaviour {
        public static NightSessionManager Instance { get; private set; }

        [Header("Scenes")]
        public string gameplaySceneName = "SceneGameplay";
        public string introSceneName = "SceneSettings";

        [Header("Runtime")]
        [SerializeField] private NightPlayMode playMode = NightPlayMode.None;
        [SerializeField] private int currentCampaignNight = 1;

        public NightPlayMode PlayMode => playMode;
        public int CurrentCampaignNight => currentCampaignNight;

        private void Awake() {
            if (Instance != null && Instance != this) {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        public void BeginNewCampaign(RuntimeGameSettings settings) {
            NightProgressSave.ResetCampaignProgressPreserveStars();

            playMode = NightPlayMode.Campaign;
            currentCampaignNight = 1;

            ApplyCampaignNight(settings);
        }

        public void BeginContinueCampaign(RuntimeGameSettings settings) {
            playMode = NightPlayMode.Campaign;
            currentCampaignNight = NightProgressSave.GetContinueNightNumber();

            ApplyCampaignNight(settings);
        }

        public void BeginSingleNight() {
            playMode = NightPlayMode.SingleNight;
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

            return "Custom Night\n12:00 AM";
        }

        public bool CanContinueCampaign() {
            return NightProgressSave.CanContinueCampaign();
        }

        public int GetContinueNightNumber() {
            return NightProgressSave.GetContinueNightNumber();
        }
    }
}