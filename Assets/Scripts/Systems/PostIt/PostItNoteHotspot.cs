using UnityEngine;

namespace FNaS.Systems {
    public class PostItNoteHotspot : MonoBehaviour {
        public string noteId;
        public Renderer[] worldRenderers;
        public Collider[] clickColliders;
        public GameObject glowRoot;

        private Vector3 baseScale;
        private Renderer glowRenderer;

        private void Awake() {
            if (worldRenderers == null || worldRenderers.Length == 0) {
                worldRenderers = GetComponentsInChildren<Renderer>(true);
            }

            if (clickColliders == null || clickColliders.Length == 0) {
                clickColliders = GetComponentsInChildren<Collider>(true);
            }

            if (glowRoot != null) {
                glowRenderer = glowRoot.GetComponent<Renderer>();
            }
        }

        private void Update() {
            if (glowRenderer == null || !glowRoot.activeSelf) return;

            float t = Mathf.Sin(Time.time * 3f) * 0.5f + 0.5f;

            Color c = glowRenderer.material.color;
            c.a = Mathf.Lerp(0.3f, 1f, t);
            glowRenderer.material.color = c;
        }

        public void SetPresentation(bool visible, bool glowing) {
            if (worldRenderers != null) {
                for (int i = 0; i < worldRenderers.Length; i++) {
                    if (worldRenderers[i] != null) {
                        worldRenderers[i].enabled = visible;
                    }
                }
            }

            if (clickColliders != null) {
                for (int i = 0; i < clickColliders.Length; i++) {
                    if (clickColliders[i] != null) {
                        clickColliders[i].enabled = visible;
                    }
                }
            }

            if (glowRoot != null) {
                glowRoot.SetActive(visible && glowing);
            }
        }
    }
}