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
        Flashlight,
        Systems,
        Debug
    }

    [Serializable]
    public class SettingDefinition {
        public string key;
        public string label;
        public SettingCategory category;
        public SettingControlType controlType;
        public bool debugOnly;

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
                defaultFloat = 5f,
                min = 1f,
                max = 10f
            },
            new SettingDefinition {
                key = "door.maxDistance",
                label = "Door Max Distance",
                category = SettingCategory.Door,
                controlType = SettingControlType.FloatSlider,
                defaultFloat = 6f,
                min = 1f,
                max = 10f
            },
            new SettingDefinition {
                key = "stalker.ai",
                label = "Stalker AI",
                category = SettingCategory.Stalker,
                controlType = SettingControlType.IntSlider,
                defaultInt = 20,
                min = 0,
                max = 20
            },
            new SettingDefinition {
                key = "stalker.freezeIfSeenOnCamera",
                label = "Stalker Freezes on Camera",
                category = SettingCategory.Stalker,
                controlType = SettingControlType.Toggle,
                defaultBool = false
            },
            new SettingDefinition {
                key = "stalker.freezeIfSeenInPerson",
                label = "Stalker Freezes When Seen",
                category = SettingCategory.Stalker,
                controlType = SettingControlType.Toggle,
                defaultBool = true
            },
            new SettingDefinition {
                key = "stalker.allowShareNodeWithPlayer",
                label = "Stalker Can Enter Player Node",
                category = SettingCategory.Stalker,
                controlType = SettingControlType.Toggle,
                defaultBool = true
            },
            new SettingDefinition {
                key = "lostGirl.ai",
                label = "Lost Girl AI",
                category = SettingCategory.LostGirl,
                controlType = SettingControlType.IntSlider,
                defaultInt = 20,
                min = 0,
                max = 20
            },
            new SettingDefinition {
                key = "lostGirl.moveSpeed",
                label = "Lost Girl Move Speed",
                category = SettingCategory.LostGirl,
                controlType = SettingControlType.FloatSlider,
                defaultFloat = 8f,
                min = 1f,
                max = 20f
            },
            new SettingDefinition {
                key = "mimic.ai",
                label = "Mimic AI",
                category = SettingCategory.Mimic,
                controlType = SettingControlType.IntSlider,
                defaultInt = 10,
                min = 0,
                max = 20
            },
            new SettingDefinition {
                key = "globalAI.baseIntervalSeconds",
                label = "Global AI Tick Interval",
                category = SettingCategory.Systems,
                controlType = SettingControlType.FloatSlider,
                defaultFloat = 5f,
                min = 0.01f,
                max = 20f
            },
            new SettingDefinition {
                key = "flashlight.maxBatterySeconds",
                label = "Flashlight Duration",
                category = SettingCategory.Flashlight,
                controlType = SettingControlType.FloatSlider,
                defaultFloat = 60f,
                min = 1f,
                max = 120f
            },
            new SettingDefinition {
                key = "debug.playerMovementMode",
                label = "Player Movement Mode",
                category = SettingCategory.Debug,
                controlType = SettingControlType.Dropdown,
                debugOnly = true,
                defaultInt = (int)PlayerMovementMode.NodeBased,
                dropdownOptions = new[] { "Node Based", "Free Roam" }
            },
            new SettingDefinition {
                key = "debug.stalkerMovementMode",
                label = "Stalker Movement Mode",
                category = SettingCategory.Debug,
                controlType = SettingControlType.Dropdown,
                debugOnly = true,
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

        public static string GetCategoryLabel(SettingCategory category) {
            return category switch {
                SettingCategory.Player => "Player",
                SettingCategory.Door => "Door",
                SettingCategory.Stalker => "Stalker",
                SettingCategory.LostGirl => "Lost Girl",
                SettingCategory.Mimic => "Mimic",
                SettingCategory.Flashlight => "Flashlight",
                SettingCategory.Systems => "Systems",
                SettingCategory.Debug => "Debug",
                _ => category.ToString()
            };
        }
    }
}