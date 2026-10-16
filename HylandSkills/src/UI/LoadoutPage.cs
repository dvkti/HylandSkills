using System.Collections.Generic;
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
    /// <summary>
    /// Active skills menu: the four slots on the left (key, assigned skill, remove), every learned active skill on
    /// the right. Pick a slot, then click a skill to put it there; click a key to rebind it.
    /// </summary>
    public class LoadoutPage
    {
        public RectTransform Root;
        int selectedSlot;

        readonly Image[] slotRow = new Image[ActiveLoadout.Slots];
        readonly Image[] slotOutline = new Image[ActiveLoadout.Slots];
        readonly Image[] slotIcon = new Image[ActiveLoadout.Slots];
        readonly TextMeshProUGUI[] slotKey = new TextMeshProUGUI[ActiveLoadout.Slots];
        readonly TextMeshProUGUI[] slotName = new TextMeshProUGUI[ActiveLoadout.Slots];
        readonly TextMeshProUGUI[] slotSub = new TextMeshProUGUI[ActiveLoadout.Slots];
        readonly Image[] slotRemove = new Image[ActiveLoadout.Slots];

        TextMeshProUGUI listCaption, emptyText;

        sealed class Row
        {
            public SkillDef Def;
            public Button Button;
            public Image Tag;
            public TextMeshProUGUI TagText;
        }
        readonly List<Row> rows = new List<Row>();
        string builtFor = "";
        RectTransform listContent;
        ScrollRect listScroll;
        const float RowH = 76f;

        public LoadoutPage(RectTransform body)
        {
            Root = Rect("LoadoutPage", body).Stretch();
            BuildSlots();
            BuildList();
        }

        public void SelectSlot(int slot)
        {
            selectedSlot = Mathf.Clamp(slot, 0, ActiveLoadout.Slots - 1);
            Refresh();
        }

        // ---------------------------------------------------------------- slots
        void BuildSlots()
        {
            var card = Image("SlotsCard", Root, Theme.Surface, Round16);
            card.rectTransform.Anchor(new Vector2(0, 0), new Vector2(0.43f, 1), new Vector2(Metrics.Pad, Metrics.Pad), new Vector2(-Metrics.Pad * 0.5f, -Metrics.Pad));
            var c = card.transform;
            var title = Label("Title", c, "Active slots", Metrics.FontHeader, Theme.Text, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            title.rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(Metrics.Pad, -Metrics.Pad - 30), new Vector2(-Metrics.Pad, -Metrics.Pad));
            var caption = Label("Caption", c, "Pick a slot, then choose a skill on the right. Click a key to change it.", Metrics.FontTiny, Theme.TextDim, TextAlignmentOptions.TopLeft);
            caption.rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(Metrics.Pad, -Metrics.Pad - 80), new Vector2(-Metrics.Pad, -Metrics.Pad - 34));

            float top = -Metrics.Pad - 88;
            const float h = 92f, gap = 12f;
            for (int i = 0; i < ActiveLoadout.Slots; i++)
            {
                int slot = i;
                float y = top - i * (h + gap);
                var outline = Image("Outline" + i, c, Theme.Accent, Round8);
                outline.rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(Metrics.Pad - 3, y - h - 3), new Vector2(-Metrics.Pad + 3, y + 3));
                slotOutline[i] = outline;

                var row = Image("Slot" + i, c, Theme.Card, Round8);
                row.rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(Metrics.Pad, y - h), new Vector2(-Metrics.Pad, y));
                Button(row, () => SelectSlot(slot), Theme.Card, Lighten(Theme.Card, 0.06f), Darken(Theme.Card, 0.2f));
                slotRow[i] = row;

                var key = Image("Key", row.transform, Theme.SurfaceHi, Round8);
                key.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(64, 64), new Vector2(0, 0.5f));
                Button(key, () => { SelectSlot(slot); KeyRebinder.Listen(slot, Refresh); Refresh(); }, Theme.SurfaceHi, Lighten(Theme.SurfaceHi, 0.1f), Darken(Theme.SurfaceHi, 0.2f));
                slotKey[i] = Label("K", key.transform, "Z", Metrics.FontHeader, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
                slotKey[i].enableAutoSizing = true;
                slotKey[i].fontSizeMin = 12;
                slotKey[i].fontSizeMax = Metrics.FontHeader;
                slotKey[i].rectTransform.Stretch(4, 4, 4, 4);

                slotIcon[i] = Image("Icon", row.transform, Color.white, null);
                slotIcon[i].preserveAspect = true;
                slotIcon[i].rectTransform.Place(new Vector2(0, 0.5f), new Vector2(92, 0), Vector2.one * 44, new Vector2(0, 0.5f));

                slotName[i] = Label("Name", row.transform, "", Metrics.FontBody, Theme.Text, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
                slotName[i].enableWordWrapping = false;
                slotName[i].overflowMode = TextOverflowModes.Ellipsis;
                slotName[i].rectTransform.Anchor(new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(146, 0), new Vector2(-104, -10));
                slotSub[i] = Label("Sub", row.transform, "", Metrics.FontTiny, Theme.TextDim, TextAlignmentOptions.TopLeft);
                slotSub[i].enableWordWrapping = false;
                slotSub[i].overflowMode = TextOverflowModes.Ellipsis;
                slotSub[i].rectTransform.Anchor(new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(146, 10), new Vector2(-104, -2));

                var remove = Image("Remove", row.transform, Theme.SurfaceHi, Round8);
                remove.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(84, 40), new Vector2(1, 0.5f));
                Button(remove, () => { ActiveManager.Unequip(slot); Sfx.Unequip(); }, Theme.SurfaceHi, Lighten(Theme.Warn, 0.1f), Darken(Theme.Warn, 0.1f));
                Label("T", remove.transform, "Remove", Metrics.FontTiny, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold).rectTransform.Stretch();
                slotRemove[i] = remove;
            }
        }

        // ---------------------------------------------------------------- list of learned actives
        void BuildList()
        {
            var card = Image("ListCard", Root, Theme.Surface, Round16);
            card.rectTransform.Anchor(new Vector2(0.43f, 0), new Vector2(1, 1), new Vector2(Metrics.Pad * 0.5f, Metrics.Pad), new Vector2(-Metrics.Pad, -Metrics.Pad));
            var c = card.transform;
            var title = Label("Title", c, "Your active skills", Metrics.FontHeader, Theme.Text, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            title.rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(Metrics.Pad, -Metrics.Pad - 30), new Vector2(-Metrics.Pad, -Metrics.Pad));
            listCaption = Label("Caption", c, "", Metrics.FontTiny, Theme.TextDim, TextAlignmentOptions.TopLeft);
            listCaption.rectTransform.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(Metrics.Pad, -Metrics.Pad - 62), new Vector2(-Metrics.Pad, -Metrics.Pad - 34));

            var viewport = Image("Viewport", c, new Color(0, 0, 0, 0), null, true);
            viewport.rectTransform.Anchor(new Vector2(0, 0), new Vector2(1, 1), new Vector2(Metrics.Pad, Metrics.Pad), new Vector2(-Metrics.Pad - 12, -Metrics.Pad - 72));
            viewport.gameObject.AddComponent<RectMask2D>();
            listContent = Rect("Content", viewport.transform);
            listContent.anchorMin = new Vector2(0, 1);
            listContent.anchorMax = new Vector2(1, 1);
            listContent.pivot = new Vector2(0.5f, 1);
            listContent.sizeDelta = new Vector2(0, 0);

            var track = Image("Scrollbar", c, Theme.Card, Round4, true);
            track.rectTransform.Anchor(new Vector2(1, 0), new Vector2(1, 1), new Vector2(-Metrics.Pad - 6, Metrics.Pad), new Vector2(-Metrics.Pad, -Metrics.Pad - 72));
            var handle = Image("Handle", Rect("Sliding", track.transform).Stretch(), Theme.Stroke, Round4, true);
            handle.rectTransform.Stretch();
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle.rectTransform;
            bar.targetGraphic = handle;
            bar.direction = Scrollbar.Direction.BottomToTop;
            var nav = bar.navigation;
            nav.mode = Navigation.Mode.None;
            bar.navigation = nav;

            listScroll = card.gameObject.AddComponent<ScrollRect>();
            listScroll.viewport = viewport.rectTransform;
            listScroll.content = listContent;
            listScroll.horizontal = false;
            listScroll.movementType = ScrollRect.MovementType.Clamped;
            listScroll.scrollSensitivity = 36f;
            listScroll.verticalScrollbar = bar;
            listScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            emptyText = Label("Empty", viewport.transform, "You haven't learned any active skills yet.\nActive skills are the rounded-square nodes in each tree.", Metrics.FontSmall, Theme.TextFaint, TextAlignmentOptions.Center);
            emptyText.rectTransform.Stretch(10, 0, 10, 0);
        }

        static IEnumerable<SkillDef> Learned() =>
            SkillDb.All.Where(d => d.IsActive && Skills.Has(d.Id)).OrderBy(d => (int)d.Branch).ThenBy(d => d.Tier);

        /// <summary>Rows are created only when the set of learned skills changes and updated in place otherwise
        /// (rebuilding them inside a click left the new buttons stuck in a washed-out tint).</summary>
        void RebuildList()
        {
            var list = Learned().ToList();
            string key = string.Join(",", list.Select(d => d.Id));
            if (key != builtFor) BuildRows(list, key);
            foreach (var r in rows)
            {
                var info = SkillDb.Info(r.Def.Branch);
                int inSlot = ActiveManager.SlotOf(r.Def.Id);
                Color bg = inSlot == selectedSlot ? Color.Lerp(Theme.Card, info.Color, 0.25f) : Theme.Card;
                SetButtonColors(r.Button, bg, Lighten(bg, 0.07f), Darken(bg, 0.2f));
                r.Tag.color = inSlot >= 0 ? Theme.Accent : Theme.SurfaceHi;
                r.TagText.text = inSlot >= 0 ? $"In slot {Prefs.KeyLabel(Prefs.SlotKeys[inSlot].Value)}" : "Equip";
                r.TagText.color = inSlot >= 0 ? Color.white : Theme.Text;
            }
        }

        void BuildRows(List<SkillDef> list, string key)
        {
            builtFor = key;
            rows.Clear();
            Clear(listContent);
            emptyText.gameObject.SetActive(list.Count == 0);
            listContent.sizeDelta = new Vector2(0, list.Count * (RowH + 8));
            for (int i = 0; i < list.Count; i++)
            {
                var d = list[i];
                var info = SkillDb.Info(d.Branch);
                bool here = ActiveManager.SlotOf(d.Id) == selectedSlot;

                var row = Image("Row_" + d.Id, listContent, Theme.Card, Round8);
                row.rectTransform.anchorMin = new Vector2(0, 1);
                row.rectTransform.anchorMax = new Vector2(1, 1);
                row.rectTransform.pivot = new Vector2(0.5f, 1);
                row.rectTransform.sizeDelta = new Vector2(0, RowH);
                row.rectTransform.anchoredPosition = new Vector2(0, -i * (RowH + 8));
                var def = d;
                Color bg = here ? Color.Lerp(Theme.Card, info.Color, 0.25f) : Theme.Card;
                var button = Button(row, () => Assign(def), bg, Lighten(bg, 0.07f), Darken(bg, 0.2f));

                var iconBg = Image("IconBg", row.transform, Theme.SurfaceHi, Round8);
                iconBg.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(12, 0), Vector2.one * 54, new Vector2(0, 0.5f));
                var icon = Image("Icon", iconBg.transform, info.Color, Icons.Get(d.Icon));
                icon.preserveAspect = true;
                icon.rectTransform.Stretch(10, 10, 10, 10);

                var name = Label("Name", row.transform, d.Name, Metrics.FontBody, Theme.Text, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
                name.enableWordWrapping = false;
                name.overflowMode = TextOverflowModes.Ellipsis;
                name.rectTransform.Anchor(new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(80, 0), new Vector2(-130, -8));
                string cd = d.Clock == CooldownClock.GameMinutes ? BranchPage.GameSpan(d.Cooldown) : BranchPage.RealSpan(d.Cooldown);
                var sub = Label("Sub", row.transform, $"<color=#{ColorUtility.ToHtmlStringRGB(info.Color)}>{info.Name}</color> · Cooldown {cd}", Metrics.FontTiny, Theme.TextDim, TextAlignmentOptions.TopLeft);
                sub.enableWordWrapping = false;
                sub.overflowMode = TextOverflowModes.Ellipsis;
                sub.rectTransform.Anchor(new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(80, 8), new Vector2(-130, -2));

                var tag = Image("Tag", row.transform, Theme.SurfaceHi, Round4);
                tag.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(108, 34), new Vector2(1, 0.5f));
                var tagText = Label("T", tag.transform, "", Metrics.FontTiny, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
                tagText.rectTransform.Stretch();
                rows.Add(new Row { Def = d, Button = button, Tag = tag, TagText = tagText });
            }
        }

        void Assign(SkillDef d)
        {
            if (ActiveManager.Slot(selectedSlot) == d.Id) return;
            ActiveManager.Equip(selectedSlot, d.Id);
            Sfx.Equip();
            // jump to the next empty slot so filling the bar is quick
            for (int i = 1; i <= ActiveLoadout.Slots; i++)
            {
                int s = (selectedSlot + i) % ActiveLoadout.Slots;
                if (string.IsNullOrEmpty(ActiveManager.Slot(s))) { selectedSlot = s; break; }
            }
            Refresh();
        }

        public void Refresh()
        {
            if (Root == null) return;
            for (int i = 0; i < ActiveLoadout.Slots; i++)
            {
                var d = SkillDb.Get(ActiveManager.Slot(i));
                bool listening = KeyRebinder.IsListening && KeyRebinder.Slot == i;
                slotOutline[i].gameObject.SetActive(i == selectedSlot);
                slotKey[i].text = listening ? "Press a key" : Prefs.KeyLabel(Prefs.SlotKeys[i].Value);
                slotKey[i].color = listening ? Theme.Gold : Theme.Text;
                slotIcon[i].enabled = d != null;
                if (d != null)
                {
                    var info = SkillDb.Info(d.Branch);
                    slotIcon[i].sprite = Icons.Get(d.Icon);
                    slotIcon[i].color = info.Color;
                    slotName[i].text = d.Name;
                    string cd = d.Clock == CooldownClock.GameMinutes ? BranchPage.GameSpan(d.Cooldown) : BranchPage.RealSpan(d.Cooldown);
                    slotSub[i].text = $"{info.Name} · Cooldown {cd}";
                }
                else
                {
                    slotName[i].text = $"<color=#{ColorUtility.ToHtmlStringRGB(Theme.TextFaint)}>Empty</color>";
                    slotSub[i].text = i == selectedSlot ? "Choose a skill on the right" : "";
                }
                slotRemove[i].gameObject.SetActive(d != null);
            }
            string key = Prefs.KeyLabel(Prefs.SlotKeys[selectedSlot].Value);
            listCaption.text = $"Click a skill to put it in slot <b>{key}</b>. A skill can only be in one slot.";
            RebuildList();
        }
    }
}
