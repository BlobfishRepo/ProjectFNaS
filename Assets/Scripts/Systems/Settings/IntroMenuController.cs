using FNaS.Settings;
using FNaS.UI.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace FNaS.UI {
    public class IntroMenuController : MonoBehaviour {
        [Header("Scene")]
        [SerializeField] private string nextSceneName = "SceneGameplay";

        [Header("References")]
        [SerializeField] private SettingsMenuBuilder menuBuilder;

        private RuntimeGameSettings runtimeSettings;
        private PlayerInputActions inputActions;

        private void Awake() {
            inputActions = new PlayerInputActions();
        }

        private void OnEnable() {
            inputActions.Enable();
            inputActions.Player.ToggleDebug.performed += OnToggleDebug;
        }

        private void OnDisable() {
            if (inputActions != null) {
                inputActions.Player.ToggleDebug.performed -= OnToggleDebug;
                inputActions.Disable();
            }
        }

        private void Start() {
            runtimeSettings = RuntimeGameSettings.Instance;

            if (runtimeSettings == null) {
                Debug.LogError("IntroMenuController: RuntimeGameSettings not found.");
                enabled = false;
                return;
            }

            RebuildMenu();
        }

        private void OnToggleDebug(InputAction.CallbackContext ctx) {
            runtimeSettings.DebugMenuUnlocked = !runtimeSettings.DebugMenuUnlocked;
            runtimeSettings.SaveToJson();
            RebuildMenu();
        }

        private void RebuildMenu() {
            if (menuBuilder == null || runtimeSettings == null)
                return;

            menuBuilder.Rebuild(runtimeSettings, runtimeSettings.DebugMenuUnlocked);
        }

        public void OnPlayPressed() {
            runtimeSettings.SaveToJson();
            SceneManager.LoadScene(nextSceneName);
        }

        public void OnResetDefaultsPressed() {
            var settings = RuntimeGameSettings.Instance;
            if (settings == null) {
                Debug.LogWarning("IntroMenuController: RuntimeGameSettings not found.");
                return;
            }

            settings.ResetToDefaults();
            settings.SaveToJson();

            if (menuBuilder != null) {
                menuBuilder.Rebuild(settings, settings.DebugMenuUnlocked);
            }
        }
    }
}