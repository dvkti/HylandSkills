using System.Collections.Generic;
using HylandSkills.Actives;
using HylandSkills.Core;
using HylandSkills.Data;
using HylandSkills.State;
using Il2CppScheduleOne.DevUtilities;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using static HylandSkills.UI.UiKit;

namespace HylandSkills.UI
{
    /// <summary>
    /// Ability timers inside the game's own HUD canvas (shares its scaling, hides with it).
    /// Each equipped active is a card that always says what it is doing: READY, ACTIVE + time left, or COOLDOWN + time left.
    /// </summary>
    public static class Hud
    {
        const float CardW = 272f;
        const float CardH = 58f;
        const float Gap = 8f;
        const float Margin = 22f;

        static RectTransform root;
        static CanvasGroup group;
        static RectTransform stack;
        static readonly Card[] cards = new Card[ActiveLoadout.Slots];
        static readonly List<Card> statusCards = new List<Card>();
        static TextMeshProUGUI toast;
        /// <summary>Copy of the toast on its own canvas, shown while the game hides its HUD (casino tables, etc.).</summary>
        static TextMeshProUGUI overlayToast;
        static GameObject overlay;
        static float toastTime;
        static readonly Dictionary<string, float> readyFlash = new Dictionary<string, float>();
        static readonly HashSet<string> wasCooling = new HashSet<string>();

        static readonly Color CardColor = new Color(0.08f, 0.08f, 0.09f, 0.74f);
        static readonly Color ReadyColor = UiKit.Hex("7BD957");
        static readonly Color CooldownColor = UiKit.Hex("9A9A9A");

        sealed class Card
        {
            public RectTransform Rt;
            public Image Bg, Edge, IconBox, Icon, Flash, BarTrack, BarFill, KeyBg;
            public TextMeshProUGUI Name, Status, Key;
        }

