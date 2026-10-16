using System;
using HylandSkills.Core;
using HylandSkills.State;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using static HylandSkills.UI.UiKit;

namespace HylandSkills.UI
{
    /// <summary>HUD placement, scale, opacity, toggles and active-slot key bindings.</summary>
    public class SettingsPage
    {
        public RectTransform Root;
        readonly Image[] anchorCells = new Image[9];
        TextMeshProUGUI scaleValue, opacityValue;
        readonly Image[] keyButtons = new Image[ActiveLoadout.Slots];
        readonly TextMeshProUGUI[] keyLabels = new TextMeshProUGUI[ActiveLoadout.Slots];
        readonly (Image box, Func<bool> get)[] toggles = new (Image, Func<bool>)[3];

        public SettingsPage(RectTransform body)
        {
            Root = Rect("SettingsPage", body).Stretch();

            // ---------- left card: timers ----------
            var left = Image("TimersCard", Root, Theme.Surface, Round16);
            left.rectTransform.Anchor(new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(Metrics.Pad, Metrics.Pad), new Vector2(-Metrics.Pad * 0.5f, -Metrics.Pad));
            var lc = left.transform;
            Header(lc, "ABILITY TIMERS", 0);
            Caption(lc, "Where your active ability timers sit on screen.", 1);

            float cell = Metrics.Node * 0.62f;
            var grid = Image("Grid", lc, Theme.Background, Round8);
            grid.rectTransform.Place(new Vector2(0, 1), new Vector2(Metrics.Pad, -Metrics.FontHeader * 3.4f), new Vector2(cell * 3 * 1.6f + 12, cell * 3 * 0.95f + 12), new Vector2(0, 1));
            for (int i = 0; i < 9; i++)
            {
                int idx = i;
                int col = i % 3, row = i / 3;
                var c = Image("Cell" + i, grid.transform, Theme.SurfaceHi, Round4);
                c.rectTransform.Place(new Vector2(0, 1), new Vector2(6 + col * cell * 1.6f + cell * 0.8f, -6 - row * cell * 0.95f - cell * 0.45f), new Vector2(cell * 1.5f, cell * 0.85f));
                Button(c, () => { Prefs.Anchor.Value = (HudAnchor)idx; Prefs.Save(); Hud.Relayout(); Refresh(); }, Theme.SurfaceHi, Lighten(Theme.SurfaceHi, 0.1f), Darken(Theme.SurfaceHi, 0.2f));
                anchorCells[i] = c;
            }

            float y = -Metrics.FontHeader * 3.4f - cell * 3 * 0.95f - 12 - Metrics.Pad * 1.2f;
            scaleValue = Stepper(lc, "Size", y, () => Step(Prefs.HudScale, -0.1f, 0.6f, 1.6f), () => Step(Prefs.HudScale, 0.1f, 0.6f, 1.6f));
            y -= Metrics.Node * 0.7f;
            opacityValue = Stepper(lc, "Opacity", y, () => Step(Prefs.HudOpacity, -0.1f, 0.3f, 1f), () => Step(Prefs.HudOpacity, 0.1f, 0.3f, 1f));
            y -= Metrics.Node * 0.7f;
            toggles[0] = (Toggle(lc, "Show key hints", y, () => { Prefs.ShowKeyHints.Value = !Prefs.ShowKeyHints.Value; }), () => Prefs.ShowKeyHints.Value);
            y -= Metrics.Node * 0.6f;
            toggles[1] = (Toggle(lc, "Show empty slots", y, () => { Prefs.ShowEmptySlots.Value = !Prefs.ShowEmptySlots.Value; }), () => Prefs.ShowEmptySlots.Value);

            // ---------- right card: controls ----------
            var right = Image("ControlsCard", Root, Theme.Surface, Round16);
            right.rectTransform.Anchor(new Vector2(0.5f, 0), new Vector2(1, 1), new Vector2(Metrics.Pad * 0.5f, Metrics.Pad), new Vector2(-Metrics.Pad, -Metrics.Pad));
            var rc = right.transform;
            Header(rc, "CONTROLS", 0);
            Caption(rc, "Click a slot, then press the key you want. Esc cancels.", 1);

            float ky = -Metrics.FontHeader * 3.4f;
            for (int i = 0; i < ActiveLoadout.Slots; i++)
            {
                int slot = i;
                var label = Label("SlotLabel" + i, rc, $"Active slot {i + 1}", Metrics.FontBody, Theme.Text, TextAlignmentOptions.MidlineLeft);
                label.rectTransform.Anchor(new Vector2(0, 1), new Vector2(0.6f, 1), new Vector2(Metrics.Pad, ky - Metrics.Node * 0.55f), new Vector2(0, ky));
                var b = Image("Key" + i, rc, Theme.SurfaceHi, Round8);
                b.rectTransform.Anchor(new Vector2(0.6f, 1), new Vector2(1, 1), new Vector2(0, ky - Metrics.Node * 0.55f), new Vector2(-Metrics.Pad, ky));
                Button(b, () => KeyRebinder.Listen(slot, Refresh), Theme.SurfaceHi, Lighten(Theme.SurfaceHi, 0.1f), Darken(Theme.SurfaceHi, 0.2f));
                keyButtons[i] = b;
                var t = Label("Text", b.transform, "", Metrics.FontBody, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
                t.rectTransform.Stretch();
                keyLabels[i] = t;
                ky -= Metrics.Node * 0.72f;
            }
            ky -= Metrics.Pad * 0.5f;
            toggles[2] = (Toggle(rc, "Notify on new skill points", ky, () => { Prefs.PointNotifications.Value = !Prefs.PointNotifications.Value; }), () => Prefs.PointNotifications.Value);
            ky -= Metrics.Node * 0.7f;

            var reset = Image("ResetKeys", rc, Theme.SurfaceHi, Round8);
            reset.rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(Metrics.Pad, ky - Metrics.Node * 0.55f), new Vector2(-Metrics.Pad, ky));
            Button(reset, ResetDefaults, Theme.SurfaceHi, Lighten(Theme.SurfaceHi, 0.1f), Darken(Theme.SurfaceHi, 0.2f));
            Label("Text", reset.transform, "Restore defaults", Metrics.FontSmall, Theme.TextDim, TextAlignmentOptions.Center, FontStyles.Bold).rectTransform.Stretch();
        }

