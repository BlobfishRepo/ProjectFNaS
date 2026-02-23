using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SecurityMonitor : MonoBehaviour {
    [Header("Feed")]
    [SerializeField] private RenderTexture feedTexture;
    [SerializeField] private RawImage feedImage;

    [Header("Security Cameras")]
    [SerializeField] private List<Camera> securityCameras = new List<Camera>();
    [SerializeField] private int startCameraIndex = 0;

    [Header("UI Root")]
    [SerializeField] private Canvas monitorCanvas; // world-space canvas root

    [Header("Controls")]
    [Tooltip("Key to open/close the monitor.")]
    [SerializeField] private Key toggleKey = Key.Tab;

    [Tooltip("Set true when player is allowed to use the monitor (e.g., only at desk node).")]
    public bool canUseMonitor = true;

    [Header("Disable While Monitor Open (optional)")]
    [Tooltip("Drop your movement/controller scripts here to disable while the monitor is open.")]
    [SerializeField] private MonoBehaviour[] disableWhenOpen;

    private bool isOpen;
    private int currentIndex;

    private void Awake() {
        if (monitorCanvas != null) {
            monitorCanvas.enabled = false;
        }

        currentIndex = Mathf.Clamp(startCameraIndex, 0, Mathf.Max(0, securityCameras.Count - 1));
        ApplyCameraTarget();
    }

    private void Update() {
        if (!canUseMonitor) return;

        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame) {
            SetOpen(!isOpen);
        }
    }

    public void SetOpen(bool open) {
        isOpen = open;

        if (monitorCanvas != null) {
            monitorCanvas.enabled = isOpen;
        }

        // Cursor + player control behavior
        if (isOpen) {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (disableWhenOpen != null) {
                for (int i = 0; i < disableWhenOpen.Length; i++) {
                    if (disableWhenOpen[i] != null) disableWhenOpen[i].enabled = false;
                }
            }
        }
        else {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (disableWhenOpen != null) {
                for (int i = 0; i < disableWhenOpen.Length; i++) {
                    if (disableWhenOpen[i] != null) disableWhenOpen[i].enabled = true;
                }
            }
        }
    }

    public void SwitchToCamera(int index) {
        if (securityCameras == null || securityCameras.Count == 0) return;
        currentIndex = Mathf.Clamp(index, 0, securityCameras.Count - 1);
        ApplyCameraTarget();
    }

    public void NextCamera() {
        if (securityCameras == null || securityCameras.Count == 0) return;
        currentIndex = (currentIndex + 1) % securityCameras.Count;
        ApplyCameraTarget();
    }

    private void ApplyCameraTarget() {
        // Make sure the RawImage is showing the feed texture
        if (feedImage != null && feedTexture != null) {
            feedImage.texture = feedTexture;
        }

        // Disable all cameras, enable only active one and bind RT
        for (int i = 0; i < securityCameras.Count; i++) {
            Camera cam = securityCameras[i];
            if (cam == null) continue;

            bool active = (i == currentIndex);
            cam.enabled = active;

            if (active) {
                cam.targetTexture = feedTexture;
            }
            else {
                cam.targetTexture = null;
            }
        }
    }
}