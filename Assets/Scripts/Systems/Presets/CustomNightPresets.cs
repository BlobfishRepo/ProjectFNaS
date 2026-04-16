using FNaS.Settings;

namespace FNaS.Systems {
    public static class CustomNightPresets {
        public static void Apply(RuntimeGameSettings settings) {
            if (settings == null) return;

            // Fixed hidden paper values for now, outside player control.
            settings.SetFloat("paper.secondsToWin", 120f);
            settings.SetFloat("paper.glyphScale", 0.4f);
            settings.SetInt("paper.textPreset", 0);

            settings.SaveToJson();
        }
    }
}