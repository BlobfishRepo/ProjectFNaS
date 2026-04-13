using FNaS.Settings;

namespace FNaS.Systems {
    public interface IRuntimeSettingsConsumer {
        void ApplyRuntimeSettings(RuntimeGameSettings settings);
    }
}