using FNaS.Settings;
using UnityEngine;

namespace FNaS.Systems {
    public static class CampaignNightPresets {
        public static void ApplyNight(RuntimeGameSettings settings, int nightNumber) {
            if (settings == null) return;

            nightNumber = Mathf.Clamp(nightNumber, 1, 5);

            switch (nightNumber) {
                case 1:
                    settings.SetInt("stalker.ai", 4);
                    settings.SetInt("lostGirl.ai", 0);
                    settings.SetInt("mimic.ai", 1);
                    settings.SetInt("mold.ai", 0);

                    settings.SetFloat("paper.secondsToWin", 30f);
                    settings.SetFloat("paper.glyphScale", 0.40f);
                    settings.SetInt("paper.textPreset", 0);
                    break;

                case 2:
                    settings.SetInt("stalker.ai", 6);
                    settings.SetInt("lostGirl.ai", 0);
                    settings.SetInt("mimic.ai", 2);
                    settings.SetInt("mold.ai", 2);

                    settings.SetFloat("paper.secondsToWin", 40f);
                    settings.SetFloat("paper.glyphScale", 0.40f);
                    settings.SetInt("paper.textPreset", 1);
                    break;

                case 3:
                    settings.SetInt("stalker.ai", 8);
                    settings.SetInt("lostGirl.ai", 4);
                    settings.SetInt("mimic.ai", 4);
                    settings.SetInt("mold.ai", 4);

                    settings.SetFloat("paper.secondsToWin", 50f);
                    settings.SetFloat("paper.glyphScale", 0.40f);
                    settings.SetInt("paper.textPreset", 2);
                    break;

                case 4:
                    settings.SetInt("stalker.ai", 12);
                    settings.SetInt("lostGirl.ai", 8);
                    settings.SetInt("mimic.ai", 6);
                    settings.SetInt("mold.ai", 8);

                    settings.SetFloat("paper.secondsToWin", 60f);
                    settings.SetFloat("paper.glyphScale", 0.40f);
                    settings.SetInt("paper.textPreset", 3);
                    break;

                case 5:
                    settings.SetInt("stalker.ai", 16);
                    settings.SetInt("lostGirl.ai", 12);
                    settings.SetInt("mimic.ai", 10);
                    settings.SetInt("mold.ai", 12);

                    settings.SetFloat("paper.secondsToWin", 80f);
                    settings.SetFloat("paper.glyphScale", 0.40f);
                    settings.SetInt("paper.textPreset", 4);
                    break;
            }
        }
    }
}