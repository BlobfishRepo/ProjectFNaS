using System;
using System.Collections.Generic;

namespace FNaS.Settings {
    public enum PlayerMovementMode {
        NodeBased,
        FreeRoam
    }

    public enum StalkerMovementMode {
        NodeBased,
        RoamTest
    }

    public enum SettingControlType {
        FloatSlider,
        IntSlider,
        Toggle,
        Dropdown
    }

    public enum SettingCategory {
        Player,
        Door,
        Stalker,
        LostGirl,
        Mimic,
        Mold,
        Paper,
        Flashlight,
        Systems,
        AudioVideo,
        Debug
    }

    [Flags]
    public enum SettingScreen {
        None = 0,

        // Player-facing screens
        CustomNight = 1 << 0,
        PlayerSettings = 1 << 1,

        // Hidden / developer screens
        DevGameplay = 1 << 2,
        PlayerSettingsDebug = 1 << 3,

        Hidden = 1 << 4
    }

    [Serializable]
    public class SettingDefinition {
        public string key;
        public string label;
        public SettingCategory category;
        public SettingControlType controlType;
        public bool debugOnly;

        // Which menus/screens should display this setting.
        public SettingScreen screens;

        // If true, changing away from default can invalidate stars.
        public bool affectsStarEligibility = true;

        public float defaultFloat;
        public int defaultInt;
        public bool defaultBool;

        public float min;
        public float max;

        public string[] dropdownOptions;
    }

