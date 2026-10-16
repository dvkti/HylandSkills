using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    /// <summary>Menu 2: one category's perk tree (prerequisite lines, point gates, combos) plus the detail panel.</summary>
    public class BranchPage
    {
        public RectTransform Root;
        public Branch Current { get; private set; }

        RectTransform treeArea, canvas, detailArea;
        ScrollRect scroll;
        TextMeshProUGUI title, tagline, progress;

        const float ColW = 132f;
        const float RowH = 112f;
        const float NodeSize = 72f;
        const float TopPad = 56f;
        const float BottomPad = 24f;
        /// <summary>Distance from the last tree level down to the masteries.</summary>
        const float MasteryGap = 136f;
        const float LineW = 4f;
        /// <summary>Scrollable height: five tree levels and the mastery row (room for the chosen one to grow).</summary>
        static float ContentHeight => TopPad + 4 * RowH + MasteryGap + MasterySize / 2f + 16f + BottomPad;

        SkillDef selected;
        readonly Dictionary<string, NodeView> nodes = new Dictionary<string, NodeView>();
        readonly List<LinkView> links = new List<LinkView>();
        readonly Dictionary<string, int> lastRank = new Dictionary<string, int>();
        bool lastComplete;
        Image masterySpark;
        /// <summary>True right after Build: the first refresh shows the current state without animating.</summary>
        bool fresh;


        /// <summary>A prerequisite connector: dim base segments plus coloured segments that grow from the parent
        /// to the child when the child is learned.</summary>
        sealed class LinkView
        {
            public string Parent, Child;
            /// <summary>Last tree level → mastery: available once the whole tree is complete.</summary>
            public bool ToMastery;
            public readonly List<Image> Base = new List<Image>();
            public readonly List<(Image img, float len)> Fill = new List<(Image, float)>();
            public float Total, Progress, Target;
        }
        readonly List<(Image img, ComboDef combo)> comboLinks = new List<(Image, ComboDef)>();
        readonly List<(TextMeshProUGUI label, Image line, int tier)> gates = new List<(TextMeshProUGUI, Image, int)>();

        // detail widgets
        Image dIconBg, dIcon, dBadgeBg, dButton;
        TextMeshProUGUI dName, dBadge, dRank, dText, dButtonText;
        RectTransform dEquipRow;
        readonly Image[] dEquip = new Image[ActiveLoadout.Slots];
        readonly TextMeshProUGUI[] dEquipLabel = new TextMeshProUGUI[ActiveLoadout.Slots];

        sealed class NodeView
        {
            public SkillDef Def;
            public RectTransform Holder;
            public Image Bg, Ring, Icon, Glow, Select, Combo, Lock, Gem, IconShadow;
            public Image[] Pips;
            public float Scale = 1f, TargetScale = 1f;
        }

        /// <summary>Asks the player to confirm a choice (title, body, confirm label, action). Set by the app.</summary>
        public System.Action<string, string, string, System.Action> Confirm;

        const float MasterySize = NodeSize + 24f;

        public BranchPage(RectTransform body)
        {
            Root = Rect("Branch", body).Stretch();
            treeArea = Rect("Tree", Root).Anchor(new Vector2(0, 0), new Vector2(0.68f, 1));
            detailArea = Rect("Detail", Root).Anchor(new Vector2(0.68f, 0), new Vector2(1, 1));

            title = Label("Title", treeArea, "", Metrics.FontTiny, Theme.TextDim, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            title.rectTransform.Anchor(new Vector2(0, 1), new Vector2(0.15f, 1), new Vector2(Metrics.Pad, -40), new Vector2(0, -8));
            title.gameObject.SetActive(false);
            tagline = Label("Tagline", treeArea, "", Metrics.FontSmall, Theme.TextDim, TextAlignmentOptions.MidlineLeft);
            tagline.rectTransform.Anchor(new Vector2(0, 1), new Vector2(0.7f, 1), new Vector2(Metrics.Pad, -44), new Vector2(0, -8));
            progress = Label("Progress", treeArea, "", Metrics.FontHeader, Theme.Text, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            progress.rectTransform.Anchor(new Vector2(0.6f, 1), new Vector2(1, 1), new Vector2(0, -44), new Vector2(-Metrics.Pad - 10, -8));

            BuildScroll();
            BuildDetail();
        }

        // =====================================================================
        // TREE
        // =====================================================================
        /// <summary>
        /// The tree is taller than the page, so it lives in a vertical ScrollRect (mouse wheel or drag) with a thin
        /// scrollbar like the native Products app.
        /// </summary>
        void BuildScroll()
        {
            var viewport = Image("Viewport", treeArea, new Color(0, 0, 0, 0), null, true);
            viewport.rectTransform.Anchor(new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0), new Vector2(-14, -48));
            viewport.gameObject.AddComponent<RectMask2D>();

            canvas = Rect("Canvas", viewport.transform);
            canvas.anchorMin = new Vector2(0, 1);
            canvas.anchorMax = new Vector2(1, 1);
            canvas.pivot = new Vector2(0.5f, 1);
            canvas.sizeDelta = new Vector2(0, ContentHeight);
            canvas.anchoredPosition = Vector2.zero;

            var track = Image("Scrollbar", treeArea, Theme.Card, Round4, true);
            track.rectTransform.Anchor(new Vector2(1, 0), new Vector2(1, 1), new Vector2(-10, 8), new Vector2(-4, -52));
            var handleArea = Rect("Sliding", track.transform).Stretch();
            var handle = Image("Handle", handleArea, Theme.Stroke, Round4, true);
            handle.rectTransform.Stretch();
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle.rectTransform;
            bar.targetGraphic = handle;
            bar.direction = Scrollbar.Direction.BottomToTop;
            var nav = bar.navigation;
            nav.mode = Navigation.Mode.None;
            bar.navigation = nav;

            scroll = treeArea.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport.rectTransform;
            scroll.content = canvas;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 36f;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide; // "expand" would resize the viewport over the header
        }

        /// <summary>Node centre in the scroll content (anchor = content centre).</summary>
        Vector2 Pos(SkillDef d)
        {
            float x = (d.Col - 2) * ColW + 40f;
            float top = ContentHeight / 2f - TopPad;
            float y = d.Tier >= 5 ? top - 4 * RowH - MasteryGap : top - d.Tier * RowH;
            return new Vector2(x, y);
        }

        float Level4Y => Pos(new SkillDef { Tier = 4 }).y;


        public void Show(Branch b)
        {
            Current = b;
            Build();
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
            var first = SkillDb.All.Where(d => d.Branch == b).OrderBy(d => d.Tier).ThenBy(d => d.Col).First();
            selected = selected != null && selected.Branch == b ? selected : first;
            Refresh();
        }

        void Build()
        {
            Clear(canvas);
            nodes.Clear();
            links.Clear();
            comboLinks.Clear();
            gates.Clear();
            lastRank.Clear();
            fresh = true;
            var info = SkillDb.Info(Current);
            var defs = SkillDb.All.Where(d => d.Branch == Current).ToList();

            // point gates: faint separators with the requirement on the left
            for (int t = 0; t <= 5; t++)
            {
                float y = Pos(new SkillDef { Tier = t, Col = 0 }).y;
                var line = Image("Gate" + t, canvas, Theme.Stroke.WithAlpha(0.35f), null);
                line.rectTransform.anchorMin = line.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                line.rectTransform.sizeDelta = new Vector2(ColW * 4.6f, 1);
                line.rectTransform.anchoredPosition = new Vector2(40f, y + RowH / 2f);
                if (t == 0) line.gameObject.SetActive(false);

                string txt = t == 5 ? "Mastery" : SkillDb.TierGate[t] == 0 ? "Start" : $"{SkillDb.TierGate[t]} pts";
                var lbl = Label("GateLabel" + t, canvas, txt, t == 5 ? Metrics.FontSmall : Metrics.FontTiny, Theme.TextFaint, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                lbl.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(-345f, y), new Vector2(84, 24));
                if (t == 5)
                {
                    // "Mastery" plus a single four-point sparkle right after the word
                    lbl.enableWordWrapping = false;
                    lbl.ForceMeshUpdate();
                    float w = lbl.preferredWidth;
                    if (w < 20f || w > 84f) w = 72f; // text not measured yet: width of "Mastery" at this size
                    var spark = Image("MasterySpark", canvas, Theme.Gold, Sparkle);
                    spark.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(-345f - 42f + w + 17f, y + 1f), Vector2.one * 22);
                    masterySpark = spark;
                }
                gates.Add((lbl, line, t));
            }

            // the mastery capstone sits behind everything else in its row
            var masteries = defs.Where(d => d.IsMastery).ToList();
            var lastLevel = defs.Where(d => d.Tier == 4).ToList();

            // prerequisite connectors (behind nodes): bend halfway below the parent's row, so siblings share that
            // bus and unrelated links never do
            foreach (var d in defs)
            {
                foreach (var pid in d.Parents)
                {
                    var p = SkillDb.Get(pid);
                    if (p == null) continue;
                    var from = Pos(p) - new Vector2(0, NodeSize / 2f);
                    var to = Pos(d) + new Vector2(0, NodeSize / 2f);
                    float bendY = Pos(p).y - RowH / 2f;
                    var pts = Mathf.Abs(from.x - to.x) < 0.5f
                        ? new List<Vector2> { from, to }
                        : new List<Vector2> { from, new Vector2(from.x, bendY), new Vector2(to.x, bendY), to };
                    MakeLink($"Link_{pid}_{d.Id}", pid, d.Id, pts, info.Color);
                }
            }

            if (masteries.Count > 0)
            {
                // each last-level skill runs down to a bus and into every mastery (they open when the tree is complete)
                float busY = Level4Y - RowH / 2f;
                foreach (var t in lastLevel)
                    foreach (var m in masteries)
                    {
                        var from = Pos(t) - new Vector2(0, NodeSize / 2f);
                        var end = Pos(m) + new Vector2(0, MasterySize / 2f);
                        var pts = Mathf.Abs(from.x - end.x) < 0.5f
                            ? new List<Vector2> { from, end }
                            : new List<Vector2> { from, new Vector2(from.x, busY), new Vector2(end.x, busY), end };
                        MakeLink($"MasteryLink_{t.Id}_{m.Id}", t.Id, m.Id, pts, Theme.Gold, toMastery: true);
                    }
            }

            // combo links: thin gold lines between partners in this tree
            foreach (var c in SkillDb.Combos)
            {
                var a = SkillDb.Get(c.A);
                var b2 = SkillDb.Get(c.B);
                if (a == null || b2 == null || a.Branch != Current || b2.Branch != Current) continue;
                var img = Line("Combo_" + c.Id, canvas, Pos(a), Pos(b2), 3f, Theme.Gold.WithAlpha(0.15f));
                comboLinks.Add((img, c));
            }

            foreach (var d in defs) nodes[d.Id] = MakeNode(d, info.Color);
            foreach (var d in defs) lastRank[d.Id] = Skills.Rank(d.Id);
            lastComplete = Skills.SpentInBranch(Current) >= SkillDb.PointsPerBranch;
        }

        void MakeLink(string name, string parent, string child, List<Vector2> pts, Color fillColor, bool toMastery = false)
        {
            var link = new LinkView { Parent = parent, Child = child, ToMastery = toMastery };
            for (int i = 0; i < pts.Count - 1; i++)
            {
                link.Base.Add(LineFrom(name + "_b" + i, canvas, pts[i], pts[i + 1], LineW, Theme.Stroke, out _));
            }
            for (int i = 0; i < pts.Count - 1; i++)
            {
                var img = LineFrom(name + "_f" + i, canvas, pts[i], pts[i + 1], LineW, fillColor, out float len);
                img.rectTransform.sizeDelta = new Vector2(0, LineW);
                link.Fill.Add((img, len));
                link.Total += len;
            }
            links.Add(link);
        }

        /// <summary>Shows a link filled up to <paramref name="p"/> (0-1) along its length, parent first.</summary>
        static void SetFill(LinkView l, float p)
        {
            l.Progress = p;
            float covered = p * l.Total;
            foreach (var (img, len) in l.Fill)
            {
                float w = Mathf.Clamp(covered, 0f, len);
                img.rectTransform.sizeDelta = new Vector2(w, LineW);
                img.enabled = w > 0.5f;
                covered -= len;
            }
        }

        NodeView MakeNode(SkillDef d, Color color)
        {
            float size = d.IsMastery ? MasterySize : NodeSize;
            var holder = Rect("Node_" + d.Id, canvas).Place(new Vector2(0.5f, 0.5f), Pos(d), Vector2.one * size);
            var v = new NodeView { Def = d, Holder = holder };
            Sprite shape = d.IsActive ? Round16 : d.IsMastery ? Diamond : Circle;
            Sprite ring = d.IsActive ? Round16 : d.IsMastery ? Diamond : Ring;

            v.Glow = Image("Glow", holder, color.WithAlpha(0.4f), Glow);
            v.Glow.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * size * 1.9f);
            v.Select = Image("Select", holder, Color.white, shape);
            v.Select.rectTransform.Stretch(-5, -5, -5, -5);
            v.Ring = Image("Ring", holder, Theme.Stroke, shape);
            v.Ring.rectTransform.Stretch();
            v.Bg = Image("Bg", holder, Theme.Card, shape);
            v.Bg.rectTransform.Stretch(3f, 3f, 3f, 3f);
            var def = d;
            Button(v.Bg, () => Select(def), Color.white, new Color(1.15f, 1.15f, 1.15f), new Color(0.8f, 0.8f, 0.8f));
            if (d.IsMastery)
            {
                // cut-diamond fill shown once the mastery is open (not a raycast target: clicks reach the button)
                v.Gem = Image("Gem", holder, Color.white, DiamondFacets);
                v.Gem.rectTransform.Stretch(3f, 3f, 3f, 3f);
            }

            float inset = d.IsMastery ? size * 0.27f : size * 0.2f;
            if (d.IsMastery)
            {
                // a soft offset copy keeps the icon readable over the facets
                v.IconShadow = Image("IconShadow", holder, Color.black.WithAlpha(0.6f), Icons.Get(d.Icon));
                v.IconShadow.preserveAspect = true;
                v.IconShadow.rectTransform.Stretch(inset + 1.5f, inset - 1.5f, inset - 1.5f, inset + 1.5f);
            }
            v.Icon = Image("Icon", holder, Color.white, Icons.Get(d.Icon));
            v.Icon.preserveAspect = true;
            v.Icon.rectTransform.Stretch(inset, inset, inset, inset);

            if (d.Kind == SkillKind.Tradeoff)
            {
                var badge = Image("Trade", holder, Theme.Warn, Circle);
                badge.rectTransform.Place(new Vector2(1, 1), new Vector2(-3, -3), Vector2.one * 24);
                var bi = Image("I", badge.transform, Color.white, Icons.Get("ui_tradeoff"));
                bi.preserveAspect = true;
                bi.rectTransform.Stretch(4, 4, 4, 4);
            }
            if (SkillDb.CombosOf(d.Id).Any())
            {
                v.Combo = Image("Combo", holder, Theme.Gold, Circle);
                v.Combo.rectTransform.Place(new Vector2(0, 1), new Vector2(3, -3), Vector2.one * 24);
                var ci = Image("I", v.Combo.transform, new Color(0.13f, 0.13f, 0.13f), Icons.Get("ui_combo"));
                ci.preserveAspect = true;
                ci.rectTransform.Stretch(4, 4, 4, 4);
            }
            // rank pips: a column right next to the icon (connector lines never run there)
            if (d.MaxRank > 1)
            {
                v.Pips = new Image[d.MaxRank];
                for (int i = 0; i < d.MaxRank; i++)
                {
                    float y = ((d.MaxRank - 1) / 2f - i) * 19f;
                    var rim = Image("PipRim" + i, holder, Theme.Background, Circle);
                    rim.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(size / 2f + 14f, y), Vector2.one * 18);
                    var pip = Image("Pip" + i, rim.transform, Theme.Locked, Circle);
                    pip.rectTransform.Stretch(3f, 3f, 3f, 3f);
                    v.Pips[i] = pip;
                }
            }
            if (d.IsMastery)
            {
                v.Lock = Image("Lock", holder, Theme.TextDim, Icons.Get("ui_lock"));
                v.Lock.preserveAspect = true;
                v.Lock.rectTransform.Place(new Vector2(1, 0), new Vector2(-4, 4), Vector2.one * 24, new Vector2(1, 0));
            }
            return v;
        }

        void Select(SkillDef d)
        {
            bool changed = selected != d;
            selected = d;
            Refresh();
            if (changed && nodes.TryGetValue(d.Id, out var v))
            {
                Tweens.Pop(v.Holder, 0.1f, 0.2f, 0f, v.Scale);
                var sel = v.Select;
                Tweens.Run(sel.GetInstanceID(), 0.18f, k => { if (sel != null) sel.color = Color.white.WithAlpha(Tweens.EaseOut(k)); });
            }
        }

        // =====================================================================
        // DETAIL
        // =====================================================================
        void BuildDetail()
        {
            // flat panel with a plain divider (native Products app detail column)
            var panel = Image("Panel", detailArea, Theme.SurfaceHi, null);
            panel.rectTransform.Stretch();
            var divider = Image("Divider", detailArea, Theme.Stroke, null);
            divider.rectTransform.Anchor(new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(1, 0));
            var c = panel.transform;
            const float icon = 76f;

            dIconBg = Image("IconBg", c, Theme.Card, Circle);
            dIconBg.rectTransform.Place(new Vector2(0, 1), new Vector2(Metrics.Pad + icon / 2f, -Metrics.Pad - icon / 2f), Vector2.one * icon);
            dIcon = Image("Icon", dIconBg.transform, Color.white, null);
            dIcon.preserveAspect = true;
            dIcon.rectTransform.Stretch(16, 16, 16, 16);

            float textX = Metrics.Pad + icon + 12;
            dName = Label("Name", c, "", Metrics.FontHeader + 2, Theme.Text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            dName.enableAutoSizing = true;
            dName.fontSizeMin = 18;
            dName.fontSizeMax = Metrics.FontHeader + 2;
            dName.enableWordWrapping = false;
            dName.rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(textX, -Metrics.Pad - 38), new Vector2(-Metrics.Pad, -Metrics.Pad - 2));
            // kind tag: small rounded rectangle, like the game's own labels
            dBadgeBg = Image("Badge", c, Theme.Card, Round4);
            dBadgeBg.rectTransform.Place(new Vector2(0, 1), new Vector2(textX, -Metrics.Pad - 44), new Vector2(112, 28), new Vector2(0, 1));
            dBadge = Label("T", dBadgeBg.transform, "", Metrics.FontTiny - 1, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            dBadge.rectTransform.Stretch();
            dRank = Label("Rank", c, "", Metrics.FontTiny, Theme.TextDim, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            dRank.rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(textX + 122, -Metrics.Pad - 72), new Vector2(-Metrics.Pad, -Metrics.Pad - 44));

            dText = Label("Text", c, "", Metrics.FontSmall + 1, Theme.Text, TextAlignmentOptions.TopLeft);
            dText.lineSpacing = 4;
            dText.enableAutoSizing = true; // long combo lists shrink a little instead of overflowing
            dText.fontSizeMin = 14;
            dText.fontSizeMax = Metrics.FontSmall + 1;
            dText.rectTransform.Anchor(new Vector2(0, 0), new Vector2(1, 1), new Vector2(Metrics.Pad, 122), new Vector2(-Metrics.Pad, -Metrics.Pad - icon - 14));

            dEquipRow = Rect("Equip", c).Anchor(new Vector2(0, 0), new Vector2(1, 0), new Vector2(Metrics.Pad, 72), new Vector2(-Metrics.Pad, 112));
            var eq = Label("L", dEquipRow, "Slot", Metrics.FontTiny, Theme.TextDim, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            eq.rectTransform.Anchor(Vector2.zero, new Vector2(0.2f, 1));
            for (int i = 0; i < ActiveLoadout.Slots; i++)
            {
                int slot = i;
                float w = 0.8f / ActiveLoadout.Slots;
                var b = Image("S" + i, dEquipRow, Theme.Card, Round8);
                b.rectTransform.Anchor(new Vector2(0.2f + w * i, 0), new Vector2(0.2f + w * (i + 1), 1), new Vector2(3, 0), new Vector2(-3, 0));
                Button(b, () => EquipSelected(slot), Theme.Card, Lighten(Theme.Card, 0.08f), Darken(Theme.Card, 0.2f));
                dEquip[i] = b;
                dEquipLabel[i] = Label("K", b.transform, "", Metrics.FontSmall, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
                dEquipLabel[i].rectTransform.Stretch();
            }

            dButton = Image("Learn", c, Theme.Accent, Round8);
            dButton.rectTransform.Anchor(new Vector2(0, 0), new Vector2(1, 0), new Vector2(Metrics.Pad, Metrics.Pad), new Vector2(-Metrics.Pad, Metrics.Pad + 46));
            Button(dButton, OnLearn, Theme.Accent, Theme.Accent, Theme.Accent);
            dButtonText = Label("T", dButton.transform, "", Metrics.FontBody, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            dButtonText.rectTransform.Stretch();
        }

        void EquipSelected(int slot)
        {
            if (selected == null || !selected.IsActive || !Skills.Has(selected.Id)) return;
            if (ActiveManager.Slot(slot) == selected.Id) ActiveManager.Unequip(slot);
            else ActiveManager.Equip(slot, selected.Id);
        }

        void OnLearn()
        {
            if (selected == null) return;
            var d = selected;
            if (d.IsMastery && Skills.CanLearn(d) == Skills.LockReason.None && Confirm != null)
            {
                string others = string.Join(" and ", SkillDb.All.Where(o => o.Branch == d.Branch && o.IsMastery && o != d).Select(o => o.Name));
                Confirm($"Choose {d.Name}?",
                    $"Each tree allows only <b>one</b> Major Mastery. Choosing {d.Name} locks {others} until you reset your skills.",
                    "Choose", () => { if (Skills.TryLearn(d)) Sfx.Mastery(); else Sfx.Error(); });
                return;
            }
            if (Skills.TryLearn(d)) Sfx.Learn(Skills.Rank(d.Id));
            else Sfx.Error();
        }

        // =====================================================================
        // REFRESH
        // =====================================================================
        public void Refresh()
        {
            if (Root == null || nodes.Count == 0) return;
            var info = SkillDb.Info(Current);
            int spent = Skills.SpentInBranch(Current);
            title.text = info.Name.ToUpperInvariant();
            title.color = info.Color;
            tagline.text = info.Tagline;
            progress.text = $"{spent}<size=65%><color=#{Hex6(Theme.TextDim)}> / {SkillDb.PointsPerBranch}</color></size>";

            foreach (var (label, line, tier) in gates)
            {
                bool open = spent >= SkillDb.TierGate[tier];
                label.color = tier == 5 ? Theme.Gold.WithAlpha(open ? 1f : 0.6f) : open ? info.Color.WithAlpha(0.9f) : Theme.TextFaint;
                if (tier == 5 && masterySpark != null) masterySpark.color = Theme.Gold.WithAlpha(open ? 1f : 0.6f);
                line.color = (open ? info.Color : Theme.Stroke).WithAlpha(0.25f);
            }

            bool complete = spent >= SkillDb.PointsPerBranch;
            bool masteryChosen = SkillDb.All.Any(o => o.Branch == Current && o.IsMastery && Skills.Has(o.Id));
            foreach (var l in links)
            {
                bool parentOn = l.ToMastery ? complete : Skills.Has(l.Parent);
                bool childOn = Skills.Has(l.Child);
                bool dead = l.ToMastery && masteryChosen && !childOn;
                Color baseC = dead ? Theme.Stroke.WithAlpha(0.35f)
                    : parentOn ? (l.ToMastery ? Theme.Gold : info.Color).WithAlpha(0.35f)
                    : Theme.Stroke.WithAlpha(0.7f);
                foreach (var b in l.Base) b.color = baseC;
                float target = childOn ? 1f : 0f;
                if (fresh || target < l.Target) SetFill(l, target);
                l.Target = target;
            }
            foreach (var (img, combo) in comboLinks)
            {
                bool on = Skills.Has(combo.A) && Skills.Has(combo.B);
                bool focus = selected != null && (combo.A == selected.Id || combo.B == selected.Id);
                img.color = Theme.Gold.WithAlpha(focus ? (on ? 0.85f : 0.5f) : 0f);
            }

            bool masteryTaken = masteryChosen;
            foreach (var v in nodes.Values)
            {
                var d = v.Def;
                int rank = Skills.Rank(d.Id);
                var reason = Skills.CanLearn(d);
                bool learned = rank > 0;
                bool available = reason == Skills.LockReason.None;
                bool reachable = learned || available || reason == Skills.LockReason.NoPoints;
                Color accent = d.IsMastery ? Theme.Gold : info.Color;
                if (d.IsMastery && masteryTaken && !learned) reachable = false;

                bool lockedOut = d.IsMastery && masteryTaken && !learned;
                if (d.IsMastery)
                {
                    // masteries: a cut diamond once open (dim until chosen, clear when chosen); the chosen one grows
                    bool open = complete && !lockedOut;
                    v.Ring.color = learned ? Lighten(Theme.Gold, 0.3f) : lockedOut ? Theme.Stroke.WithAlpha(0.5f) : open ? Theme.Gold : Theme.Gold.WithAlpha(0.45f);
                    Color bg = Hex("1B1A16");
                    SetButtonColors(v.Bg.GetComponent<Button>(), bg, Lighten(bg, 0.08f), Darken(bg, 0.2f));
                    v.Gem.gameObject.SetActive(learned || open);
                    // gold-tinted cut diamond: soft while it can be picked, solid once chosen
                    v.Gem.color = learned ? new Color(1f, 0.82f, 0.32f, 0.92f) : new Color(1f, 0.82f, 0.32f, 0.38f);
                    v.IconShadow.gameObject.SetActive(learned || open);
                    v.IconShadow.color = Color.black.WithAlpha(learned ? 0.75f : 0.6f);
                    v.Icon.color = learned || open ? Color.white : lockedOut ? Theme.TextFaint.WithAlpha(0.45f) : Theme.Gold.WithAlpha(0.6f);
                    v.TargetScale = learned ? 1.22f : lockedOut ? 0.9f : 1f;
                    if (v.Lock != null) v.Lock.gameObject.SetActive(lockedOut);
                    v.Glow.gameObject.SetActive(false);
                }
                else
                {
                    v.Ring.color = learned ? accent : available ? accent.WithAlpha(0.75f) : Theme.Stroke.WithAlpha(reachable ? 0.9f : 0.5f);
                    SetButtonColors(v.Bg.GetComponent<Button>(),
                        learned ? Color.Lerp(Theme.Card, accent, 0.28f) : Theme.Card,
                        learned ? Color.Lerp(Theme.Card, accent, 0.38f) : Lighten(Theme.Card, 0.07f),
                        Darken(Theme.Card, 0.2f));
                    v.Icon.color = learned ? Color.white : reachable ? accent : Theme.TextFaint.WithAlpha(0.7f);
                    v.Glow.gameObject.SetActive(available && !learned);
                }
                v.Select.gameObject.SetActive(selected == d);
                if (v.Combo != null)
                {
                    bool comboOn = SkillDb.CombosOf(d.Id).Any(c => Skills.Has(c.A) && Skills.Has(c.B));
                    v.Combo.color = comboOn ? Theme.Gold : Theme.Gold.WithAlpha(0.35f);
                }
                if (v.Pips != null)
                    for (int i = 0; i < v.Pips.Length; i++)
                        v.Pips[i].color = i < rank ? accent : Theme.Locked;
            }
            if (fresh)
            {
                foreach (var v in nodes.Values)
                {
                    v.Scale = v.TargetScale;
                    v.Holder.localScale = Vector3.one * v.Scale;
                }
                fresh = false;
            }
            PlayChanges(complete);
            RefreshDetail();
        }

        static string Hex6(Color c) => ColorUtility.ToHtmlStringRGB(c);

        void RefreshDetail()
        {
            var d = selected;
            if (d == null) return;
            var info = SkillDb.Info(d.Branch);
            int rank = Skills.Rank(d.Id);
            var reason = Skills.CanLearn(d);
            Color accent = d.IsMastery ? Theme.Gold : info.Color;

            dIconBg.sprite = d.IsActive ? Round16 : d.IsMastery ? Diamond : Circle;
            dIconBg.color = rank > 0 ? Color.Lerp(Theme.Card, accent, 0.35f) : Theme.Card;
            dIcon.sprite = Icons.Get(d.Icon);
            dIcon.color = rank > 0 ? Color.white : accent;
            dName.text = d.Name;
            switch (d.Kind)
            {
                case SkillKind.Passive: SetBadge("Passive", Color.Lerp(accent, Theme.Card, 0.45f)); break;
                case SkillKind.Active: SetBadge("Active", accent); break;
                case SkillKind.Tradeoff: SetBadge("Trade-off", Theme.Warn); break;
                case SkillKind.Mastery: SetBadge("Mastery", Theme.Gold); break;
            }
            dRank.text = d.IsMastery ? (rank > 0 ? "Chosen" : "") : d.MaxRank > 1 ? $"Rank {rank}/{d.MaxRank}" : rank > 0 ? "Learned" : "";

            var sb = new StringBuilder();
            sb.Append(d.Describe(rank));
            if (d.MaxRank > 1)
            {
                sb.Append("\n<size=88%>");
                for (int r = 1; r <= d.MaxRank; r++)
                {
                    string col = r == rank ? Hex6(accent) : r < rank ? Hex6(Theme.TextDim) : Hex6(Theme.TextFaint);
                    sb.Append($"<color=#{col}>{(r == rank ? "• " : "   ")}Rank {r}: {RankValue(d, r)}</color>");
                    if (r < d.MaxRank) sb.Append('\n');
                }
                sb.Append("</size>");
            }
            if (!string.IsNullOrEmpty(d.Drawback))
                sb.Append($"\n<color=#{Hex6(Theme.Warn)}><b>Drawback:</b> {string.Format(d.Drawback, d.Value, d.Value2)}</color>");
            if (d.IsActive)
            {
                string cd = d.Clock == CooldownClock.GameMinutes ? GameSpan(d.Cooldown) : RealSpan(d.Cooldown);
                string dur = d.Duration > 0 ? $"Lasts {(d.Clock == CooldownClock.GameMinutes ? GameSpan(d.Duration) : RealSpan(d.Duration))} · " : "Instant · ";
                sb.Append($"\n<size=88%><color=#{Hex6(Theme.TextDim)}>{dur}Cooldown {cd}</color></size>");
            }

            var combos = SkillDb.CombosOf(d.Id).ToList();
            if (combos.Count > 0)
            {
                sb.Append($"\n<color=#{Hex6(Theme.Gold)}><b>Combo</b></color>");
                foreach (var c in combos)
                {
                    string partner = c.A == d.Id ? c.B : c.A;
                    var p = SkillDb.Get(partner);
                    bool on = Skills.Has(c.A) && Skills.Has(c.B);
                    string head = on ? $"<color=#{Hex6(Theme.Gold)}>{c.Name} (active)</color>" : $"<color=#{Hex6(Theme.Gold)}>{c.Name}</color> <color=#{Hex6(Theme.TextFaint)}>· with {p?.Name}</color>";
                    sb.Append($"\n<size=90%>{head}\n<color=#{Hex6(on ? Theme.Text : Theme.TextDim)}>{c.Desc}</color></size>");
                }
            }

            string lockText = reason switch
            {
                Skills.LockReason.ParentMissing => $"Requires {string.Join(" and ", d.Parents.Select(p => SkillDb.Get(p)?.Name))}.",
                Skills.LockReason.TierLocked => $"Spend {Skills.TierRequirement(d.Tier)} points in {info.Name} to reach this level.",
                Skills.LockReason.BranchIncomplete => $"Complete the {info.Name} tree ({SkillDb.PointsPerBranch} points) to pick a Major Mastery.",
                Skills.LockReason.MasteryTaken => $"You already chose {SkillDb.All.FirstOrDefault(o => o.Branch == d.Branch && o.IsMastery && Skills.Has(o.Id))?.Name} for this tree. Only one Major Mastery per tree; a skill reset lets you pick again.",
                Skills.LockReason.NoMasteryPoint => $"Mastery points come at {Progression.MasteryRanksLabel()}.",
                Skills.LockReason.NoPoints => Progression.NextPointRankLabel() is string next ? $"No skill points left. Next one at {next}." : "No skill points left.",
                _ => ""
            };
            if (lockText.Length > 0) sb.Append($"\n<size=88%><color=#{Hex6(Theme.Gold)}>{lockText}</color></size>");
            dText.text = sb.ToString();

            bool can = reason == Skills.LockReason.None;
            string verb = d.IsMastery ? "Choose" : rank > 0 ? "Upgrade" : "Learn";
            string cost = d.IsMastery ? "1 MP" : $"{d.CostPerRank} SP";
            dButtonText.text = reason == Skills.LockReason.Maxed ? (d.IsMastery ? "Chosen" : d.MaxRank > 1 ? "Maxed" : "Learned") : $"{verb}  ·  {cost}";
            Color bc = can ? accent : Theme.Card;
            SetButtonColors(dButton.GetComponent<Button>(), bc, can ? Lighten(bc, 0.12f) : bc, can ? Darken(bc, 0.2f) : bc);
            dButtonText.color = can ? (Luma(bc) > 0.65f ? new Color(0.12f, 0.12f, 0.12f) : Color.white) : Theme.TextFaint;

            bool showEquip = d.IsActive && rank > 0;
            dEquipRow.gameObject.SetActive(showEquip);
            if (showEquip)
            {
                for (int i = 0; i < ActiveLoadout.Slots; i++)
                {
                    bool here = ActiveManager.Slot(i) == d.Id;
                    SetButtonColors(dEquip[i].GetComponent<Button>(), here ? accent : Theme.Card, Lighten(here ? accent : Theme.Card, 0.1f), Darken(Theme.Card, 0.2f));
                    dEquipLabel[i].text = Prefs.KeyLabel(Prefs.SlotKeys[i].Value);
                    dEquipLabel[i].color = here ? Color.white : Theme.TextDim;
                }
            }
        }

        static float Luma(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        void SetBadge(string text, Color c)
        {
            dBadge.text = text;
            dBadgeBg.color = c;
            dBadge.color = Luma(c) > 0.65f ? new Color(0.12f, 0.12f, 0.12f) : Color.white;
        }

        static string RankValue(SkillDef d, int r)
        {
            string num = SkillDef.Fmt(d.Value * r);
            int i = d.Desc.IndexOf("{0}");
            if (i < 0) return num;
            string before = i > 0 ? d.Desc.Substring(i - 1, 1) : "";
            string after = i + 3 < d.Desc.Length ? d.Desc.Substring(i + 3, 1) : "";
            return (before == "$" ? "$" : "") + num + (after == "%" ? "%" : after == "m" ? "m" : "");
        }

        public static string GameSpan(float minutes)
        {
            int m = (int)minutes;
            if (m % 1440 == 0) return m == 1440 ? "1 day" : $"{m / 1440} days";
            if (m % 60 == 0) return m == 60 ? "1 hour" : $"{m / 60} hours";
            return $"{m} min";
        }

        public static string RealSpan(float seconds)
        {
            int s = (int)seconds;
            if (s >= 60 && s % 60 == 0) return s == 60 ? "1 real minute" : $"{s / 60} real minutes";
            return $"{s} seconds";
        }

        /// <summary>Animations for what changed since the last refresh: lines grow toward newly learned skills, then
        /// the skill pops; a chosen mastery bursts and grows.</summary>
        void PlayChanges(bool complete)
        {
            const float fillTime = 0.42f;
            foreach (var v in nodes.Values)
            {
                var d = v.Def;
                int rank = Skills.Rank(d.Id);
                lastRank.TryGetValue(d.Id, out int before);
                lastRank[d.Id] = rank;
                if (rank <= before) continue;

                bool first = before == 0;
                bool hasLink = false;
                foreach (var l in links)
                {
                    if (l.Child != d.Id || l.Progress >= 1f) continue;
                    hasLink = true;
                    var link = l;
                    SetFill(link, 0f);
                    Tweens.Run(link, fillTime, k => SetFill(link, Tweens.EaseInOut(k)));
                }
                float delay = first && hasLink ? fillTime * 0.85f : 0f;
                Tweens.Pop(v.Holder, d.IsMastery ? 0.28f : 0.18f, 0.3f, delay, d.IsMastery ? 1.22f : 1f);
                if (d.IsMastery) v.Scale = 1.22f;
                var glow = v.Glow;
                Color gc = (d.IsMastery ? Theme.Gold : SkillDb.Info(Current).Color);
                glow.gameObject.SetActive(true);
                Tweens.Run(glow.GetInstanceID(), 0.55f, k => { if (glow != null) glow.color = gc.WithAlpha(0.85f * (1f - k)); }, delay,
                    () => { if (glow != null) glow.gameObject.SetActive(false); });
                if (v.Pips != null && rank - 1 < v.Pips.Length)
                    Tweens.Pop(v.Pips[rank - 1].transform.parent.GetComponent<RectTransform>(), 0.6f, 0.25f, delay);
                if (d.IsMastery) Burst(v, delay);
            }
            if (complete && !lastComplete)
            {
                // the masteries open up: a light pop on each
                foreach (var v in nodes.Values.Where(n => n.Def.IsMastery))
                    Tweens.Pop(v.Holder, 0.12f, 0.3f, 0.25f, v.Scale);
                Sfx.TreeComplete();
            }
            lastComplete = complete;
        }

        /// <summary>Expanding gold outline from a newly chosen mastery.</summary>
        void Burst(NodeView v, float delay)
        {
            var ring = Image("Burst", v.Holder, Color.white.WithAlpha(0f), DiamondRing);
            ring.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * MasterySize);
            Tweens.Run(ring.GetInstanceID(), 0.7f, k =>
            {
                if (ring == null) return;
                float e = Tweens.EaseOut(k);
                ring.rectTransform.localScale = Vector3.one * (1f + 1.6f * e);
                ring.color = new Color(1f, 0.97f, 0.88f, 0.9f * (1f - k));
            }, delay, () => { if (ring != null) Object.Destroy(ring.gameObject); });
        }

        /// <summary>Per frame: glow pulses, mastery size easing.</summary>
        public void Animate()
        {
            if (Root == null || !Root.gameObject.activeInHierarchy) return;
            var info = SkillDb.Info(Current);
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f);
            float a = 0.22f + 0.2f * wave;
            float dt = Time.unscaledDeltaTime;
            bool complete = Skills.SpentInBranch(Current) >= SkillDb.PointsPerBranch;
            foreach (var v in nodes.Values)
            {
                if (!v.Def.IsMastery)
                {
                    if (v.Glow.gameObject.activeSelf && !Tweens.IsRunning(v.Glow.GetInstanceID())) v.Glow.color = info.Color.WithAlpha(a);
                    continue;
                }
                // ease masteries toward their size (chosen one larger) unless a pop is playing
                if (!Mathf.Approximately(v.Scale, v.TargetScale))
                {
                    v.Scale = Mathf.MoveTowards(v.Scale, v.TargetScale, dt * 1.6f);
                    if (!Tweens.IsRunning(v.Holder.GetInstanceID())) v.Holder.localScale = Vector3.one * v.Scale;
                }
            }
        }
    }
}
