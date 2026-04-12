using FNaS.Settings;
using FNaS.Systems;
using FNaS.UI.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FNaS.UI {
    public class IntroMenuController : MonoBehaviour {
        [Header("Scene")]
        [SerializeField] private string nextSceneName = "SceneGameplay";

        [Header("References")]
        [SerializeField] private SettingsMenuBuilder menuBuilder;
        [SerializeField] private NightSessionManager nightSessionManager;

        [Header("Optional Continue UI")]
        [SerializeField] private Button continueCampaignButton;
        [SerializeField] private TMP_Text continueCampaignButtonText;
        [SerializeField] private string continueLabelFormat = "Continue Night {0}";

        [Header("Menu Filter")]
        [SerializeField] private SettingScreen introMenuScreens = SettingScreen.IntroDev;

        private RuntimeGameSettings runtimeSettings;
        private PlayerInputActions inputActions;

        private void EnsureInputActions() {
            if (inputActions == null) {
                inputActions = new PlayerInputActions();
            }
        }

        private void ResolveReferences() {
            if (runtimeSettings == null) {
                runtimeSettings = RuntimeGameSettings.Instance;
            }

            if (nightSessionManager == null) {
                nightSessionManager = NightSessionManager.Instance;
            }

            if (nightSessionManager == null) {
                nightSessionManager = FindFirstObjectByType<NightSessionManager>();
            }
        }

        private void OnEnable() {
            EnsureInputActions();
            ResolveReferences();

            inputActions.Enable();
            inputActions.Player.ToggleDebug.performed += OnToggleDebug;
        }

        private void OnDisable() {
            if (inputActions == null) return;

            inputActions.Player.ToggleDebug.performed -= OnToggleDebug;
            inputActions.Disable();
        }

        private void OnDestroy() {
            if (inputActions != null) {
                inputActions.Player.ToggleDebug.performed -= OnToggleDebug;
                inputActions.Disable();
            }
        }

        private void Start() {
            ResolveReferences();
            RebuildMenu();
            RefreshContinueButton();
        }

        private void OnToggleDebug(InputAction.CallbackContext ctx) {
            ResolveReferences();
            if (runtimeSettings == null) return;

            runtimeSettings.DebugMenuUnlocked = !runtimeSettings.DebugMenuUnlocked;
            runtimeSettings.SaveToJson();

            RebuildMenu();
            RefreshContinueButton();
        }

        private void RebuildMenu() {
            ResolveReferences();
            if (menuBuilder == null || runtimeSettings == null) return;

            menuBuilder.Rebuild(
                runtimeSettings,
                runtimeSettings.DebugMenuUnlocked,
                introMenuScreens
            );
        }

        private void RefreshContinueButton() {
            ResolveReferences();

            int night = 1;
            bool canContinue = false;

            if (nightSessionManager != null) {
                night = nightSessionManager.GetContinueNightNumber();
                canContinue = nightSessionManager.CanContinueCampaign();
            }

            if (continueCampaignButton != null) {
                continueCampaignButton.interactable = canContinue;
            }

            if (continueCampaignButtonText != null) {
                continueCampaignButtonText.text = string.Format(continueLabelFormat, night);
            }
        }

        public void OnNewCampaignPressed() {
            ResolveReferences();
            if (runtimeSettings == null || nightSessionManager == null) return;

            nightSessionManager.BeginNewCampaign(runtimeSettings);
            SceneManager.LoadScene(nextSceneName);
        }

        public void OnContinueCampaignPressed() {
            ResolveReferences();
            if (runtimeSettings == null || nightSessionManager == null) return;

            nightSessionManager.BeginContinueCampaign(runtimeSettings);
            SceneManager.LoadScene(nextSceneName);
        }

        public void OnSingleNightPressed() {
            ResolveReferences();
            if (nightSessionManager == null) return;

            nightSessionManager.BeginSingleNight();
            SceneManager.LoadScene(nextSceneName);
        }

        public void OnResetDefaultsPressed() {
            ResolveReferences();
            if (runtimeSettings == null) return;

            runtimeSettings.ResetToDefaults();
            runtimeSettings.SaveToJson();

            RebuildMenu();
            RefreshContinueButton();
        }
    }
}