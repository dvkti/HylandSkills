using MelonLoader;
using UnityEngine;

namespace HylandSkills.Core
{
    public enum HudAnchor { TopLeft, TopCenter, TopRight, MiddleLeft, MiddleCenter, MiddleRight, BottomLeft, BottomCenter, BottomRight }

    /// <summary>Player-facing settings. Stored in MelonPreferences so they apply to every save.</summary>
    public static class Prefs
    {
        static MelonPreferences_Category cat;

        public static MelonPreferences_Entry<KeyCode>[] SlotKeys;
        public static MelonPreferences_Entry<HudAnchor> Anchor;
        public static MelonPreferences_Entry<float> HudScale;
        public static MelonPreferences_Entry<float> HudOpacity;
        public static MelonPreferences_Entry<bool> ShowEmptySlots;
        public static MelonPreferences_Entry<bool> ShowKeyHints;
        public static MelonPreferences_Entry<bool> PointNotifications;
        public static MelonPreferences_Entry<bool> DebugLogging;

        public static readonly KeyCode[] DefaultKeys = { KeyCode.Z, KeyCode.X, KeyCode.B, KeyCode.N };

        public static void Init()
        {
            cat = MelonPreferences.CreateCategory("HylandSkills", "Hyland Skills");
            SlotKeys = new MelonPreferences_Entry<KeyCode>[4];
            for (int i = 0; i < 4; i++)
                SlotKeys[i] = cat.CreateEntry($"Slot{i + 1}Key", DefaultKeys[i], $"Active slot {i + 1} key");
            Anchor = cat.CreateEntry("HudAnchor", HudAnchor.TopRight, "Ability timers position");
            HudScale = cat.CreateEntry("HudScale", 1f, "Ability timers scale (0.6 - 1.6)");
            HudOpacity = cat.CreateEntry("HudOpacity", 1f, "Ability timers opacity (0.3 - 1)");
            ShowEmptySlots = cat.CreateEntry("ShowEmptySlots", false, "Show empty active slots on the HUD");
            ShowKeyHints = cat.CreateEntry("ShowKeyHints", true, "Show key hints on the HUD");
            PointNotifications = cat.CreateEntry("PointNotifications", true, "Notify when you earn a skill point");
            DebugLogging = cat.CreateEntry("DebugLogging", false, "Verbose logging");
        }

        public static void Save() => cat?.SaveToFile(false);

        public static string KeyLabel(KeyCode k)
        {
            string s = k.ToString();
            if (s.StartsWith("Alpha")) return s.Substring(5);
            if (s.StartsWith("Keypad")) return "Num" + s.Substring(6);
            return s switch
            {
                "Mouse3" => "M4",
                "Mouse4" => "M5",
                "LeftShift" => "LShift",
                "RightShift" => "RShift",
                "LeftControl" => "LCtrl",
                "RightControl" => "RCtrl",
                "LeftAlt" => "LAlt",
                "RightAlt" => "RAlt",
                "BackQuote" => "`",
                _ => s
            };
        }
    }
}
