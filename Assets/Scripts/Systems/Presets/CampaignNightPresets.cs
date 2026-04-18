using FNaS.Settings;
using UnityEngine;

namespace FNaS.Systems {
    public static class CampaignNightPresets {
        public static void ApplyNight(RuntimeGameSettings settings, int nightNumber) {
            if (settings == null) return;

            nightNumber = Mathf.Clamp(nightNumber, 1, 5);

            switch (nightNumber) {
                case 1:
                    settings.SetInt("stalker.ai", 10);
                    settings.SetInt("lostGirl.ai", 0);
                    settings.SetInt("mimic.ai", 0);
                    settings.SetInt("mold.ai", 0);

                    settings.SetFloat("paper.secondsToWin", 120f);
                    settings.SetFloat("paper.glyphScale", 0.40f);
                    settings.SetInt("paper.textPreset", 0);
                    break;

                case 2:
                    settings.SetInt("stalker.ai", 8);
                    settings.SetInt("lostGirl.ai", 0);
                    settings.SetInt("mimic.ai", 4);
                    settings.SetInt("mold.ai", 5);

                    settings.SetFloat("paper.secondsToWin", 150f);
                    settings.SetFloat("paper.glyphScale", 0.40f);
                    settings.SetInt("paper.textPreset", 1);
                    break;

                case 3:
                    settings.SetInt("stalker.ai", 12);
                    settings.SetInt("lostGirl.ai", 5);
                    settings.SetInt("mimic.ai", 3);
                    settings.SetInt("mold.ai", 8);

                    settings.SetFloat("paper.secondsToWin", 180f);
                    settings.SetFloat("paper.glyphScale", 0.40f);
                    settings.SetInt("paper.textPreset", 2);
                    break;

                case 4:
                    settings.SetInt("stalker.ai", 14);
                    settings.SetInt("lostGirl.ai", 7);
                    settings.SetInt("mimic.ai", 8);
                    settings.SetInt("mold.ai", 10);

                    settings.SetFloat("paper.secondsToWin", 210f);
                    settings.SetFloat("paper.glyphScale", 0.40f);
                    settings.SetInt("paper.textPreset", 3);
                    break;

                case 5:
                    settings.SetInt("stalker.ai", 16);
                    settings.SetInt("lostGirl.ai", 10);
                    settings.SetInt("mimic.ai", 10);
                    settings.SetInt("mold.ai", 16);

                    settings.SetFloat("paper.secondsToWin", 240f);
                    settings.SetFloat("paper.glyphScale", 0.40f);
                    settings.SetInt("paper.textPreset", 4);
                    break;
            }
        }
    }
}