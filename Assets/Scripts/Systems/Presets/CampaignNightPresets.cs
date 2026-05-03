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
                    settings.SetInt("mimic.ai", 0);
                    settings.SetInt("mold.ai", 0);

                    settings.SetBool("batteryPack.enabled", true);

                    settings.SetFloat("paper.secondsToWin", 120f);
                    settings.SetFloat("paper.glyphScale", 0.40f);
                    settings.SetInt("paper.textPreset", 0);
                    break;

                case 2:
                    settings.SetInt("stalker.ai", 6);
                    settings.SetInt("lostGirl.ai", 0);
                    settings.SetInt("mimic.ai", 4);
                    settings.SetInt("mold.ai", 3);

                    settings.SetBool("batteryPack.enabled", true);

                    settings.SetFloat("paper.secondsToWin", 150f);
                    settings.SetFloat("paper.glyphScale", 0.40f);
                    settings.SetInt("paper.textPreset", 1);
                    break;

                case 3:
                    settings.SetInt("stalker.ai", 8);
                    settings.SetInt("lostGirl.ai", 6);
                    settings.SetInt("mimic.ai", 3);
                    settings.SetInt("mold.ai", 8);

                    settings.SetBool("batteryPack.enabled", true);

                    settings.SetFloat("paper.secondsToWin", 180f);
                    settings.SetFloat("paper.glyphScale", 0.40f);
                    settings.SetInt("paper.textPreset", 2);
                    break;

                case 4:
                    settings.SetInt("stalker.ai", 9);
                    settings.SetInt("lostGirl.ai", 7);
                    settings.SetInt("mimic.ai", 5);
                    settings.SetInt("mold.ai", 10);

                    settings.SetBool("batteryPack.enabled", false);

                    settings.SetFloat("paper.secondsToWin", 195f);
                    settings.SetFloat("paper.glyphScale", 0.40f);
                    settings.SetInt("paper.textPreset", 3);
                    break;

                case 5:
                    settings.SetInt("stalker.ai", 11);
                    settings.SetInt("lostGirl.ai", 10);
                    settings.SetInt("mimic.ai", 10);
                    settings.SetInt("mold.ai", 12);

                    settings.SetBool("batteryPack.enabled", false);

                    settings.SetFloat("paper.secondsToWin", 210f);
                    settings.SetFloat("paper.glyphScale", 0.40f);
                    settings.SetInt("paper.textPreset", 4);
                    break;
            }
        }
    }
}