    public static class SettingsSchema {
        public static readonly IReadOnlyList<SettingDefinition> Definitions = new List<SettingDefinition> {
            new SettingDefinition {
                key = "player.moveSpeed",
                label = "Player Move Speed",
                category = SettingCategory.Player,
                controlType = SettingControlType.FloatSlider,
                screens = SettingScreen.DevGameplay,
                affectsStarEligibility = true,
                defaultFloat = 7f,
                min = 1f,
                max = 10f
            },
            new SettingDefinition {
                key = "door.maxDistance",
                label = "Door Max Distance",
                category = SettingCategory.Door,
                controlType = SettingControlType.FloatSlider,
                screens = SettingScreen.DevGameplay,
                affectsStarEligibility = true,
                defaultFloat = 6f,
                min = 1f,
                max = 10f
            },

            new SettingDefinition {
                key = "stalker.ai",
                label = "Stalker AI",
                category = SettingCategory.Stalker,
                controlType = SettingControlType.IntSlider,
                screens = SettingScreen.CustomNight | SettingScreen.DevGameplay,
                affectsStarEligibility = false,
                defaultInt = 20,
                min = 0,
                max = 20
            },
            new SettingDefinition {
                key = "stalker.freezeIfSeenOnCamera",
                label = "Stalker Freezes on Camera",
                category = SettingCategory.Stalker,
                controlType = SettingControlType.Toggle,
                screens = SettingScreen.None,
                affectsStarEligibility = true,
                defaultBool = false
            },
            new SettingDefinition {
                key = "stalker.freezeIfSeenInPerson",
                label = "Stalker Freezes When Seen",
                category = SettingCategory.Stalker,
                controlType = SettingControlType.Toggle,
                screens = SettingScreen.None,
                affectsStarEligibility = true,
                defaultBool = true
            },
            new SettingDefinition {
                key = "stalker.allowShareNodeWithPlayer",
                label = "Stalker Can Enter Player Node",
                category = SettingCategory.Stalker,
                controlType = SettingControlType.Toggle,
                screens = SettingScreen.None,
                affectsStarEligibility = true,
                defaultBool = true
            },

            new SettingDefinition {
                key = "lostGirl.ai",
                label = "Lost Girl AI",
                category = SettingCategory.LostGirl,
                controlType = SettingControlType.IntSlider,
                screens = SettingScreen.CustomNight | SettingScreen.DevGameplay,
                affectsStarEligibility = false,
                defaultInt = 20,
                min = 0,
                max = 20
            },
            new SettingDefinition {
                key = "lostGirl.moveSpeed",
                label = "Lost Girl Move Speed",
                category = SettingCategory.LostGirl,
                controlType = SettingControlType.FloatSlider,
                screens = SettingScreen.DevGameplay,
                affectsStarEligibility = true,
                defaultFloat = 8f,
                min = 1f,
                max = 20f
            },

            new SettingDefinition {
                key = "mimic.ai",
                label = "Mimic AI",
                category = SettingCategory.Mimic,
                controlType = SettingControlType.IntSlider,
                screens = SettingScreen.CustomNight | SettingScreen.DevGameplay,
                affectsStarEligibility = false,
                defaultInt = 10,
                min = 0,
                max = 20
            },

            new SettingDefinition {
                key = "mold.ai",
                label = "Mold AI",
                category = SettingCategory.Mold,
                controlType = SettingControlType.IntSlider,
                screens = SettingScreen.CustomNight | SettingScreen.DevGameplay,
                affectsStarEligibility = true,
                defaultInt = 10,
                min = 0,
                max = 20
            },

            new SettingDefinition {
                key = "paper.secondsToWin",
                label = "Paper Duration",
                category = SettingCategory.Paper,
                controlType = SettingControlType.FloatSlider,
                screens = SettingScreen.DevGameplay,
                affectsStarEligibility = true,
                defaultFloat = 30f,
                min = 1f,
                max = 300f
            },
            new SettingDefinition {
                key = "paper.glyphScale",
                label = "Paper Glyph Scale",
                category = SettingCategory.Paper,
                controlType = SettingControlType.FloatSlider,
                screens = SettingScreen.DevGameplay,
                affectsStarEligibility = true,
                defaultFloat = 0.4f,
                min = 0.2f,
                max = 1.5f
            },
            new SettingDefinition {
                key = "paper.textPreset",
                label = "Paper Text Preset",
                category = SettingCategory.Paper,
                controlType = SettingControlType.Dropdown,
                screens = SettingScreen.DevGameplay,
                affectsStarEligibility = true,
                defaultInt = 0,
                dropdownOptions = new[] {
                    "Night 1",
                    "Night 2",
                    "Night 3",
                    "Night 4",
                    "Night 5",
                    "Test Short",
                    "Test Long",
                    "Presentation"
                }
            },

            new SettingDefinition {
                key = "globalAI.baseIntervalSeconds",
                label = "Global AI Tick Interval",
                category = SettingCategory.Systems,
                controlType = SettingControlType.FloatSlider,
                screens = SettingScreen.DevGameplay,
                affectsStarEligibility = true,
                defaultFloat = 5f,
                min = 0.01f,
                max = 20f
            },

            new SettingDefinition {
                key = "flashlight.maxBatterySeconds",
                label = "Flashlight Duration",
                category = SettingCategory.Flashlight,
                controlType = SettingControlType.FloatSlider,
                screens = SettingScreen.DevGameplay,
                affectsStarEligibility = true,
                defaultFloat = 120f,
                min = 1f,
                max = 240f
            },

            new SettingDefinition {
                key = "video.brightness",
                label = "Brightness",
                category = SettingCategory.AudioVideo,
                controlType = SettingControlType.FloatSlider,
                screens = SettingScreen.PlayerSettings,
                affectsStarEligibility = false,
                defaultFloat = 1f,
                min = 0.25f,
                max = 2f
            },
            new SettingDefinition {
                key = "audio.masterVolume",
                label = "Master Volume",
                category = SettingCategory.AudioVideo,
                controlType = SettingControlType.FloatSlider,
                screens = SettingScreen.PlayerSettings,
                affectsStarEligibility = false,
                defaultFloat = 1f,
                min = 0f,
                max = 1f
            },

            new SettingDefinition {
                key = "fun.paperWritingSoundMode",
                label = "Writing Sound",
                category = SettingCategory.Debug,
                controlType = SettingControlType.Dropdown,
                screens = SettingScreen.PlayerSettingsDebug,
                affectsStarEligibility = false,
                defaultInt = 0,
                dropdownOptions = new[] { "Normal", "Fun" }
            },
            new SettingDefinition {
                key = "fun.paperWritingUsePitchAdjust",
                label = "Writing Pitch Adjust",
                category = SettingCategory.Debug,
                controlType = SettingControlType.Toggle,
                screens = SettingScreen.PlayerSettingsDebug,
                affectsStarEligibility = false,
                defaultBool = true
            },

            new SettingDefinition {
                key = "debug.playerMovementMode",
                label = "Player Movement Mode",
                category = SettingCategory.Debug,
                controlType = SettingControlType.Dropdown,
                debugOnly = true,
                screens = SettingScreen.None,
                affectsStarEligibility = true,
                defaultInt = (int)PlayerMovementMode.NodeBased,
                dropdownOptions = new[] { "Node Based", "Free Roam" }
            },
            new SettingDefinition {
                key = "debug.stalkerMovementMode",
                label = "Stalker Movement Mode",
                category = SettingCategory.Debug,
                controlType = SettingControlType.Dropdown,
                debugOnly = true,
                screens = SettingScreen.None,
                affectsStarEligibility = true,
                defaultInt = (int)StalkerMovementMode.NodeBased,
                dropdownOptions = new[] { "Node Based", "Roam Test" }
            }
        };

        private static readonly Dictionary<string, SettingDefinition> ByKey = BuildLookup();

        private static Dictionary<string, SettingDefinition> BuildLookup() {
            Dictionary<string, SettingDefinition> dict = new();
            foreach (var def in Definitions) {
                dict[def.key] = def;
            }
            return dict;
        }

        public static bool TryGetDefinition(string key, out SettingDefinition definition) {
            return ByKey.TryGetValue(key, out definition);
        }

        public static bool ShouldShowOnScreen(SettingDefinition def, SettingScreen includedScreens) {
            if (def == null) return false;
            return (def.screens & includedScreens) != 0;
        }

        public static string GetCategoryLabel(SettingCategory category) {
            return category switch {
                SettingCategory.Player => "Player",
                SettingCategory.Door => "Door",
                SettingCategory.Stalker => "Stalker",
                SettingCategory.LostGirl => "Lost Girl",
                SettingCategory.Mimic => "Mimic",
                SettingCategory.Mold => "Mold",
                SettingCategory.Paper => "Paper",
                SettingCategory.Flashlight => "Flashlight",
                SettingCategory.Systems => "Systems",
                SettingCategory.AudioVideo => "Audio / Video",
                SettingCategory.Debug => "Debug",
                _ => category.ToString()
            };
        }
    }
}