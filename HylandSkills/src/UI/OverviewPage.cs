using System;
using System.Linq;
using HylandSkills.Actives;
using HylandSkills.Core;
using HylandSkills.Data;
using HylandSkills.State;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using static HylandSkills.UI.UiKit;

namespace HylandSkills.UI
{
    /// <summary>Menu 1: the eight categories on an octagon. Click one to open its tree.</summary>
    public class OverviewPage
    {
        public RectTransform Root;
        readonly Action<Branch> open;
        /// <summary>Opens the active skills menu on a slot (set by the app).</summary>
        public Action<int> OpenLoadout;

        const float Radius = 186f;
        const float Tile = 86f;
        const float SynergyDot = 18f;

        readonly Image[] rings = new Image[8];
        readonly Image[] tileBg = new Image[8];
        readonly Image[] icons = new Image[8];
        readonly TextMeshProUGUI[] names = new TextMeshProUGUI[8];
        readonly TextMeshProUGUI[] counts = new TextMeshProUGUI[8];
        readonly Image[] gems = new Image[8];
        readonly Image[] edges = new Image[8];
        readonly RectTransform[] holders = new RectTransform[8];
        readonly float[] tileScale = { 1, 1, 1, 1, 1, 1, 1, 1 };
        RectTransform infoGroup;
        CanvasGroup infoFade;
        string infoKey = "";
        TextMeshProUGUI centerValue, centerLabel, centerMastery;

        // info panel
        TextMeshProUGUI infoTitle, infoTagline, infoBody, infoStats;
        Image infoIcon, openButton;
        TextMeshProUGUI openLabel;
        int hovered = -1;
        SynergyDef hoveredSynergy;

        // loadout
        readonly Image[] slotBg = new Image[ActiveLoadout.Slots];
        readonly Image[] slotIcon = new Image[ActiveLoadout.Slots];
        readonly TextMeshProUGUI[] slotKey = new TextMeshProUGUI[ActiveLoadout.Slots];
        readonly TextMeshProUGUI[] slotName = new TextMeshProUGUI[ActiveLoadout.Slots];

        public OverviewPage(RectTransform body, Action<Branch> openBranch)
        {
            open = openBranch;
            Root = Rect("Overview", body).Stretch();
            var left = Rect("Octagon", Root).Anchor(new Vector2(0, 0), new Vector2(0.63f, 1));
            var right = Rect("Side", Root).Anchor(new Vector2(0.63f, 0), new Vector2(1, 1));
            BuildOctagon(left);
            BuildSide(right);
        }

        static Vector2 Vertex(int i) // i = 0 at the top, clockwise
        {
            float a = Mathf.Deg2Rad * (90f - 45f * i);
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Radius;
        }

