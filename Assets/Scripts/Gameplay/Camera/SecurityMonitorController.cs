using System;
using UnityEngine;
using FNaS.Systems;
using FNaS.MasterNodes;

public class SecurityMonitorController : MonoBehaviour {
    [Serializable]
    public class CamBinding {
        public Camera cam;
        public MasterNode masterNode;
    }

    [Header("Cameras")]
    public CamBinding[] cams;

    [Header("Systems")]
    public GameAttentionState attentionState;

    [Header("Audio")]
    public AudioSource uiSource;
    public AudioClip camSwitchClip;

    private int activeIndex = -1;

    private void Start() {
        // Disable all, then enable default.
        for (int i = 0; i < cams.Length; i++) {
            var c = cams[i]?.cam;
            if (c == null) continue;
            c.enabled = false;
            var al = c.GetComponent<AudioListener>();
            if (al != null) al.enabled = false;
        }

        // Cameras are “powered” in your design
        if (attentionState != null) attentionState.isCameraActive = true;

        if (cams != null && cams.Length > 0)
            ActivateCam(0);
    }

    public void ActivateCam(int index) {
        if (cams == null || cams.Length == 0) return;
        if (index < 0 || index >= cams.Length) return;
        if (cams[index] == null || cams[index].cam == null) return;

        if (activeIndex >= 0 && activeIndex < cams.Length && cams[activeIndex]?.cam != null)
            cams[activeIndex].cam.enabled = false;

        uiSource.PlayOneShot(camSwitchClip, 1.0f);
        cams[index].cam.enabled = true;
        activeIndex = index;

        if (attentionState != null) {
            attentionState.isCameraActive = true; // always powered
            attentionState.activeCameraNode = cams[index].masterNode;
        }
    }
}