using UnityEngine;

public class SecurityMonitorController : MonoBehaviour {
    [Header("Cameras (Base cameras that render to the monitor RT)")]
    public Camera[] cams;

    private int activeIndex = -1;

    private void Start() {
        // Disable all, then enable default.
        for (int i = 0; i < cams.Length; i++) {
            if (cams[i] != null) {
                cams[i].enabled = false;
                // Important: security cams should NOT have AudioListener enabled.
                var al = cams[i].GetComponent<AudioListener>();
                if (al != null) al.enabled = false;
            }
        }

        if (cams != null && cams.Length > 0) {
            ActivateCam(0);
        }
    }

    public void ActivateCam(int index) {
        if (cams == null || cams.Length == 0) return;
        if (index < 0 || index >= cams.Length) return;
        if (cams[index] == null) return;

        if (activeIndex >= 0 && activeIndex < cams.Length && cams[activeIndex] != null) {
            cams[activeIndex].enabled = false;
        }

        cams[index].enabled = true;
        activeIndex = index;
    }
}