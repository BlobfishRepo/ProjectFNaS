using FNaS.Settings;
using UnityEngine;

namespace FNaS.Systems {
    public static class PresentationNightPresets {
        public static readonly int[] Thresholds = { 0, 20, 40, 60, 80 };

        public static void ApplyForPercent(RuntimeGameSettings settings, int percentStage) {
            ApplyForPercent(settings, percentStage, true);
        }

        public static void ApplyForPercent(RuntimeGameSettings settings, int percentStage, bool saveToJson) {
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

            if (saveToJson) {
                settings.SaveToJson();
            }
        }

        private static void ApplyStage0(RuntimeGameSettings settings) {
            settings.SetInt("stalker.ai", 0);
            settings.SetInt("lostGirl.ai", 0);
            settings.SetInt("mimic.ai", 0);
            settings.SetInt("mold.ai", 0);

            settings.SetBool("batteryPack.enabled", true);

            float duration = settings.GetBool("paper.presentationShortNight") ? 60f : 120f;
            settings.SetFloat("paper.secondsToWin", duration);

            settings.SetFloat(
                "paper.glyphScale",
                settings.GetFloat("paper.presentationGlyphScale")
            );

            settings.SetInt("paper.textPreset", 7);
        }

        private static void ApplyStage20(RuntimeGameSettings settings) {
            settings.SetInt("stalker.ai", 3);
            settings.SetInt("lostGirl.ai", 0);
            settings.SetInt("mimic.ai", 0);
            settings.SetInt("mold.ai", 0);
        }

        private static void ApplyStage40(RuntimeGameSettings settings) {
            settings.SetInt("stalker.ai", 4);
            settings.SetInt("lostGirl.ai", 0);
            settings.SetInt("mimic.ai", 1);
            settings.SetInt("mold.ai", 6);
        }

        private static void ApplyStage60(RuntimeGameSettings settings) {
            settings.SetInt("stalker.ai", 5);
            settings.SetInt("lostGirl.ai", 5);
            settings.SetInt("mimic.ai", 1);
            settings.SetInt("mold.ai", 6);
        }

        private static void ApplyStage80(RuntimeGameSettings settings) {
            settings.SetInt("stalker.ai", 7);
            settings.SetInt("lostGirl.ai", 5);
            settings.SetInt("mimic.ai", 5);
            settings.SetInt("mold.ai", 6);
        }
    }
}