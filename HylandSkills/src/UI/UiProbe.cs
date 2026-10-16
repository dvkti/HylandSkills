using System;
using System.IO;
using System.Text;
using Il2CppTMPro;
using MelonLoader.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace HylandSkills.UI
{
    /// <summary>Development helper: dumps a UI hierarchy (sizes, colours, sprites, fonts) to UserData so the mod can match the native look.</summary>
    public static class UiProbe
    {
        public static void Dump(Transform root, string file, int maxDepth = 9)
        {
            try
            {
                var sb = new StringBuilder();
                Walk(root, 0, maxDepth, sb);
                string dir = Path.Combine(MelonEnvironment.UserDataDirectory, "HylandSkills");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, file), sb.ToString());
                Mod.Log.Msg($"UI dump written: {file}");
            }
            catch (Exception e) { Mod.Log.Warning("UI dump failed: " + e.Message); }
        }

        static void Walk(Transform t, int depth, int maxDepth, StringBuilder sb)
        {
            if (depth > maxDepth) return;
            var pad = new string(' ', depth * 2);
            sb.Append(pad).Append(t.name);
            if (!t.gameObject.activeSelf) sb.Append(" [off]");
            var rt = t.GetComponent<RectTransform>();
            if (rt != null) sb.Append($" rect={rt.rect.width:0}x{rt.rect.height:0} amin={rt.anchorMin} amax={rt.anchorMax}");
            var img = t.GetComponent<Image>();
            if (img != null) sb.Append($" IMG col=#{ColorUtility.ToHtmlStringRGBA(img.color)} spr={(img.sprite != null ? img.sprite.name : "-")} type={img.type}");
            var tmp = t.GetComponent<TextMeshProUGUI>();
            if (tmp != null) sb.Append($" TMP font={(tmp.font != null ? tmp.font.name : "-")} size={tmp.fontSize} col=#{ColorUtility.ToHtmlStringRGBA(tmp.color)} style={tmp.fontStyle} txt=\"{Trim(tmp.text)}\"");
            var txt = t.GetComponent<Text>();
            if (txt != null) sb.Append($" TXT font={(txt.font != null ? txt.font.name : "-")} size={txt.fontSize} col=#{ColorUtility.ToHtmlStringRGBA(txt.color)} style={txt.fontStyle} txt=\"{Trim(txt.text)}\"");
            var hl = t.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (hl != null) sb.Append($" LAYOUT spacing={hl.spacing} pad={hl.padding.left},{hl.padding.right},{hl.padding.top},{hl.padding.bottom}");
            var sh = t.GetComponent<Shadow>();
            if (sh != null) sb.Append($" SHADOW #{ColorUtility.ToHtmlStringRGBA(sh.effectColor)} {sh.effectDistance}");
            sb.AppendLine();
            for (int i = 0; i < t.childCount; i++) Walk(t.GetChild(i), depth + 1, maxDepth, sb);
        }

        static string Trim(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\n", "\\n");
            return s.Length > 40 ? s.Substring(0, 40) + "…" : s;
        }
    }
}
