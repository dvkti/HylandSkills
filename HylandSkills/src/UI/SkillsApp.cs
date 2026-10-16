using System;
using HylandSkills.Core;
using HylandSkills.Data;
using HylandSkills.State;
using Il2CppTMPro;
using S1API.PhoneApp;
using UnityEngine;
using UnityEngine.UI;
using static HylandSkills.UI.UiKit;

namespace HylandSkills.UI
{
    /// <summary>
    /// The "Skills" phone app. Menu 1 is the category octagon, menu 2 the selected category's tree.
    /// S1API discovers PhoneApp subclasses and places the icon on the home screen.
    /// </summary>
    public class SkillsApp : PhoneApp
    {
        public static SkillsApp Instance { get; private set; }

        protected override string AppName => "HylandSkills";
        protected override string AppTitle => "Skills";
        protected override string IconLabel => "Skills";
        protected override string IconFileName => "hyland_skills_icon.png";
        protected override Sprite IconSprite => Icons.Get("app_icon");
        protected override EOrientation Orientation => EOrientation.Horizontal;

        enum Page { Overview, Branch, Settings, Loadout }

        public RectTransform Root { get; private set; }
        OverviewPage overview;
        BranchPage branch;
        SettingsPage settings;
        LoadoutPage loadout;
        Dialog dialog;
        Page page = Page.Overview;
        Page beforeSettings = Page.Overview;

        TextMeshProUGUI title, spLabel, mpLabel;
        Image back, logo, settingsBtn;

        public SkillsApp()
        {
            Instance = this;
        }

        protected override void OnCreatedUI(GameObject container)
        {
            Instance = this;
            try
            {
                Root = container.GetComponent<RectTransform>();
                Build(Root);
                Skills.Changed += Refresh;
                ShowPage(Page.Overview);
                Mod.Log.Msg($"Skills app UI built ({Root.rect.width:0}x{Root.rect.height:0}).");
            }
            catch (Exception e)
            {
                Mod.Log.Error("Skills app build failed: " + e);
            }
        }

        protected override void OnDestroyed()
        {
            Skills.Changed -= Refresh;
            base.OnDestroyed();
        }

        protected override void OnPhoneClosed()
        {
            KeyRebinder.Cancel();
        }

        /// <summary>Esc / back: tree → octagon first, then let the phone close the app.</summary>
        public override void Exit(ExitAction exit)
        {
            if (!exit.Used && IsOpen() && page != Page.Overview)
            {
                exit.Used = true;
                GoBack();
                return;
            }
            base.Exit(exit);
        }

        void Build(RectTransform root)
        {
            Image("Background", root, Theme.Background, null, true).rectTransform.Stretch();

            var bar = Image("Topbar", root, Theme.Header, null).rectTransform;
            bar.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -Metrics.TopBar), Vector2.zero);

