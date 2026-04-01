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

    private int activeIndex = -1;

    private void Start() {
        if (cams != null) {
            for (int i = 0; i < cams.Length; i++) {
                var c = cams[i]?.cam;
                if (c == null) continue;
                c.enabled = false;

                var al = c.GetComponent<AudioListener>();
                if (al != null) al.enabled = false;
            }
        }

        if (attentionState != null) {
            attentionState.isCameraActive = false;
            attentionState.isMonitorInUse = false;
            attentionState.activeCameraNode = null;
            attentionState.activeCameraLookTarget = null;
        }

        SetActiveCam(0, playSfx: false);
    }

    public void BeginMonitorUse() {
        if (attentionState != null) {
            attentionState.isMonitorInUse = true;
            attentionState.isCameraActive = activeIndex >= 0;
        }
    }

    public void EndMonitorUse() {
        if (attentionState != null) {
            attentionState.isMonitorInUse = false;
            attentionState.isCameraActive = false;
            attentionState.activeCameraNode = null;
            attentionState.activeCameraLookTarget = null;
        }
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

        if (playSfx && uiSource != null && camSwitchClip != null) {
            uiSource.PlayOneShot(camSwitchClip, 1.0f);
        }

        cams[index].cam.enabled = true;

        var newAl = cams[index].cam.GetComponent<AudioListener>();
        if (newAl != null) newAl.enabled = true;

        activeIndex = index;

        if (attentionState != null) {
            attentionState.isCameraActive = attentionState.isMonitorInUse;
            attentionState.activeCameraNode = cams[index].masterNode;
            attentionState.activeCameraLookTarget =
                cams[index].lookTarget != null ? cams[index].lookTarget : cams[index].cam.transform;
        }
    }
}