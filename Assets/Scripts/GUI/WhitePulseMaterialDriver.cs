using UnityEngine;

namespace FNaS.Visuals {
    public class WhitePulseMaterialDriver : MonoBehaviour {
        [Header("Targets")]
        [SerializeField] private Renderer[] renderers;

        [Header("Pulse")]
        [SerializeField] private bool pulseEnabled = true;
        [SerializeField] private float pulseSpeed = 3f;
        [Range(0f, 1f)]
        [SerializeField] private float minStrength = 0f;
        [Range(0f, 1f)]
        [SerializeField] private float maxStrength = 0.65f;

        [Header("Shader Property")]
        [SerializeField] private string pulseStrengthProperty = "_PulseStrength";

        private MaterialPropertyBlock block;
        private int pulseStrengthId;

        private void Awake() {
            if (renderers == null || renderers.Length == 0) {
                renderers = GetComponentsInChildren<Renderer>(true);
            }

            block = new MaterialPropertyBlock();
            pulseStrengthId = Shader.PropertyToID(pulseStrengthProperty);
        }

        private void Update() {
            float strength = 0f;

            if (pulseEnabled) {
                float t = Mathf.Sin(Time.time * pulseSpeed) * 0.5f + 0.5f;
                strength = Mathf.Lerp(minStrength, maxStrength, t);
            }

            ApplyStrength(strength);
        }

        public void SetPulseEnabled(bool enabled) {
            pulseEnabled = enabled;

            if (!enabled) {
                ApplyStrength(0f);
            }
        }

        private void ApplyStrength(float strength) {
            if (renderers == null) return;

            for (int i = 0; i < renderers.Length; i++) {
                Renderer r = renderers[i];
                if (r == null) continue;

                r.GetPropertyBlock(block);
                block.SetFloat(pulseStrengthId, strength);
                r.SetPropertyBlock(block);
            }
        }
    }
}