        static void Header(Transform parent, string text, int line)
        {
            var t = Label("Header", parent, text, Metrics.FontSmall, Theme.TextDim, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            t.characterSpacing = 4;
            t.rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(Metrics.Pad, -Metrics.Pad - Metrics.FontHeader), new Vector2(-Metrics.Pad, -Metrics.Pad));
        }

        static void Caption(Transform parent, string text, int line)
        {
            var t = Label("Caption", parent, text, Metrics.FontSmall, Theme.TextFaint, TextAlignmentOptions.TopLeft);
            t.rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(Metrics.Pad, -Metrics.Pad - Metrics.FontHeader * 2.6f), new Vector2(-Metrics.Pad, -Metrics.Pad - Metrics.FontHeader * 1.15f));
        }

        TextMeshProUGUI Stepper(Transform parent, string name, float y, Action minus, Action plus)
        {
            float h = Metrics.Node * 0.55f;
            var label = Label(name, parent, name, Metrics.FontBody, Theme.Text, TextAlignmentOptions.MidlineLeft);
            label.rectTransform.Anchor(new Vector2(0, 1), new Vector2(0.45f, 1), new Vector2(Metrics.Pad, y - h), new Vector2(0, y));
            var m = Image("Minus", parent, Theme.SurfaceHi, Round8);
            m.rectTransform.Anchor(new Vector2(0.45f, 1), new Vector2(0.45f, 1), new Vector2(0, y - h), new Vector2(h, y));
            Button(m, () => { minus(); Prefs.Save(); Hud.Relayout(); Refresh(); }, Theme.SurfaceHi, Lighten(Theme.SurfaceHi, 0.1f), Darken(Theme.SurfaceHi, 0.2f));
            Label("T", m.transform, "−", Metrics.FontHeader, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold).rectTransform.Stretch();
            var value = Label("Value", parent, "", Metrics.FontBody, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            value.rectTransform.Anchor(new Vector2(0.45f, 1), new Vector2(1, 1), new Vector2(h, y - h), new Vector2(-Metrics.Pad - h, y));
            var p = Image("Plus", parent, Theme.SurfaceHi, Round8);
            p.rectTransform.Anchor(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-Metrics.Pad - h, y - h), new Vector2(-Metrics.Pad, y));
            Button(p, () => { plus(); Prefs.Save(); Hud.Relayout(); Refresh(); }, Theme.SurfaceHi, Lighten(Theme.SurfaceHi, 0.1f), Darken(Theme.SurfaceHi, 0.2f));
            Label("T", p.transform, "+", Metrics.FontHeader, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold).rectTransform.Stretch();
            return value;
        }