        public static void Create()
        {
            Destroy();
            if (!Singleton<Il2CppScheduleOne.UI.HUD>.InstanceExists) return;
            var hud = Singleton<Il2CppScheduleOne.UI.HUD>.Instance;
            if (hud.canvas == null) return;

            root = Rect("HylandSkillsHUD", hud.canvas.transform);
            root.Stretch();
            root.SetAsLastSibling();
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            stack = Rect("Stack", root);
            for (int i = 0; i < cards.Length; i++) cards[i] = MakeCard(stack, "Slot" + i, withKey: true);
            toast = Label("Toast", root, "", 21, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            toast.outlineWidth = 0.2f;
            toast.outlineColor = new Color32(0, 0, 0, 210);
            toast.enableWordWrapping = false;
            toast.rectTransform.sizeDelta = new Vector2(660, 32);
            CreateOverlayToast(hud.canvas);

            ActiveManager.ActivationFailed += OnActivationFailed;
            ActiveManager.Activated += OnActivated;
            Relayout();
        }

        public static void Destroy()
        {
            ActiveManager.ActivationFailed -= OnActivationFailed;
            ActiveManager.Activated -= OnActivated;
            if (root != null) Object.Destroy(root.gameObject);
            root = null;
            if (overlay != null) Object.Destroy(overlay);
            overlay = null;
            overlayToast = null;
            PoliceMarkers.Clear();
            statusCards.Clear();
            readyFlash.Clear();
            wasCooling.Clear();
        }

        static void CreateOverlayToast(Canvas hud)
        {
            overlay = new GameObject("HylandSkillsToast");
            var canvas = overlay.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = overlay.AddComponent<CanvasScaler>();
            var hudScaler = hud.GetComponent<CanvasScaler>();
            if (hudScaler != null)
            {
                scaler.uiScaleMode = hudScaler.uiScaleMode;
                scaler.referenceResolution = hudScaler.referenceResolution;
                scaler.screenMatchMode = hudScaler.screenMatchMode;
                scaler.matchWidthOrHeight = hudScaler.matchWidthOrHeight;
                scaler.scaleFactor = hudScaler.scaleFactor;
            }
            var rt = overlay.GetComponent<RectTransform>();
            overlayToast = Label("Toast", rt, "", 24, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            overlayToast.outlineWidth = 0.2f;
            overlayToast.outlineColor = new Color32(0, 0, 0, 220);
            overlayToast.enableWordWrapping = false;
            overlayToast.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(900, 36));
            overlay.SetActive(false);
        }

        static Card MakeCard(RectTransform parent, string name, bool withKey)
        {
            var c = new Card();
            c.Bg = Image(name, parent, CardColor, Round8);
            c.Rt = c.Bg.rectTransform;
            c.Rt.sizeDelta = new Vector2(CardW, CardH);

            c.IconBox = Image("IconBox", c.Rt, Color.white, Round8);
            c.IconBox.rectTransform.Anchor(new Vector2(0, 0), new Vector2(0, 1), new Vector2(4, 4), new Vector2(4 + CardH - 8, -4));
            c.Icon = Image("Icon", c.IconBox.transform, Color.white, null);
            c.Icon.preserveAspect = true;
            c.Icon.rectTransform.Stretch(9, 9, 9, 9);

            c.Edge = Image("Edge", c.Rt, Color.white, null);
            c.Edge.rectTransform.Anchor(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 6), new Vector2(3, -6));

            c.Name = Label("Name", c.Rt, "", 17, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            c.Name.enableWordWrapping = false;
            c.Name.overflowMode = TextOverflowModes.Ellipsis;
            c.Name.rectTransform.Anchor(new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(CardH + 6, -2), new Vector2(withKey ? -46 : -8, -6));
            c.Status = Label("Status", c.Rt, "", 14, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            c.Status.enableWordWrapping = false;
            c.Status.characterSpacing = 1;
            c.Status.rectTransform.Anchor(new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(CardH + 6, 6), new Vector2(-8, 0));

            c.BarTrack = Image("Bar", c.Rt, new Color(1, 1, 1, 0.08f), null);
            c.BarTrack.rectTransform.Anchor(new Vector2(0, 0), new Vector2(1, 0), new Vector2(CardH + 6, 5), new Vector2(-8, 8));
            c.BarFill = Image("Fill", c.BarTrack.transform, Color.white, null);
            c.BarFill.rectTransform.Anchor(Vector2.zero, new Vector2(1, 1));

            if (withKey)
            {
                c.KeyBg = Image("Key", c.Rt, new Color(1, 1, 1, 0.12f), Round4);
                c.KeyBg.rectTransform.Place(new Vector2(1, 1), new Vector2(-8, -7), new Vector2(32, 24), new Vector2(1, 1));
                c.Key = Label("K", c.KeyBg.transform, "", 15, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                c.Key.enableWordWrapping = false;
                c.Key.rectTransform.Stretch();
            }

            c.Flash = Image("Flash", c.Rt, new Color(1, 1, 1, 0), Round8);
            c.Flash.rectTransform.Stretch();
            return c;
        }

        static (Vector2 anchor, bool horizontal) Placement(HudAnchor a) => a switch
        {
            HudAnchor.TopLeft => (new Vector2(0, 1), false),
            HudAnchor.TopCenter => (new Vector2(0.5f, 1), true),
            HudAnchor.TopRight => (new Vector2(1, 1), false),
            HudAnchor.MiddleLeft => (new Vector2(0, 0.5f), false),
            HudAnchor.MiddleCenter => (new Vector2(0.5f, 0.5f), true),
            HudAnchor.MiddleRight => (new Vector2(1, 0.5f), false),
            HudAnchor.BottomLeft => (new Vector2(0, 0), false),
            HudAnchor.BottomCenter => (new Vector2(0.5f, 0), true),
            _ => (new Vector2(1, 0), false),
        };

        /// <summary>Extra clearance so the default corners don't sit on top of native HUD elements.</summary>
        static Vector2 Clearance(HudAnchor a) => a switch
        {
            HudAnchor.TopRight => new Vector2(0, 40),
            HudAnchor.TopCenter => new Vector2(0, 60),
            HudAnchor.BottomCenter => new Vector2(0, 120),
            HudAnchor.BottomRight => new Vector2(0, 30),
            HudAnchor.BottomLeft => new Vector2(0, 30),
            HudAnchor.TopLeft => new Vector2(0, 40),
            _ => Vector2.zero
        };

        public static void Relayout()
        {
            if (root == null) return;
            var a = Prefs.Anchor.Value;
            var (anchor, horizontal) = Placement(a);
            float scale = Mathf.Clamp(Prefs.HudScale.Value, 0.6f, 1.6f);
            var clear = Clearance(a);
            Vector2 inward = new Vector2(anchor.x == 0 ? 1 : anchor.x == 1 ? -1 : 0, anchor.y == 0 ? 1 : anchor.y == 1 ? -1 : 0);

            stack.anchorMin = stack.anchorMax = anchor;
            stack.pivot = anchor;
            stack.localScale = Vector3.one * scale;
            stack.anchoredPosition = new Vector2(inward.x * (Margin + clear.x), inward.y * (Margin + clear.y));

            var visible = new List<Card>();
            for (int i = 0; i < cards.Length; i++)
            {
                bool vis = ActiveManager.Slot(i) != null || Prefs.ShowEmptySlots.Value;
                cards[i].Rt.gameObject.SetActive(vis);
                if (vis) visible.Add(cards[i]);
            }
            foreach (var s in statusCards) if (s.Rt.gameObject.activeSelf) visible.Add(s);

            int n = Mathf.Max(1, visible.Count);
            stack.sizeDelta = horizontal ? new Vector2(n * (CardW + Gap) - Gap, CardH) : new Vector2(CardW, n * (CardH + Gap) - Gap);
            // stack from the screen edge inward: top anchors grow down, bottom anchors grow up
            bool growUp = anchor.y == 0;
            for (int i = 0; i < visible.Count; i++)
            {
                var rt = visible[i].Rt;
                rt.anchorMin = rt.anchorMax = horizontal ? new Vector2(0, 0.5f) : new Vector2(0.5f, growUp ? 0 : 1);
                rt.pivot = horizontal ? new Vector2(0, 0.5f) : new Vector2(0.5f, growUp ? 0 : 1);
                rt.anchoredPosition = horizontal ? new Vector2(i * (CardW + Gap), 0) : new Vector2(0, (growUp ? 1 : -1) * i * (CardH + Gap));
            }

            var trt = toast.rectTransform;
            trt.anchorMin = trt.anchorMax = anchor;
            trt.pivot = anchor;
            trt.localScale = Vector3.one * scale;
            float extent = (horizontal ? CardH : stack.sizeDelta.y) * scale + 10;
            trt.anchoredPosition = stack.anchoredPosition + new Vector2(0, (anchor.y == 0 ? 1 : -1) * extent);
            toast.alignment = anchor.x < 0.25f ? TextAlignmentOptions.Left : anchor.x > 0.75f ? TextAlignmentOptions.Right : TextAlignmentOptions.Center;
            group.alpha = Mathf.Clamp(Prefs.HudOpacity.Value, 0.3f, 1f);
        }

        static void OnActivated(string id)
        {
            readyFlash[id] = 1f;
            Sfx.Activate();
        }

        static void OnActivationFailed(string id, string reason)
        {
            var d = SkillDb.Get(id);
            ShowToast($"{d?.Name}: {reason}", Theme.Warn);
            Sfx.Error();
        }

        public static void ShowToast(string text, Color color)
        {
            if (toast == null) return;
            toast.text = text;
            toast.color = color;
            if (overlayToast != null) { overlayToast.text = text; overlayToast.color = color; }
            toastTime = 2.6f;
        }

        public static void Tick()
        {
            if (root == null)
            {
                if (Singleton<Il2CppScheduleOne.UI.HUD>.InstanceExists) Create();
                return;
            }
            float dt = Time.unscaledDeltaTime;
            bool dirty = false;

            for (int i = 0; i < cards.Length; i++)
            {
                var c = cards[i];
                var id = ActiveManager.Slot(i);
                bool vis = id != null || Prefs.ShowEmptySlots.Value;
                if (c.Rt.gameObject.activeSelf != vis) dirty = true;
                if (!vis) continue;
                c.KeyBg.gameObject.SetActive(Prefs.ShowKeyHints.Value);
                c.Key.text = Prefs.KeyLabel(Prefs.SlotKeys[i].Value);
                c.KeyBg.rectTransform.sizeDelta = new Vector2(Mathf.Max(26, c.Key.preferredWidth + 12), 20);
                if (id == null) { DrawEmpty(c); continue; }
                DrawActive(c, SkillDb.Get(id), dt);
            }

            dirty |= UpdateStatusCards();

            if (toastTime > 0)
            {
                toastTime -= dt;
                toast.alpha = Mathf.Clamp01(toastTime / 0.4f);
                if (overlayToast != null) overlayToast.alpha = toast.alpha;
            }
            else if (toast.text.Length > 0) toast.text = "";
            if (overlay != null)
            {
                var hudCanvas = Singleton<Il2CppScheduleOne.UI.HUD>.InstanceExists ? Singleton<Il2CppScheduleOne.UI.HUD>.Instance.canvas : null;
                bool hudHidden = hudCanvas == null || !hudCanvas.enabled;
                bool show = hudHidden && toastTime > 0;
                if (overlay.activeSelf != show) overlay.SetActive(show);
            }

            PoliceMarkers.Tick(root, Singleton<Il2CppScheduleOne.UI.HUD>.InstanceExists ? Singleton<Il2CppScheduleOne.UI.HUD>.Instance.canvas : null);
            if (dirty) Relayout();
        }

        static void DrawEmpty(Card c)
        {
            c.Bg.color = CardColor.WithAlpha(0.4f);
            c.IconBox.color = new Color(1, 1, 1, 0.06f);
            c.Icon.enabled = false;
            c.Edge.color = new Color(1, 1, 1, 0);
            c.Name.text = "Empty slot";
            c.Name.color = Theme.TextFaint;
            c.Status.text = "";
            c.BarTrack.gameObject.SetActive(false);
        }

        static void DrawActive(Card c, SkillDef d, float dt)
        {
            var color = SkillDb.Info(d.Branch).Color;
            bool running = ActiveManager.IsRunning(d.Id);
            float cd = ActiveManager.CooldownLeft(d.Id);
            bool cooling = cd > 0 && !running;

            c.Name.text = d.Name;
            c.Name.color = Color.white;
            c.Icon.sprite = Icons.Get(d.Icon);
            c.Icon.enabled = c.Icon.sprite != null;
            c.Bg.color = CardColor;
            c.BarTrack.gameObject.SetActive(true);

            if (running)
            {
                // ACTIVE: coloured card, draining bar, time left
                c.IconBox.color = color;
                c.Icon.color = Color.white;
                c.Edge.color = color;
                string left = d.Duration > 0 ? ActiveManager.FormatTime(ActiveManager.RunningLeft(d.Id), d.Clock) : "";
                c.Status.text = d.Duration > 0 ? $"ACTIVE  <color=#FFFFFF>{left} left</color>" : "ACTIVE";
                c.Status.color = color;
                c.BarFill.color = color;
                c.BarFill.rectTransform.anchorMax = new Vector2(d.Duration > 0 ? ActiveManager.RunningFraction(d.Id) : 1f, 1);
            }
            else if (cooling)
            {
                // COOLDOWN: dim icon, grey bar filling up towards ready
                c.IconBox.color = new Color(1, 1, 1, 0.07f);
                c.Icon.color = color.WithAlpha(0.45f);
                c.Edge.color = new Color(1, 1, 1, 0);
                c.Status.text = $"COOLDOWN  <color=#DDDDDD>{ActiveManager.FormatTime(cd, d.Clock)}</color>";
                c.Status.color = CooldownColor;
                c.BarFill.color = CooldownColor;
                c.BarFill.rectTransform.anchorMax = new Vector2(1f - ActiveManager.CooldownFraction(d.Id), 1);
                wasCooling.Add(d.Id);
            }
            else
            {
                // READY
                c.IconBox.color = Color.Lerp(new Color(0.16f, 0.16f, 0.17f), color, 0.35f);
                c.Icon.color = Color.white;
                c.Edge.color = new Color(1, 1, 1, 0);
                c.Status.text = "READY";
                c.Status.color = ReadyColor;
                c.BarFill.color = ReadyColor.WithAlpha(0.5f);
                c.BarFill.rectTransform.anchorMax = Vector2.one;
                if (wasCooling.Remove(d.Id)) readyFlash[d.Id] = 1f;
            }

            if (readyFlash.TryGetValue(d.Id, out var f))
            {
                f -= dt * 1.5f;
                if (f <= 0) { readyFlash.Remove(d.Id); c.Flash.color = new Color(1, 1, 1, 0); }
                else { readyFlash[d.Id] = f; c.Flash.color = color.WithAlpha(f * 0.45f); }
            }
            else c.Flash.color = new Color(1, 1, 1, 0);
        }

        /// <summary>Timed windows that aren't an active's own duration (e.g. Overclock burnout) as extra cards.</summary>
        static bool UpdateStatusCards()
        {
            var list = Effects.EffectHub.StatusEffects();
            bool dirty = false;
            while (statusCards.Count < list.Count) { statusCards.Add(MakeCard(stack, "Status" + statusCards.Count, withKey: false)); dirty = true; }
            for (int i = 0; i < statusCards.Count; i++)
            {
                var c = statusCards[i];
                bool vis = i < list.Count;
                if (c.Rt.gameObject.activeSelf != vis) { c.Rt.gameObject.SetActive(vis); dirty = true; }
                if (!vis) continue;
                var s = list[i];
                c.Bg.color = CardColor;
                c.IconBox.color = s.Color.WithAlpha(0.8f);
                c.Icon.sprite = Icons.Get(s.Icon);
                c.Icon.enabled = c.Icon.sprite != null;
                c.Icon.color = Color.white;
                c.Edge.color = s.Color;
                c.Name.text = s.Label;
                c.Name.color = Color.white;
                c.Status.text = $"EFFECT  <color=#DDDDDD>{s.TimeLeft} left</color>";
                c.Status.color = s.Color;
                c.BarTrack.gameObject.SetActive(false);
                c.Flash.color = new Color(1, 1, 1, 0);
            }
            return dirty;
        }
    }
}
