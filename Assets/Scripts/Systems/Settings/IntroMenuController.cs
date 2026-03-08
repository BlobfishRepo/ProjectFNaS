using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using FNaS.Settings;

public class IntroMenuController : MonoBehaviour {
    [Header("Scene")]
    [SerializeField] private string nextSceneName = "Night1";

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
        if (runtimeSettings == null)
            runtimeSettings = RuntimeGameSettings.Instance;

        if (runtimeSettings == null) {
            Debug.LogError("IntroMenuController: RuntimeGameSettings not found.");
            enabled = false;
            return;
        }

        debugVisible = false;
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

        if (debugPanel != null)
            debugPanel.SetActive(debugVisible);
    }

    private void RefreshUIFromSettings() {
        if (playerMoveSpeedSlider != null)
            playerMoveSpeedSlider.value = runtimeSettings.playerMoveSpeed;
        SetFloatText(playerMoveSpeedValueText, runtimeSettings.playerMoveSpeed);

        if (doorMaxDistanceSlider != null)
            doorMaxDistanceSlider.value = runtimeSettings.doorMaxDistance;
        SetFloatText(doorMaxDistanceValueText, runtimeSettings.doorMaxDistance);

        if (stalkerAISlider != null)
            stalkerAISlider.value = runtimeSettings.stalkerAI;
        SetIntText(stalkerAIValueText, runtimeSettings.stalkerAI);

        if (opportunityIntervalSlider != null)
            opportunityIntervalSlider.value = runtimeSettings.opportunityInterval;
        SetFloatText(opportunityIntervalValueText, runtimeSettings.opportunityInterval);

        if (playerMovementDropdown != null)
            playerMovementDropdown.value = (int)runtimeSettings.playerMovementMode;

        if (stalkerMovementDropdown != null)
            stalkerMovementDropdown.value = (int)runtimeSettings.stalkerMovementMode;
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