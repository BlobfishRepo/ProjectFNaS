using UnityEngine;
using FNaS.Settings;

namespace FNaS.Systems {
    public class PresentationNightDirector : MonoBehaviour {
        [Header("References")]
        [SerializeField] private PaperWinProgress paperWinProgress;
        [SerializeField] private RuntimeSettingsApplier runtimeSettingsApplier;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip thresholdClip;
        [SerializeField][Range(0f, 1f)] private float volume = 1f;

        [Header("Debug")]
        [SerializeField] private int appliedStagePercent = -1;
        [SerializeField] private bool verboseLogging = false;

        private bool initializedForPresentation;

        private void Update() {
            NightSessionManager session = NightSessionManager.Instance;

            if (session == null || session.PlayMode != NightPlayMode.Presentation) {
                return; // just wait instead of disabling
            }

            ResolveReferences();

            if (paperWinProgress == null || runtimeSettingsApplier == null) {
                return;
            }

            if (!initializedForPresentation) {
                ApplyStageIfNeeded(0);
                initializedForPresentation = true;
            }

            float progress01 = Mathf.Clamp01(paperWinProgress.GetProgress01());
            int percent = Mathf.FloorToInt(progress01 * 100f);

            if (percent >= 80) ApplyStageIfNeeded(80);
            else if (percent >= 60) ApplyStageIfNeeded(60);
            else if (percent >= 40) ApplyStageIfNeeded(40);
            else if (percent >= 20) ApplyStageIfNeeded(20);
        }

        private void ResolveReferences() {
            if (paperWinProgress == null) {
                paperWinProgress = FindFirstObjectByType<PaperWinProgress>();
            }

            if (runtimeSettingsApplier == null) {
                runtimeSettingsApplier = FindFirstObjectByType<RuntimeSettingsApplier>();
            }
        }

        private void ApplyStageIfNeeded(int stagePercent) {
            if (appliedStagePercent == stagePercent) return;

            RuntimeGameSettings settings = RuntimeGameSettings.Instance;
            if (settings == null) return;

            PresentationNightPresets.ApplyForPercent(settings, stagePercent);

            if (runtimeSettingsApplier != null) {
                runtimeSettingsApplier.ApplyAllSettings();
            }

            if (audioSource != null && thresholdClip != null && stagePercent > 0) {
                audioSource.PlayOneShot(thresholdClip, volume);
            }

            appliedStagePercent = stagePercent;

            if (verboseLogging) {
                Debug.Log($"PresentationNightDirector applied stage {stagePercent}%.", this);
            }
        }
    }
}