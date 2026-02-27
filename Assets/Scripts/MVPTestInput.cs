using UnityEngine;
using FNaS.Gameplay;
using FNaS.Systems;

public class MVPTestInput : MonoBehaviour {
    public NodeViewController viewController;
    public FlashlightTool flashlight;
    public GameAttentionState attention;

    void Awake() {
        if (!viewController) viewController = FindFirstObjectByType<NodeViewController>();
        if (!flashlight) flashlight = FindFirstObjectByType<FlashlightTool>();
    }

    void Update() {
        if (Input.GetKeyDown(KeyCode.F))
            flashlight?.Toggle();

        if (attention != null && Input.GetKeyDown(KeyCode.C))
            attention.isCameraActive = !attention.isCameraActive;

        if (attention != null && Input.GetKeyDown(KeyCode.W))
            attention.isWorking = !attention.isWorking;

        // Movement is already handled by NodeViewController via your input system,
        // so you can omit manual movement keys here unless you want extra debugging.
    }
}