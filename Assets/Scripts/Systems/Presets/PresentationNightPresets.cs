using FNaS.Settings;
using UnityEngine;

namespace FNaS.Systems {
    public static class PresentationNightPresets {
        public static readonly int[] Thresholds = { 0, 20, 40, 60, 80 };

        public static void ApplyForPercent(RuntimeGameSettings settings, int percentStage) {
            if (settings == null) return;

            // Snap to valid stages only.
            if (percentStage <= 0) {
                ApplyStage0(settings);
            }
            else if (percentStage <= 20) {
                ApplyStage20(settings);
            }
            else if (percentStage <= 40) {
                ApplyStage40(settings);
            }
            else if (percentStage <= 60) {
                ApplyStage60(settings);
            }
            else {
                ApplyStage80(settings);
            }

            settings.SaveToJson();
        }

        private static void ApplyStage0(RuntimeGameSettings settings) {
            settings.SetInt("stalker.ai", 12);
            settings.SetInt("lostGirl.ai", 0);
            settings.SetInt("mimic.ai", 0);
            settings.SetInt("mold.ai", 0);

            // Presentation-specific paper setup.
            settings.SetFloat("paper.secondsToWin", 180f);
            settings.SetFloat("paper.glyphScale", 0.30f);
            settings.SetInt("paper.textPreset", 7);
        }

        private static void ApplyStage20(RuntimeGameSettings settings) {
            settings.SetInt("stalker.ai", 8);
            settings.SetInt("lostGirl.ai", 0);
            settings.SetInt("mimic.ai", 5);
            settings.SetInt("mold.ai", 10);
        }

        private static void ApplyStage40(RuntimeGameSettings settings) {
            settings.SetInt("stalker.ai", 12);
            settings.SetInt("lostGirl.ai", 4);
            settings.SetInt("mimic.ai", 8);
            settings.SetInt("mold.ai", 12);
        }

        private static void ApplyStage60(RuntimeGameSettings settings) {
            settings.SetInt("stalker.ai", 12);
            settings.SetInt("lostGirl.ai", 8);
            settings.SetInt("mimic.ai", 10);
            settings.SetInt("mold.ai", 14);
        }

        private static void ApplyStage80(RuntimeGameSettings settings) {
            settings.SetInt("stalker.ai", 18);
            settings.SetInt("lostGirl.ai", 12);
            settings.SetInt("mimic.ai", 12);
            settings.SetInt("mold.ai", 16);
        }
    }
}