using System.Collections;
using UnityEngine;
using FNaS.Settings;
using FNaS.Entities.Stalker;
using FNaS.Entities.Mimic;
using FNaS.Entities.LostGirl;
using FNaS.Entities.Mold;

namespace FNaS.Systems {
    public class PresentationNightDirector : MonoBehaviour {
        [Header("References")]
        [SerializeField] private PaperWinProgress paperWinProgress;
        [SerializeField] private RuntimeSettingsApplier runtimeSettingsApplier;

        [Header("Live Entities")]
        [SerializeField] private StalkerEntity stalker;
        [SerializeField] private MimicEntity mimic;
        [SerializeField] private LostGirlEntity lostGirl;
        [SerializeField] private MoldManager mold;

        [Header("Forced Spawns")]
        [SerializeField] private float forcedSpawnDelaySeconds = 5f;
        [SerializeField] private bool forceMimicAtStage40 = true;
        [SerializeField] private bool forceLostGirlAtStage60 = true;

        [Header("Stage Feedback")]
        [SerializeField] private ScreenFader stageFader;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip thresholdClip;
        [SerializeField][Range(0f, 1f)] private float volume = 1f;

        [Header("Debug")]
        [SerializeField] private int appliedStagePercent = -1;
        [SerializeField] private bool verboseLogging = false;

        private bool initializedForPresentation;
        private bool mimicSpawnQueued;
        private bool lostGirlSpawnQueued;

        private void Update() {
            NightSessionManager session = NightSessionManager.Instance;

            if (session == null || session.PlayMode != NightPlayMode.Presentation) {
                return;
            }

            ResolveReferences();

            if (paperWinProgress == null) {
                return;
            }

            if (!initializedForPresentation) {
                ApplyStageIfNeeded(0, applyAllSettings: true);
                initializedForPresentation = true;
            }

            float progress01 = Mathf.Clamp01(paperWinProgress.GetProgress01());
            int percent = Mathf.FloorToInt(progress01 * 100f);

            if (percent >= 80) ApplyStageIfNeeded(80, applyAllSettings: false);
            else if (percent >= 60) ApplyStageIfNeeded(60, applyAllSettings: false);
            else if (percent >= 40) ApplyStageIfNeeded(40, applyAllSettings: false);
            else if (percent >= 20) ApplyStageIfNeeded(20, applyAllSettings: false);
        }

        private void ResolveReferences() {
            if (paperWinProgress == null) paperWinProgress = FindFirstObjectByType<PaperWinProgress>();
            if (runtimeSettingsApplier == null) runtimeSettingsApplier = FindFirstObjectByType<RuntimeSettingsApplier>();

            if (stalker == null) stalker = FindFirstObjectByType<StalkerEntity>();
            if (mimic == null) mimic = FindFirstObjectByType<MimicEntity>();
            if (lostGirl == null) lostGirl = FindFirstObjectByType<LostGirlEntity>();
            if (mold == null) mold = FindFirstObjectByType<MoldManager>();

            if (stageFader == null) stageFader = FindFirstObjectByType<ScreenFader>();
        }

        private void ApplyStageIfNeeded(int stagePercent, bool applyAllSettings) {
            if (appliedStagePercent == stagePercent) return;

            RuntimeGameSettings settings = RuntimeGameSettings.Instance;
            if (settings == null) return;

            PresentationNightPresets.ApplyForPercent(settings, stagePercent, saveToJson: stagePercent == 0);

            if (applyAllSettings && runtimeSettingsApplier != null) {
                runtimeSettingsApplier.ApplyAllSettings();
            }
            else {
                ApplyLiveEntityAI(settings);
            }

            if (stagePercent > 0) {
                if (audioSource != null && thresholdClip != null) {
                    audioSource.PlayOneShot(thresholdClip, volume);
                }

                if (stageFader != null) {
                    stageFader.Pulse();
                }
            }

            if (stagePercent >= 40 && forceMimicAtStage40 && !mimicSpawnQueued) {
                mimicSpawnQueued = true;
                StartCoroutine(ForceMimicAfterDelay());
            }

            if (stagePercent >= 60 && forceLostGirlAtStage60 && !lostGirlSpawnQueued) {
                lostGirlSpawnQueued = true;
                StartCoroutine(ForceLostGirlAfterDelay());
            }

            appliedStagePercent = stagePercent;

            if (verboseLogging) {
                Debug.Log($"PresentationNightDirector applied stage {stagePercent}%.", this);
            }
        }

        private void ApplyLiveEntityAI(RuntimeGameSettings settings) {
            if (stalker != null) stalker.ai = settings.GetInt("stalker.ai");
            if (mimic != null) mimic.ai = settings.GetInt("mimic.ai");
            if (lostGirl != null) lostGirl.ai = settings.GetInt("lostGirl.ai");
            if (mold != null) mold.ai = settings.GetInt("mold.ai");
        }

        private IEnumerator ForceMimicAfterDelay() {
            yield return new WaitForSeconds(forcedSpawnDelaySeconds);

            if (mimic != null) {
                mimic.ForceSpawnNow();
            }
        }

        private IEnumerator ForceLostGirlAfterDelay() {
            yield return new WaitForSeconds(forcedSpawnDelaySeconds);

            if (lostGirl != null) {
                lostGirl.ForceSpawnNow();
            }
        }
    }
}