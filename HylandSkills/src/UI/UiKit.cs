using System;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HylandSkills.UI
{
    /// <summary>Small uGUI toolkit: procedural sprites, the game's own font and layout helpers.</summary>
    public static class UiKit
    {
        // Palette tuned against the native phone apps (see Theme.ApplyFromGame).
        public static class Theme
        {
            // Values taken from the native phone apps (Products / Deliveries / Dealers).
            public static Color Background = Hex("323232");
            public static Color Surface = Hex("282828");
            public static Color SurfaceHi = Hex("3C3C3C");
            public static Color Card = Hex("202020");
            public static Color Stroke = Hex("4F4F4F");
            public static Color Text = Hex("FFFFFF");
            public static Color TextDim = Hex("A0A0A0");
            public static Color TextFaint = Hex("787878");
            public static Color Accent = Hex("5EC341");
            public static Color Warn = Hex("C55E55");
            public static Color Gold = Hex("FFDE55");
            public static Color Locked = Hex("4A4A4A");
            public static Color Header = Hex("1D5246");
            public static Color HeaderText = Hex("E3FFF6");
        }

        static Sprite round8, round16, round4, circle, ring, ringThin, softGlow, diamond, diamondRing, facets, sparkle;
        static TMP_FontAsset font, fontBold;
        static readonly System.Collections.Generic.Dictionary<string, Sprite> nativeSprites = new System.Collections.Generic.Dictionary<string, Sprite>();

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }

        public static Color WithAlpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);

        // ---------------- fonts ----------------
        /// <summary>The game's own OpenSans SDF fonts (SemiBold for body text, Bold for titles).</summary>
        public static TMP_FontAsset Font => font != null ? font : (font = FindFont("OpenSans-SemiBold SDF") ?? FindFont("OpenSans-Bold SDF") ?? AnyFont());
        public static TMP_FontAsset FontBold => fontBold != null ? fontBold : (fontBold = FindFont("OpenSans-Bold SDF") ?? Font);

        static TMP_FontAsset FindFont(string name)
        {
            try
            {
                foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                    if (f != null && f.name == name) return f;
            }
            catch (Exception e) { Mod.Log.Warning("Font lookup failed: " + e.Message); }
            return null;
        }

        static TMP_FontAsset AnyFont()
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<TextMeshProUGUI>())
                if (t != null && t.font != null) return t.font;
            return TMP_Settings.defaultFontAsset;
        }

        /// <summary>Looks up one of the game's UI sprites by name (e.g. "Rectangle_RoundedEdges").</summary>
        public static Sprite Native(string name)
        {
            if (nativeSprites.TryGetValue(name, out var sp) && sp != null) return sp;
            try
            {
                foreach (var s in Resources.FindObjectsOfTypeAll<Sprite>())
                {
                    if (s != null && s.name == name) { nativeSprites[name] = s; return s; }
                }
            }
            catch { }
            nativeSprites[name] = null;
            return null;
        }

        public static void SetFont(TMP_FontAsset f) { if (f != null) font = f; }

        // ---------------- procedural sprites ----------------
        public static Sprite Round4 => round4 ??= RoundedRect(32, 4);
        public static Sprite Round8 => round8 ??= RoundedRect(48, 8);
        public static Sprite Round16 => round16 ??= RoundedRect(64, 12);
        public static Sprite Circle => circle ??= MakeCircle(128, 0);
        public static Sprite VerticalGradient => Native("Vertical gradient");
        public static Sprite HorizontalGradient => Native("Horizontal gradient");
        public static Sprite Ring => ring ??= MakeCircle(128, 0.14f);
        public static Sprite RingThin => ringThin ??= MakeCircle(128, 0.075f);
        public static Sprite Glow => softGlow ??= MakeGlow(128);
        public static Sprite Diamond => diamond ??= MakeDiamond(64);
        /// <summary>Diamond outline, for the mastery selection burst.</summary>
        public static Sprite DiamondRing => diamondRing ??= MakeDiamond(128, 0.1f);
        /// <summary>Rhombus filled with brilliant-cut facets (white/grey shades), for unlocked masteries.</summary>
        public static Sprite DiamondFacets => facets ??= MakeFacets(256);
        /// <summary>Single four-point sparkle (curved star), like one star of ✨.</summary>
        public static Sprite Sparkle => sparkle ??= MakeSparkle(96);

        static Texture2D NewTex(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontUnloadUnusedAsset
            };
            return t;
        }

        static Sprite Finish(Texture2D t, Vector4 border, string name)
        {
            t.Apply(false, false);
            var s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            s.hideFlags = HideFlags.DontUnloadUnusedAsset;
            s.name = name;
            return s;
        }

        /// <summary>White rounded rectangle, 9-sliced so it stretches to any size.</summary>
        static Sprite RoundedRect(int size, int radius)
        {
            var t = NewTex(size, size);
            var px = new Color32[size * size];
            float r = radius;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, r, size - r);
                float cy = Mathf.Clamp(y + 0.5f, r, size - r);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                float a = Mathf.Clamp01(r - d + 0.5f);
                if (r <= 0) a = 1;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            t.SetPixels32(px);
            return Finish(t, new Vector4(radius + 1, radius + 1, radius + 1, radius + 1), $"HS_round{radius}");
        }

        static Sprite MakeCircle(int size, float ringWidth)
        {
            var t = NewTex(size, size);
            var px = new Color32[size * size];
            float c = size / 2f;
            float outer = c - 1f;
            float inner = ringWidth > 0 ? outer - size * ringWidth : -1;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                float a = Mathf.Clamp01(outer - d + 0.5f);
                if (inner > 0) a *= Mathf.Clamp01(d - inner + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            t.SetPixels32(px);
            return Finish(t, Vector4.zero, ringWidth > 0 ? "HS_ring" : "HS_circle");
        }

        static Sprite MakeGlow(int size)
        {
            var t = NewTex(size, size);
            var px = new Color32[size * size];
            float c = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c)) / c;
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3 - 2 * a);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            t.SetPixels32(px);
            return Finish(t, Vector4.zero, "HS_glow");
        }

        static Sprite MakeDiamond(int size, float ringWidth = 0f)
        {
            var t = NewTex(size, size);
            var px = new Color32[size * size];
            float c = size / 2f;
            float inner = ringWidth > 0 ? c - 1 - size * ringWidth : -1;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Abs(x + 0.5f - c) + Mathf.Abs(y + 0.5f - c);
                float a = Mathf.Clamp01(c - 1 - d + 0.5f);
                if (inner > 0) a *= Mathf.Clamp01(d - inner + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            t.SetPixels32(px);
            return Finish(t, Vector4.zero, ringWidth > 0 ? "HS_diamond_ring" : "HS_diamond");
        }

        /// <summary>
        /// Procedural brilliant cut inside a rhombus: a bright table in the middle, then rings of triangular
        /// facets split by 16 radial sectors, each with its own grey value, thin darker edges between facets and
        /// a soft diagonal shine. Rings follow the rhombus outline (L1 distance), so it reads as a cut gem.
        /// </summary>
        static Sprite MakeFacets(int size)
        {
            var t = NewTex(size, size);
            var px = new Color32[size * size];
            float c = size / 2f;
            const int sectors = 16;
            float[] ringEdge = { 0f, 0.34f, 0.62f, 0.84f, 1.0f };
            var rng = new System.Random(11);
            var shade = new float[sectors, 4, 2];
            for (int s = 0; s < sectors; s++)
            for (int r = 0; r < 4; r++)
            for (int k = 0; k < 2; k++)
                shade[s, r, k] = r == 0 ? 0.72f + 0.28f * (float)rng.NextDouble() : 0.22f + 0.78f * Mathf.Pow((float)rng.NextDouble(), 0.8f);

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - c) / (c - 1f);
                float dy = (y + 0.5f - c) / (c - 1f);
                float l1 = Mathf.Abs(dx) + Mathf.Abs(dy);
                float alpha = Mathf.Clamp01((1f - l1) * (c - 1f) + 0.5f);
                if (alpha <= 0f) { px[y * size + x] = new Color32(255, 255, 255, 0); continue; }

                float ang = (Mathf.Atan2(dy, dx) / (2f * Mathf.PI) + 1f) % 1f;
                float sf = ang * sectors;
                int sec = Mathf.Min(sectors - 1, (int)sf);
                float fs = sf - sec;
                int ring = 0;
                while (ring < 3 && l1 >= ringEdge[ring + 1]) ring++;
                float r0 = ringEdge[ring], r1 = ringEdge[ring + 1];
                float rf = Mathf.Clamp01((l1 - r0) / (r1 - r0));

                // two triangles per cell, the diagonal flipping on every other sector (like star/kite facets)
                float across = (sec % 2 == 0) ? fs : 1f - fs;
                int tri = across + rf > 1f ? 1 : 0;
                float v = shade[sec, ring, tri];
                v += (rf - 0.5f) * 0.1f;                                      // light runs across each facet
                v += 0.18f * Mathf.Clamp01(1f - Mathf.Abs(dx - dy + 0.25f) * 2.2f); // diagonal shine

                // thin darker lines on facet borders
                float edgeSector = Mathf.Min(fs, 1f - fs) * l1 * 6.3f;
                float edgeRing = Mathf.Min(rf, 1f - rf) * (r1 - r0);
                float edgeDiag = Mathf.Abs(across + rf - 1f) * (r1 - r0) * 0.7f;
                float edge = Mathf.Min(edgeSector, Mathf.Min(ring == 0 ? 1f : edgeRing, ring == 0 ? 1f : edgeDiag));
                if (edge < 0.014f) v *= 0.68f + 0.32f * (edge / 0.014f);
                // outer bevel: a little darker near the outline
                v *= 0.86f + 0.14f * Mathf.Clamp01((1f - l1) * 8f);

                byte g = (byte)(Mathf.Clamp01(v) * 255f);
                px[y * size + x] = new Color32(g, g, g, (byte)(alpha * 255f));
            }
            t.SetPixels32(px);
            return Finish(t, Vector4.zero, "HS_diamond_facets");
        }

        static Sprite MakeSparkle(int size)
        {
            var t = NewTex(size, size);
            var px = new Color32[size * size];
            float c = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - c) / (c - 1f);
                float dy = Mathf.Abs(y + 0.5f - c) / (c - 1f);
                // |x|^p + |y|^p <= 1 with p < 1 gives four curved points (p = 0.62: plump, like one star of ✨)
                float f = Mathf.Pow(dx, 0.62f) + Mathf.Pow(dy, 0.62f);
                float a = Mathf.Clamp01((1f - f) * c * 0.35f + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            t.SetPixels32(px);
            return Finish(t, Vector4.zero, "HS_sparkle");
        }

        // ---------------- element builders ----------------
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.layer = parent != null ? parent.gameObject.layer : 5;
            var rt = go.AddComponent<RectTransform>();
            if (parent != null) rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float l = 0, float b = 0, float r = 0, float t = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        public static RectTransform Anchor(this RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin = default, Vector2 offMax = default)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
            return rt;
        }

        /// <summary>Fixed-size element centred on an anchor point.</summary>
        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return rt;
        }

        public static Image Image(string name, Transform parent, Color color, Sprite sprite = null, bool raycast = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static TextMeshProUGUI Label(string name, Transform parent, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Left, FontStyles style = FontStyles.Normal)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            bool bold = (style & FontStyles.Bold) != 0;
            var f = bold ? FontBold : Font;
            if (f != null)
            {
                t.font = f;
                if (bold) style &= ~FontStyles.Bold; // real bold face instead of the synthetic one
            }
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.enableWordWrapping = true;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.richText = true;
            return t;
        }

        /// <summary>Button whose state colours live in the ColorBlock (the image itself stays white).</summary>
        public static Button Button(Image target, Action onClick, Color normal, Color hover, Color pressed)
        {
            target.raycastTarget = true;
            target.color = Color.white;
            var b = target.gameObject.AddComponent<Button>();
            b.targetGraphic = target;
            b.transition = Selectable.Transition.ColorTint;
            SetButtonColors(b, normal, hover, pressed);
            var nav = b.navigation;
            nav.mode = Navigation.Mode.None;
            b.navigation = nav;
            if (onClick != null) b.onClick.AddListener((UnityAction)onClick);
            Sfx.Attach(b);
            return b;
        }

        public static void SetButtonColors(Button b, Color normal, Color hover, Color pressed)
        {
            var cb = b.colors;
            cb.normalColor = normal;
            cb.highlightedColor = hover;
            cb.pressedColor = pressed;
            cb.selectedColor = normal;
            cb.disabledColor = normal;
            cb.colorMultiplier = 1;
            cb.fadeDuration = 0.08f;
            b.colors = cb;
        }

        public static Color Lighten(Color c, float amount) => Color.Lerp(c, Color.white, amount).WithAlpha(c.a);
        public static Color Darken(Color c, float amount) => Color.Lerp(c, Color.black, amount).WithAlpha(c.a);

        /// <summary>Straight line between two points in the parent's local space (pivot centre).</summary>
        public static Image Line(string name, Transform parent, Vector2 a, Vector2 b, float thickness, Color color)
        {
            var img = Image(name, parent, color, null);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            Vector2 d = b - a;
            rt.sizeDelta = new Vector2(d.magnitude, thickness);
            rt.anchoredPosition = (a + b) / 2f;
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            return img;
        }

        /// <summary>
        /// Line that starts at <paramref name="a"/> (pivot there) and can be grown toward <paramref name="b"/> by
        /// changing its width. Both ends are extended by half the thickness so joined segments leave no gaps.
        /// </summary>
        public static Image LineFrom(string name, Transform parent, Vector2 a, Vector2 b, float thickness, Color color, out float length)
        {
            var img = Image(name, parent, color, null);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0, 0.5f);
            Vector2 d = b - a;
            Vector2 dir = d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.right;
            length = d.magnitude + thickness;
            rt.anchoredPosition = a - dir * (thickness / 2f);
            rt.sizeDelta = new Vector2(length, thickness);
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            return img;
        }

        /// <summary>Pointer enter/exit callbacks (hover previews).</summary>
        public static void OnHover(GameObject go, Action enter, Action exit)
        {
            var trigger = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
            if (enter != null) AddTrigger(trigger, EventTriggerType.PointerEnter, enter);
            if (exit != null) AddTrigger(trigger, EventTriggerType.PointerExit, exit);
        }

        static void AddTrigger(EventTrigger t, EventTriggerType type, Action a)
        {
            var e = new EventTrigger.Entry { eventID = type };
            e.callback.AddListener((UnityAction<BaseEventData>)(new Action<BaseEventData>(_ => a())));
            t.triggers.Add(e);
        }

        /// <summary>Right-angle connector (down, across, down) between two points, Cyberpunk-perk-tree style.
        /// <paramref name="bendY"/> picks the height of the horizontal run (default: halfway).</summary>
        public static Image[] Elbow(string name, Transform parent, Vector2 from, Vector2 to, float thickness, Color color, float? bendY = null)
        {
            if (Mathf.Abs(from.x - to.x) < 0.5f)
                return new[] { Line(name, parent, from, to, thickness, color) };
            float midY = bendY ?? (from.y + to.y) / 2f;
            var a = new Vector2(from.x, midY);
            var b = new Vector2(to.x, midY);
            return new[]
            {
                Line(name + "_a", parent, from, a + new Vector2(0, -thickness / 2f), thickness, color),
                Line(name + "_b", parent, a, b, thickness, color),
                Line(name + "_c", parent, b + new Vector2(0, thickness / 2f), to, thickness, color),
            };
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var c = t.GetChild(i).gameObject;
                c.SetActive(false);
                UnityEngine.Object.Destroy(c);
            }
        }

        public static void AddLayoutElement(GameObject go, float prefH = -1, float prefW = -1, float flexW = -1, float flexH = -1)
        {
            var le = go.AddComponent<LayoutElement>();
            if (prefH >= 0) le.preferredHeight = prefH;
            if (prefW >= 0) le.preferredWidth = prefW;
            if (flexW >= 0) le.flexibleWidth = flexW;
            if (flexH >= 0) le.flexibleHeight = flexH;
        }
    }
}
