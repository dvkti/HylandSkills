using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace HylandSkills.UI
{
    /// <summary>Skill icons embedded in the DLL (Assets/Icons/*.png → HylandSkills.Icons.name.png).</summary>
    public static class Icons
    {
        const string Prefix = "HylandSkills.Icons.";
        static readonly Dictionary<string, byte[]> raw = new Dictionary<string, byte[]>();
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static void Load()
        {
            var asm = Assembly.GetExecutingAssembly();
            foreach (var res in asm.GetManifestResourceNames())
            {
                if (!res.StartsWith(Prefix) || !res.EndsWith(".png")) continue;
                using var s = asm.GetManifestResourceStream(res);
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                raw[res.Substring(Prefix.Length, res.Length - Prefix.Length - 4)] = ms.ToArray();
            }
            Mod.Log.Msg($"Loaded {raw.Count} embedded icons.");
        }

        public static bool Exists(string name) => raw.ContainsKey(name);

        /// <summary>Returns the icon sprite, creating it lazily (textures must be made on the main thread, after Unity is up).</summary>
        public static Sprite Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (cache.TryGetValue(name, out var sp) && sp != null) return sp;
            if (!raw.TryGetValue(name, out var bytes)) return null;
            try
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (!ImageConversion.LoadImage(tex, bytes, false)) return null;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Trilinear;
                tex.anisoLevel = 4;
                tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
                sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                sp.hideFlags = HideFlags.DontUnloadUnusedAsset;
                sp.name = "HS_" + name;
                cache[name] = sp;
                return sp;
            }
            catch (Exception e)
            {
                Mod.Log.Warning($"Icon {name} failed: {e.Message}");
                return null;
            }
        }
    }
}
