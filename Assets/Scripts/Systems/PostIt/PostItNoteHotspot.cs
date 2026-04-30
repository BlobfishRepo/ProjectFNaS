using UnityEngine;
using FNaS.Visuals;

namespace FNaS.Systems {
    public class PostItNoteHotspot : MonoBehaviour {
        public string noteId;
        public Renderer[] worldRenderers;
        public Collider[] clickColliders;

        [Header("Pulse")]
        public WhitePulseMaterialDriver pulseDriver;

        private void Awake() {
            if (worldRenderers == null || worldRenderers.Length == 0) {
                worldRenderers = GetComponentsInChildren<Renderer>(true);
            }

            if (clickColliders == null || clickColliders.Length == 0) {
                clickColliders = GetComponentsInChildren<Collider>(true);
            }

            if (pulseDriver == null) {
                pulseDriver = GetComponentInChildren<WhitePulseMaterialDriver>(true);
            }

            if (pulseDriver != null) {
                pulseDriver.SetPulseEnabled(false);
            }
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

            if (pulseDriver != null) {
                pulseDriver.SetPulseEnabled(visible && glowing);
            }
        }
    }
}