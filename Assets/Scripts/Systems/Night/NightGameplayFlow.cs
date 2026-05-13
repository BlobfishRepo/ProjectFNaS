using System.Collections;
using FNaS.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FNaS.Systems {
    public class NightGameplayFlow : MonoBehaviour {
        [Header("References")]
        public WinState winState;

        [Header("Timing")]
        public float delayAfterWinSeconds = 1.5f;

        private bool handled;

        private void Update() {
            if (handled) return;
            if (winState == null || !winState.hasWon) return;

            NightSessionManager session = NightSessionManager.Instance;
            if (session == null) return;

            handled = true;
            StartCoroutine(HandleWin());
        }

        private IEnumerator HandleWin() {
            NightSessionManager session = NightSessionManager.Instance;
            RuntimeGameSettings settings = RuntimeGameSettings.Instance;

            if (session == null) yield break;

            if (session.PlayMode == NightPlayMode.Campaign) {
                if (delayAfterWinSeconds > 0f) {
                    yield return new WaitForSecondsRealtime(delayAfterWinSeconds);
                }

                Time.timeScale = 1f;

                int finishedNight = session.CurrentCampaignNight;
                NightProgressSave.MarkNightCompleted(finishedNight);

                if (finishedNight >= 5) {
                    session.ClearSession();
                    SceneManager.LoadScene(session.introSceneName);
                    yield break;
                }

                session.AdvanceCampaign(settings);
                SceneManager.LoadScene(session.gameplaySceneName);
                yield break;
            }

            if (session.PlayMode == NightPlayMode.CustomNight) {
                bool eligible = session.IsCurrentRunStarEligible(settings);

                if (eligible) {
                    if (session.DoesCurrentCustomNightMeetThreshold(session.CustomNightStar2MinimumAI, settings)) {
                        NightProgressSave.AwardCustomNightThreshold1Star();
                    }

                    if (session.DoesCurrentCustomNightMeetThreshold(session.CustomNightStar3MinimumAI, settings)) {
                        NightProgressSave.AwardCustomNightThreshold2Star();
                    }
                }

                yield break;
            }

            if (session.PlayMode == NightPlayMode.Presentation) {
                yield break;
            }
        }
    }
}