        void BuildOctagon(RectTransform area)
        {
            var c = Rect("Center", area).Place(new Vector2(0.5f, 0.5f), new Vector2(0, -4), new Vector2(10, 10));

            for (int i = 0; i < 8; i++)
            {
                var a = Vertex(i);
                var b = Vertex((i + 1) % 8);
                // shorten edges so they stop at the tiles
                var dir = (b - a).normalized;
                edges[i] = Line("Edge" + i, c, a + dir * (Tile * 0.55f), b - dir * (Tile * 0.55f), 3.5f, Theme.Stroke);
            }

            // centre: available points, flat like the native apps' stat boxes
            var hub = Image("Hub", c, Theme.Card, Circle);
            hub.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 150);
            centerValue = Label("SP", hub.transform, "0", 48, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            centerValue.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 14), new Vector2(140, 56));
            centerLabel = Label("SPLabel", hub.transform, "Skill points", Metrics.FontTiny, Theme.TextDim, TextAlignmentOptions.Center, FontStyles.Bold);
            centerLabel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(140, 20));
            centerMastery = Label("MP", hub.transform, "", Metrics.FontTiny, Theme.Gold, TextAlignmentOptions.Center, FontStyles.Bold);
            centerMastery.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(140, 20));

            for (int i = 0; i < 8; i++)
            {
                int idx = i;
                var info = SkillDb.Branches[i];
                var holder = Rect("Tile_" + info.Name, c).Place(new Vector2(0.5f, 0.5f), Vertex(i), Vector2.one * Tile);
                holders[i] = holder;

                var bg = Image("Bg", holder, Theme.Card, Circle);
                bg.rectTransform.Stretch();
                Button(bg, () => { Tweens.Pop(holder, -0.08f, 0.14f, 0f, tileScale[idx]); open(info.Branch); }, Theme.Card, Lighten(Theme.Card, 0.08f), Darken(Theme.Card, 0.2f));
                OnHover(bg.gameObject, () => { hovered = idx; hoveredSynergy = null; RefreshInfo(); }, null);
                tileBg[i] = bg;

                var track = Image("Track", holder, Theme.Stroke, RingThin);
                track.rectTransform.Stretch();
                var ring = Image("Progress", holder, info.Color, RingThin);
                ring.type = UnityEngine.UI.Image.Type.Filled;
                ring.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
                ring.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
                ring.rectTransform.Stretch();
                rings[i] = ring;

                var icon = Image("Icon", holder, info.Color, Icons.Get(info.Icon));
                icon.preserveAspect = true;
                icon.rectTransform.Stretch(Tile * 0.22f, Tile * 0.22f, Tile * 0.22f, Tile * 0.22f);
                icons[i] = icon;

                // labels sit outside the octagon; side vertices push further out horizontally
                Vector2 outward = Vertex(i).normalized;
                float push = Tile * 0.5f + 22f + Mathf.Abs(outward.x) * 36f;
                var lbl = Rect("Label", c).Place(new Vector2(0.5f, 0.5f), Vertex(i) + outward * push, new Vector2(140, 44));
                names[i] = Label("Name", lbl, info.Name, Metrics.FontBody, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
                names[i].rectTransform.Anchor(new Vector2(0, 0.5f), new Vector2(1, 1));
                counts[i] = Label("Count", lbl, "0/24", Metrics.FontTiny, Theme.TextDim, TextAlignmentOptions.Center, FontStyles.Bold);
                counts[i].rectTransform.Anchor(new Vector2(0, 0), new Vector2(1, 0.5f));
            }

            // synergy dots on edge midpoints
            for (int i = 0; i < 8; i++)
            {
                var syn = SynergyFor(SkillDb.Branches[i].Branch, SkillDb.Branches[(i + 1) % 8].Branch);
                var mid = (Vertex(i) + Vertex((i + 1) % 8)) / 2f;
                var gem = Image("Synergy" + i, c, Theme.Locked, Circle, true);
                gem.rectTransform.Place(new Vector2(0.5f, 0.5f), mid, Vector2.one * SynergyDot);
                var gemRt = gem.rectTransform;
                OnHover(gem.gameObject, () => { hoveredSynergy = syn; RefreshInfo(); Tweens.Pop(gemRt, 0.35f, 0.25f); }, null);
                gems[i] = gem;
            }
        }

        static SynergyDef SynergyFor(Branch a, Branch b) =>
            SkillDb.Synergies.FirstOrDefault(s => (s.A == a && s.B == b) || (s.A == b && s.B == a));

        void BuildSide(RectTransform area)
        {
            // flat side panel with a plain divider, like the Products app's detail column
            var panel = Image("Panel", area, Theme.SurfaceHi, null);
            panel.rectTransform.Stretch();
            var divider = Image("Divider", area, Theme.Stroke, null);
            divider.rectTransform.Anchor(new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(1, 0));
            float pad = Metrics.Pad + 3;
            // the hover info fades in when it changes; the loadout list below stays put
            infoGroup = Rect("Info", panel.transform).Stretch();
            infoFade = infoGroup.gameObject.AddComponent<CanvasGroup>();
            var p = infoGroup.transform;

            infoIcon = Image("Icon", p, Color.white, null);
            infoIcon.preserveAspect = true;
            infoIcon.rectTransform.Place(new Vector2(0, 1), new Vector2(pad + 22, -pad - 22), Vector2.one * 44);
            infoTitle = Label("Title", p, "", Metrics.FontHeader + 2, Theme.Text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            infoTitle.rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(pad + 56, -pad - 44), new Vector2(-pad, -pad));
            infoTagline = Label("Tagline", p, "", Metrics.FontSmall, Theme.TextDim, TextAlignmentOptions.TopLeft);
            infoTagline.rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(pad, -pad - 82), new Vector2(-pad, -pad - 54));
            infoBody = Label("Body", p, "", Metrics.FontBody, Theme.Text, TextAlignmentOptions.TopLeft);
            infoBody.lineSpacing = 2;
            infoBody.rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(pad, -pad - 210), new Vector2(-pad, -pad - 88));
            infoStats = Label("Stats", p, "", Metrics.FontSmall, Theme.TextDim, TextAlignmentOptions.TopLeft);
            infoStats.rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(pad, -pad - 270), new Vector2(-pad, -pad - 214));

            openButton = Image("Open", p, Theme.Accent, Round8);
            openButton.rectTransform.Anchor(new Vector2(0, 0), new Vector2(1, 0), new Vector2(pad, pad + 232), new Vector2(-pad, pad + 276));
            Button(openButton, () => { if (hovered >= 0) open(SkillDb.Branches[hovered].Branch); }, Theme.Accent, Theme.Accent, Theme.Accent);
            openLabel = Label("Text", openButton.transform, "Open tree", Metrics.FontBody, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            openLabel.rectTransform.Stretch();

            // loadout list at the bottom, native list style (title + plain rows)
            var lo = Rect("Loadout", panel.transform).Anchor(new Vector2(0, 0), new Vector2(1, 0), new Vector2(pad, pad), new Vector2(-pad, pad + 220));
            var title = Label("Title", lo, "Active slots · click to change", Metrics.FontSmall, Theme.TextDim, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            title.rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -24), Vector2.zero);
            for (int i = 0; i < ActiveLoadout.Slots; i++)
            {
                int slot = i;
                float y = -28 - i * 48;
                var row = Image("Slot" + i, lo, Theme.Card, Round8);
                row.rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, y - 44), new Vector2(0, y));
                Button(row, () => OnSlot(slot), Theme.Card, Lighten(Theme.Card, 0.06f), Darken(Theme.Card, 0.2f));
                slotBg[i] = row;
                var keyBg = Image("Key", row.transform, Theme.SurfaceHi, Round4);
                keyBg.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(34, 30));
                slotKey[i] = Label("K", keyBg.transform, "Z", Metrics.FontSmall, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
                slotKey[i].rectTransform.Stretch();
                slotIcon[i] = Image("Icon", row.transform, Color.white, null);
                slotIcon[i].preserveAspect = true;
                slotIcon[i].rectTransform.Place(new Vector2(0, 0.5f), new Vector2(64, 0), Vector2.one * 28);
                slotName[i] = Label("Name", row.transform, "", Metrics.FontSmall, Theme.Text, TextAlignmentOptions.MidlineLeft);
                slotName[i].enableWordWrapping = false;
                slotName[i].overflowMode = TextOverflowModes.Ellipsis;
                slotName[i].rectTransform.Stretch(86, 0, 10, 0);
            }
        }

        void OnSlot(int slot) => OpenLoadout?.Invoke(slot);

        public void Refresh()
        {
            if (Root == null) return;
            int sp = Skills.AvailablePoints;
            centerValue.text = sp.ToString();
            centerValue.color = sp > 0 ? Theme.Accent : Theme.Text;
            centerLabel.text = sp == 1 ? "Skill point" : "Skill points";
            int mp = Skills.AvailableMasteryPoints;
            centerMastery.text = mp > 0 ? $"+{mp} mastery" : "";

            for (int i = 0; i < 8; i++)
            {
                var info = SkillDb.Branches[i];
                int spent = Skills.SpentInBranch(info.Branch);
                rings[i].fillAmount = spent / (float)SkillDb.PointsPerBranch;
                counts[i].text = $"{spent}/{SkillDb.PointsPerBranch}";
                counts[i].color = spent > 0 ? info.Color : Theme.TextFaint;
                // a complete tree fills its whole circle with the branch colour and turns the icon white
                bool full = spent >= SkillDb.PointsPerBranch;
                Color tile = full ? info.Color : Theme.Card;
                SetButtonColors(tileBg[i].GetComponent<Button>(), tile, Lighten(tile, 0.08f), Darken(tile, 0.2f));
                icons[i].color = full ? Color.white : spent > 0 ? info.Color : Color.Lerp(info.Color, Theme.TextFaint, 0.45f);

                var syn = SynergyFor(info.Branch, SkillDb.Branches[(i + 1) % 8].Branch);
                bool on = syn != null && Skills.SynergyActive(syn);
                gems[i].color = on ? Theme.Gold : Theme.Locked;
                edges[i].color = on ? Theme.Gold.WithAlpha(0.6f) : Theme.Stroke;
            }

            for (int i = 0; i < ActiveLoadout.Slots; i++)
            {
                var d = SkillDb.Get(ActiveManager.Slot(i));
                slotKey[i].text = Prefs.KeyLabel(Prefs.SlotKeys[i].Value);
                slotIcon[i].sprite = d != null ? Icons.Get(d.Icon) : null;
                slotIcon[i].enabled = d != null;
                slotIcon[i].color = d != null ? SkillDb.Info(d.Branch).Color : Color.white;
                slotName[i].text = d != null ? d.Name : $"<color=#{ColorUtility.ToHtmlStringRGB(Theme.TextFaint)}>Empty</color>";
            }
            RefreshInfo();
        }

        /// <summary>Per frame: the hovered category grows a little, the others settle back.</summary>
        public void Animate()
        {
            if (Root == null || !Root.gameObject.activeInHierarchy) return;
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < 8; i++)
            {
                float target = i == hovered ? 1.1f : 1f;
                if (Mathf.Approximately(tileScale[i], target)) continue;
                tileScale[i] = Mathf.MoveTowards(tileScale[i], target, dt * 1.2f);
                if (!Tweens.IsRunning(holders[i].GetInstanceID())) holders[i].localScale = Vector3.one * tileScale[i];
            }
        }

        void FadeInfo()
        {
            string key = hoveredSynergy != null ? "s" + hoveredSynergy.Id : "b" + hovered;
            if (key == infoKey) return;
            infoKey = key;
            var g = infoFade;
            var rt = infoGroup;
            Tweens.Run(g.GetInstanceID(), 0.18f, k =>
            {
                if (g == null) return;
                float e = Tweens.EaseOut(k);
                g.alpha = 0.25f + 0.75f * e;
                rt.anchoredPosition = new Vector2(10f * (1f - e), 0);
            });
        }

        void RefreshInfo()
        {
            FadeInfo();
            if (hoveredSynergy != null)
            {
                var s = hoveredSynergy;
                bool on = Skills.SynergyActive(s);
                infoIcon.sprite = Icons.Get("diamond");
                infoIcon.color = on ? Theme.Gold : Theme.TextFaint;
                infoTitle.text = s.Name;
                infoTagline.text = on ? "Synergy active" : "Synergy";
                infoBody.text = s.Desc;
                infoStats.text = $"Spend {SkillDb.SynergyRequirement}+ points in both <b>{SkillDb.Info(s.A).Name}</b> ({Skills.SpentInBranch(s.A)}) and <b>{SkillDb.Info(s.B).Name}</b> ({Skills.SpentInBranch(s.B)}). It's free and turns on by itself.";
                openButton.gameObject.SetActive(false);
                return;
            }
            if (hovered < 0)
            {
                infoIcon.sprite = Icons.Get("app_icon");
                infoIcon.color = Color.white;
                infoTitle.text = "Your skills";
                infoTagline.text = Progression.NextPointRankLabel() is string next ? $"Next points at {next}" : "All skill points earned";
                infoBody.text = "Pick a category to see its tree. Ranking up earns enough points to complete half of the trees, so choose what kind of operator you want to be.";
                infoStats.text = $"{Skills.SpentTotal()} of {SkillDb.TotalSkillPoints} points spent · {Skills.MasteriesTaken()} of {Progression.MasteryCount} masteries";
                openButton.gameObject.SetActive(false);
                return;
            }
            var info = SkillDb.Branches[hovered];
            int spent = Skills.SpentInBranch(info.Branch);
            var mastery = SkillDb.All.FirstOrDefault(d => d.Branch == info.Branch && d.IsMastery && Skills.Has(d.Id));
            int learned = SkillDb.All.Count(d => d.Branch == info.Branch && !d.IsMastery && Skills.Has(d.Id));
            int total = SkillDb.All.Count(d => d.Branch == info.Branch && !d.IsMastery);
            infoIcon.sprite = Icons.Get(info.Icon);
            infoIcon.color = info.Color;
            infoTitle.text = info.Name;
            infoTagline.text = info.Tagline;
            infoBody.text = info.Blurb;
            infoStats.text = $"<color=#{ColorUtility.ToHtmlStringRGB(info.Color)}>{spent}/{SkillDb.PointsPerBranch}</color> points · {learned}/{total} skills" +
                             (mastery != null ? $"\nMastery: <color=#{ColorUtility.ToHtmlStringRGB(Theme.Gold)}>{mastery.Name}</color>" : "");
            openButton.gameObject.SetActive(true);
            SetButtonColors(openButton.GetComponent<Button>(), info.Color, Lighten(info.Color, 0.12f), Darken(info.Color, 0.2f));
            openLabel.text = $"Open {info.Name}";
        }
    }
}
