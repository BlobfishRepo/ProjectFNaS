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
        if (cams != null) {
            for (int i = 0; i < cams.Length; i++) {
                var c = cams[i]?.cam;
                if (c == null) continue;
                c.enabled = false;
                var al = c.GetComponent<AudioListener>();
                if (al != null) al.enabled = false;
            }
        }

        // Cameras are “powered” in your design
        if (attentionState != null) attentionState.isCameraActive = true;

        // Silent boot (no SFX)
        SetActiveCam(0, playSfx: false);
    }

    public void ActivateCam(int index) {
        SetActiveCam(index, playSfx: true);
    }

    private void SetActiveCam(int index, bool playSfx) {
        if (cams == null || cams.Length == 0) return;
        if (index < 0 || index >= cams.Length) return;
        if (cams[index] == null || cams[index].cam == null) return;

        if (activeIndex >= 0 && activeIndex < cams.Length && cams[activeIndex]?.cam != null) {
            cams[activeIndex].cam.enabled = false;
            var oldAl = cams[activeIndex].cam.GetComponent<AudioListener>();
            if (oldAl != null) oldAl.enabled = false;
        }

        if (playSfx && uiSource != null && camSwitchClip != null)
            uiSource.PlayOneShot(camSwitchClip, 1.0f);

        cams[index].cam.enabled = true;
        var newAl = cams[index].cam.GetComponent<AudioListener>();
        if (newAl != null) newAl.enabled = true;

        activeIndex = index;

        if (attentionState != null) {
            attentionState.isCameraActive = true; // always powered
            attentionState.activeCameraNode = cams[index].masterNode;
        }
    }
}