using FNaS.Settings;

namespace FNaS.Systems {
    public static class CustomNightPresets {
        public static void Apply(RuntimeGameSettings settings) {
            if (settings == null) return;

            // Preserve current values from dev/settings menu
            float secondsToWin = settings.GetFloat("paper.secondsToWin");
            float glyphScale = settings.GetFloat("paper.glyphScale");
            int textPreset = settings.GetInt("paper.textPreset");

            // Re-apply them (instead of overwriting with fixed values)
            settings.SetFloat("paper.secondsToWin", secondsToWin);
            settings.SetFloat("paper.glyphScale", glyphScale);
            settings.SetInt("paper.textPreset", textPreset);

            settings.SaveToJson();
        }
    }
}