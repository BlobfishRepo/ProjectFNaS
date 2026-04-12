using System.Collections;
using FNaS.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FNaS.Systems {
    public class NightGameplayFlow : MonoBehaviour {
        [Header("References")]
        public WinState winState;

        [Header("Timing")]
        public float delayAfterCampaignWinSeconds = 1.5f;

        private bool handled;

        private void Update() {
            if (handled) return;
            if (winState == null || !winState.hasWon) return;

            NightSessionManager session = NightSessionManager.Instance;
            if (session == null) return;
            if (session.PlayMode != NightPlayMode.Campaign) return;

            handled = true;
            StartCoroutine(HandleCampaignWin());
        }

        private IEnumerator HandleCampaignWin() {
            if (delayAfterCampaignWinSeconds > 0f) {
                yield return new WaitForSecondsRealtime(delayAfterCampaignWinSeconds);
            }

            NightSessionManager session = NightSessionManager.Instance;
            RuntimeGameSettings settings = RuntimeGameSettings.Instance;

            if (session == null) yield break;

            int finishedNight = session.CurrentCampaignNight;
            NightProgressSave.MarkNightCompleted(finishedNight);

            if (finishedNight >= 5) {
                session.ClearSession();
                SceneManager.LoadScene(session.introSceneName);
                yield break;
            }

            session.AdvanceCampaign(settings);
            SceneManager.LoadScene(session.gameplaySceneName);
        }
    }
}