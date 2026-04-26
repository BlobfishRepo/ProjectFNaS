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
        [SerializeField] private SettingsMenuBuilder customNightMenuBuilder;
        [SerializeField] private SettingsMenuBuilder playerSettingsMenuBuilder;
        [SerializeField] private SettingsMenuBuilder presentationSettingsMenuBuilder;
        [SerializeField] private SettingsMenuBuilder devGameplayMenuBuilder;
        [SerializeField] private NightSessionManager nightSessionManager;

        [Header("Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject customNightPanel;
        [SerializeField] private GameObject playerSettingsPanel;
        [SerializeField] private GameObject presentationSettingsPanel;
        [SerializeField] private GameObject devGameplayPanel;

        [Header("Buttons")]
        [SerializeField] private Button continueCampaignButton;
        [SerializeField] private TMP_Text continueCampaignButtonText;
        [SerializeField] private string continueLabelFormat = "Continue Night {0}";

        [SerializeField] private Button customNightButton;
        [SerializeField] private TMP_Text customNightButtonText;
        [SerializeField] private string customNightLockedLabel = "Custom Night (Locked)";
        [SerializeField] private string customNightUnlockedLabel = "Custom Night";

        [Header("Stars")]
        [SerializeField] private GameObject star1Object;
        [SerializeField] private GameObject star2Object;
        [SerializeField] private GameObject star3Object;

        [Header("Background")]
        [SerializeField] private GameObject stalkerBackground;

        [Header("Indicators")]
        [SerializeField] private GameObject devSettingsChangedIndicator;
        [SerializeField] private GameObject funSettingsEnabledIndicator;

        private RuntimeGameSettings runtimeSettings;
        private PlayerInputActions inputActions;

        private enum TitlePanel {
            MainMenu,
            CustomNight,
            PlayerSettings,
            PresentationSettings,
            DevGameplay
        }

        private TitlePanel currentPanel = TitlePanel.MainMenu;

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
            ShowMainMenu();
            RefreshAll();
        }

        private void OnToggleDebug(InputAction.CallbackContext ctx) {
            ResolveReferences();
            if (runtimeSettings == null) return;

            if (currentPanel == TitlePanel.PlayerSettings || currentPanel == TitlePanel.PresentationSettings) {
                runtimeSettings.PlayerSettingsDebugUnlocked = !runtimeSettings.PlayerSettingsDebugUnlocked;
                runtimeSettings.SaveToJson();
                RebuildCurrentPanel();
                return;
            }

            if (currentPanel == TitlePanel.CustomNight) {
                runtimeSettings.DebugMenuUnlocked = true;
                runtimeSettings.SaveToJson();
                ShowDevGameplay();
                return;
            }

            if (currentPanel == TitlePanel.DevGameplay) {
                runtimeSettings.DebugMenuUnlocked = false;
                runtimeSettings.SaveToJson();
                ShowCustomNightPreserveCurrent();
                return;
            }
        }

        private void RefreshAll() {
            RefreshContinueButton();
            RefreshCustomNightButton();
            RefreshStars();
            RefreshIndicators();
            RebuildCurrentPanel();
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

        private void RefreshCustomNightButton() {
            bool unlocked = nightSessionManager != null && nightSessionManager.IsCustomNightUnlocked();

            if (customNightButton != null) {
                customNightButton.interactable = unlocked;
            }

            if (customNightButtonText != null) {
                customNightButtonText.text = unlocked ? customNightUnlockedLabel : customNightLockedLabel;
            }
        }

        private void RefreshStars() {
            if (star1Object != null) star1Object.SetActive(NightProgressSave.HasStar1());
            if (star2Object != null) star2Object.SetActive(NightProgressSave.HasStar2());
            if (star3Object != null) star3Object.SetActive(NightProgressSave.HasStar3());
        }

        private void RefreshIndicators() {
            bool showDevChanged = false;
            bool showFunEnabled = false;

            if (runtimeSettings != null) {
                showDevChanged = runtimeSettings.HasNonDefaultStarRelevantDevGameplaySettings();
                showFunEnabled = runtimeSettings.HasNonDefaultFunSettingsEnabled();
            }

            if (devSettingsChangedIndicator != null) {
                devSettingsChangedIndicator.SetActive(showDevChanged);
            }

            if (funSettingsEnabledIndicator != null) {
                funSettingsEnabledIndicator.SetActive(showFunEnabled);
            }
        }

        private void SetActivePanel(TitlePanel panel) {
            currentPanel = panel;

            if (mainMenuPanel != null) mainMenuPanel.SetActive(panel == TitlePanel.MainMenu);
            if (customNightPanel != null) customNightPanel.SetActive(panel == TitlePanel.CustomNight);
            if (playerSettingsPanel != null) playerSettingsPanel.SetActive(panel == TitlePanel.PlayerSettings);
            if (presentationSettingsPanel != null) presentationSettingsPanel.SetActive(panel == TitlePanel.PresentationSettings);
            if (devGameplayPanel != null) devGameplayPanel.SetActive(panel == TitlePanel.DevGameplay);
            if (stalkerBackground != null)
                stalkerBackground.SetActive(panel == TitlePanel.MainMenu);
        }

        private void RebuildCurrentPanel() {
            ResolveReferences();
            if (runtimeSettings == null) return;

            switch (currentPanel) {
                case TitlePanel.CustomNight:
                    if (customNightMenuBuilder != null) {
                        customNightMenuBuilder.Rebuild(runtimeSettings, false, SettingScreen.CustomNight);
                    }
                    break;

                case TitlePanel.PlayerSettings:
                    if (playerSettingsMenuBuilder != null) {
                        SettingScreen playerScreens = SettingScreen.PlayerSettings;
                        if (runtimeSettings.PlayerSettingsDebugUnlocked) {
                            playerScreens |= SettingScreen.PlayerSettingsFun;
                        }

                        playerSettingsMenuBuilder.Rebuild(runtimeSettings, false, playerScreens);
                    }
                    break;

                case TitlePanel.PresentationSettings:
                    if (presentationSettingsMenuBuilder != null) {
                        SettingScreen presentationScreens = SettingScreen.PlayerSettings;
                        if (runtimeSettings.PlayerSettingsDebugUnlocked) {
                            presentationScreens |= SettingScreen.PlayerSettingsFun;
                        }

                        presentationSettingsMenuBuilder.Rebuild(runtimeSettings, false, presentationScreens);
                    }
                    break;

                case TitlePanel.DevGameplay:
                    if (devGameplayMenuBuilder != null) {
                        devGameplayMenuBuilder.Rebuild(runtimeSettings, runtimeSettings.DebugMenuUnlocked, SettingScreen.DevGameplay);
                    }
                    break;
            }

            RefreshIndicators();
        }

        public void ShowMainMenu() {
            SetActivePanel(TitlePanel.MainMenu);
            RefreshAll();
        }

        public void ShowCustomNight() {
            ResolveReferences();
            if (nightSessionManager == null || !nightSessionManager.IsCustomNightUnlocked()) {
                return;
            }

            // Opening Custom Night from the main menu should start from defaults,
            // but preserve all fun.* settings.
            if (runtimeSettings != null) {
                var preservedFunFloats = new System.Collections.Generic.Dictionary<string, float>();
                var preservedFunInts = new System.Collections.Generic.Dictionary<string, int>();
                var preservedFunBools = new System.Collections.Generic.Dictionary<string, bool>();

                foreach (var def in SettingsSchema.Definitions) {
                    if (def == null || string.IsNullOrWhiteSpace(def.key)) continue;
                    if (def.category != SettingCategory.Fun) continue;

                    switch (def.controlType) {
                        case SettingControlType.FloatSlider:
                            preservedFunFloats[def.key] = runtimeSettings.GetFloat(def.key);
                            break;

                        case SettingControlType.IntSlider:
                        case SettingControlType.Dropdown:
                            preservedFunInts[def.key] = runtimeSettings.GetInt(def.key);
                            break;

                        case SettingControlType.Toggle:
                            preservedFunBools[def.key] = runtimeSettings.GetBool(def.key);
                            break;
                    }
                }

                runtimeSettings.ResetToDefaults();

                foreach (var kvp in preservedFunFloats) {
                    runtimeSettings.SetFloat(kvp.Key, kvp.Value);
                }

                foreach (var kvp in preservedFunInts) {
                    runtimeSettings.SetInt(kvp.Key, kvp.Value);
                }

                foreach (var kvp in preservedFunBools) {
                    runtimeSettings.SetBool(kvp.Key, kvp.Value);
                }

                runtimeSettings.SaveToJson();
            }

            SetActivePanel(TitlePanel.CustomNight);
            RefreshAll();
        }

        public void ShowCustomNightPreserveCurrent() {
            ResolveReferences();
            if (nightSessionManager == null || !nightSessionManager.IsCustomNightUnlocked()) {
                return;
            }

            SetActivePanel(TitlePanel.CustomNight);
            RefreshAll();
        }

        public void ShowPlayerSettings() {
            SetActivePanel(TitlePanel.PlayerSettings);
            RefreshAll();
        }

        public void ShowPresentationSettings() {
            SetActivePanel(TitlePanel.PresentationSettings);
            RefreshAll();
        }

        public void ShowDevGameplay() {
            if (currentPanel != TitlePanel.CustomNight && currentPanel != TitlePanel.DevGameplay) {
                return;
            }

            SetActivePanel(TitlePanel.DevGameplay);
            RefreshAll();
        }

        public void OnNewGamePressed() {
            ResolveReferences();
            if (runtimeSettings == null || nightSessionManager == null) return;

            nightSessionManager.BeginNewCampaign(runtimeSettings);
            SceneManager.LoadScene(nextSceneName);
        }

        public void OnContinuePressed() {
            ResolveReferences();
            if (runtimeSettings == null || nightSessionManager == null) return;

            nightSessionManager.BeginContinueCampaign(runtimeSettings);
            SceneManager.LoadScene(nextSceneName);
        }

        public void OnStartCustomNightPressed() {
            ResolveReferences();
            if (runtimeSettings == null || nightSessionManager == null) return;
            if (!nightSessionManager.IsCustomNightUnlocked()) return;

            nightSessionManager.BeginCustomNight(runtimeSettings);
            SceneManager.LoadScene(nextSceneName);
        }

        public void OnStartPresentationPressed() {
            ResolveReferences();
            if (runtimeSettings == null || nightSessionManager == null) return;

            nightSessionManager.BeginPresentationNight(runtimeSettings);
            SceneManager.LoadScene(nextSceneName);
        }

        public void OnResetDefaultsPressed() {
            ResolveReferences();
            if (runtimeSettings == null) return;

            runtimeSettings.ResetToDefaults();
            runtimeSettings.SaveToJson();
            RefreshAll();
        }
    }
}