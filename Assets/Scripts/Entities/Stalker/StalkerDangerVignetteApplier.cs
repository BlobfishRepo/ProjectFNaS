using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using FNaS.Entities.Stalker;

namespace FNaS.Systems {
    [RequireComponent(typeof(Volume))]
    public class StalkerDangerVignetteApplier : MonoBehaviour {
        [SerializeField] private Volume volume;
        [SerializeField] private StalkerEntity stalker;

        [Header("Colors")]
        [SerializeField] private Color safeColor = Color.black;
        [SerializeField] private Color dangerColor = Color.red;

        [Header("Intensity")]
        [SerializeField] private float safeIntensity = 0.15f;
        [SerializeField] private float dangerIntensity = 0.45f;

        [Header("Smoothing")]
        [SerializeField] private float riseSpeed = 8f;
        [SerializeField] private float fallSpeed = 4f;

        private Vignette vignette;
        private float currentPressure;

        private void Awake() {
            ResolveReferences();
        }

        private void Update() {
            if (volume == null || !volume) {
                volume = null;
                vignette = null;
                return;
            }

            if (stalker == null || !stalker) {
                stalker = FindFirstObjectByType<StalkerEntity>();
            }

            if (vignette == null && volume.profile != null) {
                volume.profile.TryGet(out vignette);
            }

            if (vignette == null || stalker == null || !stalker) return;

            float target = stalker.DangerPressure01;
            float speed = target > currentPressure ? riseSpeed : fallSpeed;
            currentPressure = Mathf.MoveTowards(currentPressure, target, speed * Time.deltaTime);

            vignette.color.Override(Color.Lerp(safeColor, dangerColor, currentPressure));
            vignette.intensity.Override(Mathf.Lerp(safeIntensity, dangerIntensity, currentPressure));
        }

        private void ResolveReferences() {
            if (volume == null) volume = GetComponent<Volume>();

            if (stalker == null) {
                stalker = FindFirstObjectByType<StalkerEntity>();
            }

            if (volume != null && volume.profile != null) {
                volume.profile.TryGet(out vignette);
            }
        }
    }
}