        Image Toggle(Transform parent, string name, float y, Action flip)
        {
            float h = Metrics.Node * 0.45f;
            var label = Label(name, parent, name, Metrics.FontBody, Theme.Text, TextAlignmentOptions.MidlineLeft);
            label.rectTransform.Anchor(new Vector2(0, 1), new Vector2(0.8f, 1), new Vector2(Metrics.Pad, y - h), new Vector2(0, y));
            // switch drawn as rounded rectangles (track + square knob) to match the game's flat controls
            var box = Image("Toggle", parent, Theme.SurfaceHi, Round8);
            box.rectTransform.Anchor(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-Metrics.Pad - h * 1.9f, y - h), new Vector2(-Metrics.Pad, y));
            Button(box, () => { flip(); Prefs.Save(); Hud.Relayout(); Refresh(); }, Theme.SurfaceHi, Lighten(Theme.SurfaceHi, 0.1f), Darken(Theme.SurfaceHi, 0.2f));
            var knob = Image("Knob", box.transform, Color.white, Round4);
            knob.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(h * 0.5f, 0), Vector2.one * (h - 8));
            return box;
        }

        static void Step(MelonLoader.MelonPreferences_Entry<float> e, float delta, float min, float max)
        {
            e.Value = Mathf.Round(Mathf.Clamp(e.Value + delta, min, max) * 10f) / 10f;
        }

        void ResetDefaults()
        {
            for (int i = 0; i < ActiveLoadout.Slots; i++) Prefs.SlotKeys[i].Value = Prefs.DefaultKeys[i];
            Prefs.Anchor.Value = HudAnchor.TopRight;
            Prefs.HudScale.Value = 1f;
            Prefs.HudOpacity.Value = 1f;
            Prefs.ShowKeyHints.Value = true;
            Prefs.ShowEmptySlots.Value = false;
            Prefs.PointNotifications.Value = true;
            Prefs.Save();
            Hud.Relayout();
            Skills.NotifyChanged();
        }

        public void Refresh()
        {
            if (Root == null) return;
            int a = (int)Prefs.Anchor.Value;
            for (int i = 0; i < 9; i++)
                SetButtonColors(anchorCells[i].GetComponent<Button>(), i == a ? Theme.Accent : Theme.SurfaceHi, Lighten(i == a ? Theme.Accent : Theme.SurfaceHi, 0.1f), Darken(Theme.SurfaceHi, 0.2f));
            scaleValue.text = $"{Mathf.RoundToInt(Prefs.HudScale.Value * 100)}%";
            opacityValue.text = $"{Mathf.RoundToInt(Prefs.HudOpacity.Value * 100)}%";
            for (int i = 0; i < ActiveLoadout.Slots; i++)
            {
                bool listening = KeyRebinder.IsListening && KeyRebinder.Slot == i;
                keyLabels[i].text = listening ? "Press a key…" : Prefs.KeyLabel(Prefs.SlotKeys[i].Value);
                keyLabels[i].color = listening ? Theme.Gold : Theme.Text;
            }
            foreach (var (box, get) in toggles)
            {
                if (box == null) continue;
                bool on = get();
                SetButtonColors(box.GetComponent<Button>(), on ? Theme.Accent : Theme.Locked, Lighten(on ? Theme.Accent : Theme.Locked, 0.1f), Darken(Theme.Locked, 0.2f));
                var knob = box.transform.Find("Knob").GetComponent<RectTransform>();
                float h = box.rectTransform.rect.height;
                float w = box.rectTransform.rect.width;
                knob.anchoredPosition = new Vector2(on ? w - h * 0.5f : h * 0.5f, 0);
            }
        }
    }
}