            float h = Metrics.TopBar;
            back = Image("Back", bar, HeaderButton, Round8);
            back.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(Metrics.Pad + h * 0.34f, 0), Vector2.one * h * 0.68f);
            Button(back, GoBack, HeaderButton, Lighten(HeaderButton, 0.12f), Darken(HeaderButton, 0.2f));
            var bi = Image("Icon", back.transform, Theme.HeaderText, Icons.Get("ui_back"));
            bi.preserveAspect = true;
            bi.rectTransform.Stretch(9, 9, 9, 9);

            logo = Image("Logo", bar, Color.white, Icons.Get("app_icon"));
            logo.preserveAspect = true;
            logo.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(Metrics.Pad + h * 0.32f, 0), Vector2.one * h * 0.64f);

            title = Label("Title", bar, "Skills", Metrics.FontTitle, Theme.HeaderText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            title.rectTransform.Anchor(new Vector2(0, 0), new Vector2(0.45f, 1), new Vector2(Metrics.Pad + h * 0.82f, 0), Vector2.zero);

            var chips = Rect("Chips", bar).Anchor(new Vector2(0.45f, 0), new Vector2(0.8f, 1));
            spLabel = Chip(chips, "SP", -Metrics.ChipW * 0.55f, Theme.Accent);
            mpLabel = Chip(chips, "MP", Metrics.ChipW * 0.55f, Theme.Gold);

            settingsBtn = Image("Settings", bar, HeaderButton, Round8);
            settingsBtn.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(-Metrics.Pad - h * 0.34f, 0), Vector2.one * h * 0.68f);
            Button(settingsBtn, ToggleSettings, HeaderButton, Lighten(HeaderButton, 0.12f), Darken(HeaderButton, 0.2f));
            var gi = Image("Icon", settingsBtn.transform, Theme.HeaderText, Icons.Get("ui_settings"));
            gi.preserveAspect = true;
            gi.rectTransform.Stretch(9, 9, 9, 9);

            var actives = Image("Actives", bar, HeaderButton, Round8);
            actives.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(-Metrics.Pad * 3 - h * 1.7f, 0), Vector2.one * h * 0.68f);
            Button(actives, () => OpenLoadout(-1), HeaderButton, Lighten(HeaderButton, 0.12f), Darken(HeaderButton, 0.2f));
            var ai = Image("Icon", actives.transform, Theme.HeaderText, Icons.Get("zap"));
            ai.preserveAspect = true;
            ai.rectTransform.Stretch(9, 9, 9, 9);

            var respec = Image("Respec", bar, HeaderButton, Round8);
            respec.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(-Metrics.Pad * 2 - h * 1.02f, 0), Vector2.one * h * 0.68f);
            Button(respec, AskRespec, HeaderButton, Lighten(HeaderButton, 0.12f), Darken(HeaderButton, 0.2f));
            var ri = Image("Icon", respec.transform, Theme.HeaderText, Icons.Get("ui_respec"));
            ri.preserveAspect = true;
            ri.rectTransform.Stretch(9, 9, 9, 9);

            var body = Rect("Body", root).Stretch(0, 0, 0, Metrics.TopBar);
            overview = new OverviewPage(body, OpenBranch) { OpenLoadout = OpenLoadout };
            branch = new BranchPage(body);
            settings = new SettingsPage(body);
            loadout = new LoadoutPage(body);
            dialog = new Dialog(root);
            branch.Confirm = (t, b, ok, act) => dialog.Show(t, b, "Cancel", ok, act);
        }

        static Color HeaderButton => Lighten(Theme.Header, 0.12f);

        TextMeshProUGUI Chip(RectTransform parent, string tag, float x, Color color)
        {
            var chip = Image("Chip_" + tag, parent, new Color(0, 0, 0, 0.25f), Round8);
            chip.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(x, 0), new Vector2(Metrics.ChipW, Metrics.TopBar * 0.56f));
            var dot = Image("Dot", chip.transform, color, Circle);
            dot.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(16, 0), Vector2.one * 10);
            var t = Label("Tag", chip.transform, tag, Metrics.FontTiny, Theme.HeaderText.WithAlpha(0.7f), TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            t.rectTransform.Stretch(28, 0, 0, 0);
            var v = Label("Value", chip.transform, "0", Metrics.FontBody, Theme.HeaderText, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            v.rectTransform.Stretch(0, 0, 14, 0);
            return v;
        }

        void OpenBranch(Branch b)
        {
            branch.Show(b);
            ShowPage(Page.Branch);
        }

        /// <summary>Opens the active skills menu, optionally with a slot preselected (-1 keeps the current one).</summary>
        void OpenLoadout(int slot)
        {
            if (slot >= 0) loadout.SelectSlot(slot);
            ShowPage(Page.Loadout);
        }

        void GoBack()
        {
            if (page == Page.Settings) ShowPage(beforeSettings == Page.Settings ? Page.Overview : beforeSettings);
            else ShowPage(Page.Overview);
        }

        void ToggleSettings()
        {
            if (page == Page.Settings) GoBack();
            else
            {
                beforeSettings = page;
                ShowPage(Page.Settings);
            }
        }

        void ShowPage(Page p)
        {
            page = p;
            overview.Root.gameObject.SetActive(p == Page.Overview);
            branch.Root.gameObject.SetActive(p == Page.Branch);
            settings.Root.gameObject.SetActive(p == Page.Settings);
            loadout.Root.gameObject.SetActive(p == Page.Loadout);
            back.gameObject.SetActive(p != Page.Overview);
            FadeIn(p switch { Page.Branch => branch.Root, Page.Settings => settings.Root, Page.Loadout => loadout.Root, _ => overview.Root });
            logo.gameObject.SetActive(p == Page.Overview);
            title.text = p switch
            {
                Page.Branch => SkillDb.Info(branch.Current).Name,
                Page.Settings => "Settings",
                Page.Loadout => "Active skills",
                _ => "Skills"
            };
            if (p != Page.Settings && p != Page.Loadout) KeyRebinder.Cancel();
            Refresh();
        }

        /// <summary>Short fade and rise when a page opens.</summary>
        static void FadeIn(RectTransform page)
        {
            if (page == null) return;
            var g = page.GetComponent<CanvasGroup>() ?? page.gameObject.AddComponent<CanvasGroup>();
            Tweens.Run(g.GetInstanceID(), 0.2f, k =>
            {
                if (g == null) return;
                float e = Tweens.EaseOut(k);
                g.alpha = e;
                page.anchoredPosition = new Vector2(0, -12f * (1f - e));
            });
        }

        public void Refresh()
        {
            if (Root == null) return;
            spLabel.text = Skills.AvailablePoints.ToString();
            mpLabel.text = Skills.AvailableMasteryPoints.ToString();
            mpLabel.transform.parent.gameObject.SetActive(Skills.EarnedMasteryPoints > 0 || Skills.MasteriesTaken() > 0);
            switch (page)
            {
                case Page.Overview: overview?.Refresh(); break;
                case Page.Branch: branch?.Refresh(); break;
                case Page.Settings: settings?.Refresh(); break;
                case Page.Loadout: loadout?.Refresh(); break;
            }
        }

        bool dumped;

        /// <summary>Per-frame work while the app is open.</summary>
        public void Tick()
        {
            if (Root == null || !Root.gameObject.activeInHierarchy) return;
            Tweens.Update(Time.unscaledDeltaTime);
            branch?.Animate();
            overview?.Animate();
            if (KeyRebinder.IsListening)
            {
                if (page == Page.Loadout) loadout?.Refresh();
                else settings?.Refresh();
            }
            if (!dumped && Prefs.DebugLogging.Value)
            {
                dumped = true;
                var apps = Root.parent != null ? Root.parent.parent : null;
                if (apps != null) UiProbe.Dump(apps, "ui_appscanvas.txt", 7);
            }
        }

        void AskRespec()
        {
            if (Skills.SpentTotal() == 0 && Skills.MasteriesTaken() == 0)
            {
                dialog.Show("Nothing to reset", "You haven't learned any skills yet.", null, "OK", null);
                return;
            }
            int cost = Skills.RespecCost();
            int next = Skills.RespecCost(Skills.Data.Respecs + 1);
            float bank = Money.Online;
            string price = $"Resetting costs <b>${cost:N0}</b> from your bank account.";
            string after = next > cost ? $" The next reset will cost ${next:N0} (it stops rising at ${Skills.RespecCap:N0})." : $" Resets cost ${Skills.RespecCap:N0} from now on.";
            if (bank < cost)
            {
                dialog.Show("Reset skills", $"{price}\nYour bank balance is ${bank:N0}. Deposit cash at an ATM first.", null, "OK", null);
                return;
            }
            if (Money.InPursuit)
            {
                dialog.Show("Reset skills", "You can't reset your skills while the police are after you.", null, "OK", null);
                return;
            }
            dialog.Show("Reset all skills?",
                $"All skill and mastery points will be refunded.\n{price}{after}",
                "Cancel", "Pay & reset", () =>
                {
                    if (!Money.TrySpendOnline(cost, "Skill reset", "Hyland Skills")) { Sfx.Error(); return; }
                    Skills.Respec();
                    Sfx.Respec();
                });
        }
    }

    /// <summary>Sizes shared by every page, in the phone canvas' units (1201 x 655, like the native apps).</summary>
    public static class Metrics
    {
        public static float TopBar = 60f;
        public static float Pad = 15f;
        public static float ChipW = 124f;
        public static float FontTitle = 26f;
        public static float FontHeader = 24f;
        public static float FontBody = 20f;
        public static float FontSmall = 18f;
        public static float FontTiny = 16f;
        public static float Node = 70f;
        public static float MasteryNode = 76f;
    }
}
