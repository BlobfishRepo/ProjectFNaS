using UnityEngine;
using FNaS.Systems;
using FNaS.MasterNodes;

namespace FNaS.Entities.LostGirl {
    public class LostGirlGlassAnchor : MonoBehaviour {
        [Header("Observation")]
        [Tooltip("Used for direct player observation raycasts. If null, falls back to this transform.")]
        public Transform flashlightTarget;

        [Tooltip("If assigned, this glass is considered observed when this camera node is active on the monitor.")]
        public MasterNode cameraNode;

        [Header("Screen FX (optional)")]
        public ScreenFader screenFader;

        [Header("Stage Visuals (0..3)")]
        [Tooltip("Exactly one of these is shown based on the current glass stage.")]
        public GameObject[] stageVisuals;

        [Header("Optional Active Spawn")]
        [Tooltip("If assigned, the Lost Girl active form spawns here when leaving this glass.")]
        public Transform activeSpawnPoint;

        public void ShowStage(int stage) {
            if (stageVisuals == null) return;

            for (int i = 0; i < stageVisuals.Length; i++) {
                if (stageVisuals[i] != null) {
                    stageVisuals[i].SetActive(i == stage);
                }
            }
        }

        public void HideAllStages() {
            if (stageVisuals == null) return;

            for (int i = 0; i < stageVisuals.Length; i++) {
                if (stageVisuals[i] != null) {
                    stageVisuals[i].SetActive(false);
                }
            }
        }

        public void PulseScreen() {
            if (screenFader != null) {
                screenFader.Pulse();
            }
        }
    }
}