using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using FNaS.Settings;

public class IntroMenuController : MonoBehaviour {
    [Header("Scene")]
    [SerializeField] private string nextSceneName = "SceneGameplay";

    [Header("References")]
    [SerializeField] private RuntimeGameSettings runtimeSettings;
    [SerializeField] private GameObject debugPanel;

    [Header("Standard Settings")]
    [SerializeField] private Slider playerMoveSpeedSlider;
    [SerializeField] private TMP_Text playerMoveSpeedValueText;

    [SerializeField] private Slider doorMaxDistanceSlider;
    [SerializeField] private TMP_Text doorMaxDistanceValueText;

    [SerializeField] private Slider stalkerAISlider;
    [SerializeField] private TMP_Text stalkerAIValueText;

    [SerializeField] private Slider opportunityIntervalSlider;
    [SerializeField] private TMP_Text opportunityIntervalValueText;

    [SerializeField] private Slider maxBatterySecondsSlider;
    [SerializeField] private TMP_Text maxBatterySecondsValueText;

    [Header("Stalker - Freeze Rules")]

    [SerializeField] private Toggle freezeOnCameraToggle;
    [SerializeField] private Toggle freezeInPersonToggle;
    [SerializeField] private Toggle allowShareNodeToggle;

    [Header("Debug Settings")]
    [SerializeField] private TMP_Dropdown playerMovementDropdown;
    [SerializeField] private TMP_Dropdown stalkerMovementDropdown;

    private PlayerInputActions inputActions;
    private bool debugVisible;
    private bool hasBoundUi;

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

        debugVisible = runtimeSettings.debugMenuUnlocked;
        if (debugPanel != null)
            debugPanel.SetActive(debugVisible);

        RefreshUIFromSettings();

        if (!hasBoundUi) {
            BindUI();
            hasBoundUi = true;
        }
    }

    private void OnToggleDebug(InputAction.CallbackContext ctx) {
        debugVisible = !debugVisible;
        runtimeSettings.debugMenuUnlocked = debugVisible;

        if (debugPanel != null)
            debugPanel.SetActive(debugVisible);
    }

    private void RefreshUIFromSettings() {
        if (playerMoveSpeedSlider != null)
            playerMoveSpeedSlider.SetValueWithoutNotify(runtimeSettings.playerMoveSpeed);
        SetFloatText(playerMoveSpeedValueText, runtimeSettings.playerMoveSpeed);

        if (doorMaxDistanceSlider != null)
            doorMaxDistanceSlider.SetValueWithoutNotify(runtimeSettings.doorMaxDistance);
        SetFloatText(doorMaxDistanceValueText, runtimeSettings.doorMaxDistance);

        if (stalkerAISlider != null)
            stalkerAISlider.SetValueWithoutNotify(runtimeSettings.stalkerAI);
        SetIntText(stalkerAIValueText, runtimeSettings.stalkerAI);

        if (opportunityIntervalSlider != null)
            opportunityIntervalSlider.SetValueWithoutNotify(runtimeSettings.opportunityInterval);
        SetFloatText(opportunityIntervalValueText, runtimeSettings.opportunityInterval);

        if (maxBatterySecondsSlider != null)
            maxBatterySecondsSlider.SetValueWithoutNotify(runtimeSettings.maxBatterySeconds);
        SetFloatText(maxBatterySecondsValueText, runtimeSettings.maxBatterySeconds);

        if (playerMovementDropdown != null)
            playerMovementDropdown.SetValueWithoutNotify((int)runtimeSettings.playerMovementMode);

        if (stalkerMovementDropdown != null)
            stalkerMovementDropdown.SetValueWithoutNotify((int)runtimeSettings.stalkerMovementMode);

        if (freezeOnCameraToggle != null)
            freezeOnCameraToggle.isOn = runtimeSettings.freezeIfSeenOnCamera;

        if (freezeInPersonToggle != null)
            freezeInPersonToggle.isOn = runtimeSettings.freezeIfSeenInPerson;

        if (allowShareNodeToggle != null)
            allowShareNodeToggle.isOn = runtimeSettings.allowShareNodeWithPlayer;
    }

    private void BindUI() {
        if (playerMoveSpeedSlider != null) {
            playerMoveSpeedSlider.onValueChanged.AddListener(v => {
                runtimeSettings.playerMoveSpeed = v;
                SetFloatText(playerMoveSpeedValueText, v);
            });
        }

        if (doorMaxDistanceSlider != null) {
            doorMaxDistanceSlider.onValueChanged.AddListener(v => {
                runtimeSettings.doorMaxDistance = v;
                SetFloatText(doorMaxDistanceValueText, v);
            });
        }

        if (stalkerAISlider != null) {
            stalkerAISlider.onValueChanged.AddListener(v => {
                runtimeSettings.stalkerAI = Mathf.RoundToInt(v);
                SetIntText(stalkerAIValueText, runtimeSettings.stalkerAI);
            });
        }

        if (opportunityIntervalSlider != null) {
            opportunityIntervalSlider.onValueChanged.AddListener(v => {
                runtimeSettings.opportunityInterval = v;
                SetFloatText(opportunityIntervalValueText, v);
            });
        }

        if (maxBatterySecondsSlider != null) {
            maxBatterySecondsSlider.onValueChanged.AddListener(v => {
                runtimeSettings.maxBatterySeconds = v;
                SetFloatText(maxBatterySecondsValueText, v);
            });
        }

        if (playerMovementDropdown != null) {
            playerMovementDropdown.onValueChanged.AddListener(v => {
                runtimeSettings.playerMovementMode = (PlayerMovementMode)v;
            });
        }

        if (stalkerMovementDropdown != null) {
            stalkerMovementDropdown.onValueChanged.AddListener(v => {
                runtimeSettings.stalkerMovementMode = (StalkerMovementMode)v;
            });
        }

        if (freezeOnCameraToggle != null) {
            freezeOnCameraToggle.onValueChanged.AddListener(v =>
            {
                runtimeSettings.freezeIfSeenOnCamera = v;
            });
        }

        if (freezeInPersonToggle != null) {
            freezeInPersonToggle.onValueChanged.AddListener(v =>
            {
                runtimeSettings.freezeIfSeenInPerson = v;
            });
        }

        if (allowShareNodeToggle != null) {
            allowShareNodeToggle.onValueChanged.AddListener(v =>
            {
                runtimeSettings.allowShareNodeWithPlayer = v;
            });
        }
    }

    public void OnPlayPressed() {
        SceneManager.LoadScene(nextSceneName);
    }

    private void SetFloatText(TMP_Text textField, float value) {
        if (textField != null)
            textField.text = value.ToString("0.##");
    }

    private void SetIntText(TMP_Text textField, int value) {
        if (textField != null)
            textField.text = value.ToString();
    }
}