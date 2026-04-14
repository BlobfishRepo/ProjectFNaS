using System;
using UnityEngine;
using FNaS.Systems;
using FNaS.MasterNodes;

public class SecurityMonitorController : MonoBehaviour {
    [Serializable]
    public class CamBinding {
        public Camera cam;
        public MasterNode masterNode;

        [Tooltip("World-space thing the stalker should look toward when this feed is being watched.")]
        public Transform lookTarget;
    }

    [Header("Cameras")]
    public CamBinding[] cams;

    [Header("Systems")]
    public GameAttentionState attentionState;

    [Header("Audio")]
    public AudioSource uiSource;
    public AudioClip camSwitchClip;

    [Header("Behavior")]
    [Tooltip("If true, no security camera renders unless the monitor is actually in use.")]
    public bool renderOnlyWhenMonitorInUse = true;

    public event Action<int> OnCameraSwitched;

    private int activeIndex = -1;
    private bool lastShouldRender;

    public int ActiveIndex => activeIndex;

    public MasterNode GetActiveMasterNode() {
        if (cams == null) return null;
        if (activeIndex < 0 || activeIndex >= cams.Length) return null;
        return cams[activeIndex] != null ? cams[activeIndex].masterNode : null;
    }

    public Transform GetActiveLookTarget() {
        if (cams == null) return null;
        if (activeIndex < 0 || activeIndex >= cams.Length) return null;
        if (cams[activeIndex] == null) return null;

        return cams[activeIndex].lookTarget != null
            ? cams[activeIndex].lookTarget
            : (cams[activeIndex].cam != null ? cams[activeIndex].cam.transform : null);
    }

    private void Start() {
        DisableAllCameras();

        // Keep a selected camera index, but do not enable it yet unless monitor is in use.
        if (cams != null && cams.Length > 0 && cams[0] != null && cams[0].cam != null) {
            activeIndex = 0;
        }

        ApplyCameraRenderState(force: true);
    }

    private void Update() {
        ApplyCameraRenderState(force: false);
    }

    public void ActivateCam(int index) {
        if (cams == null || cams.Length == 0) return;
        if (index < 0 || index >= cams.Length) return;
        if (cams[index] == null || cams[index].cam == null) return;
        if (activeIndex == index) return;

        activeIndex = index;

        if (uiSource != null && camSwitchClip != null) {
            uiSource.PlayOneShot(camSwitchClip, 1.0f);
        }

        ApplyCameraRenderState(force: true);
        OnCameraSwitched?.Invoke(index);
    }

    private void ApplyCameraRenderState(bool force) {
        bool shouldRender = !renderOnlyWhenMonitorInUse ||
                            (attentionState != null && attentionState.isMonitorInUse);

        if (!force && shouldRender == lastShouldRender) {
            // If monitor usage state didn't change, only need to react if active cam changed.
            // We handle that by forcing ApplyCameraRenderState from ActivateCam().
            return;
        }

        lastShouldRender = shouldRender;

        if (!shouldRender) {
            DisableAllCameras();
            return;
        }

        EnableOnlyActiveCamera();
    }

    private void EnableOnlyActiveCamera() {
        if (cams == null || cams.Length == 0) return;

        for (int i = 0; i < cams.Length; i++) {
            Camera cam = cams[i]?.cam;
            if (cam == null) continue;

            bool enable = (i == activeIndex);
            cam.enabled = enable;

            AudioListener al = cam.GetComponent<AudioListener>();
            if (al != null) {
                al.enabled = enable;
            }
        }
    }

    private void DisableAllCameras() {
        if (cams == null) return;

        for (int i = 0; i < cams.Length; i++) {
            Camera cam = cams[i]?.cam;
            if (cam == null) continue;

            cam.enabled = false;

            AudioListener al = cam.GetComponent<AudioListener>();
            if (al != null) {
                al.enabled = false;
            }
        }
    }
}