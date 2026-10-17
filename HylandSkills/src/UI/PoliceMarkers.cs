using System.Collections.Generic;
using HylandSkills.Actives;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Police;
using Il2CppScheduleOne.UI.Phone;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using static HylandSkills.UI.UiKit;

namespace HylandSkills.UI
{
    /// <summary>
    /// Police Scanner: badges over nearby officers (clamped to the screen edge when off-screen). They live on their
    /// own canvas drawn just below the game's HUD, and hide while the phone or any game menu is open, so they never
    /// cover the game's interface.
    /// </summary>
    public static class PoliceMarkers
    {
        sealed class Marker
        {
            public RectTransform Rt;
            public Image Bg;
            public TextMeshProUGUI Dist;
        }

        static readonly List<Marker> pool = new List<Marker>();
        static RectTransform layer;
        static Canvas canvas;

        /// <summary>True while the phone or any game screen that frees the cursor is open.</summary>
        static bool GameUiOpen()
        {
            try
            {
                if (PlayerSingleton<PlayerCamera>.InstanceExists && PlayerSingleton<PlayerCamera>.Instance.ActiveUIElementCount > 0) return true;
                if (PlayerSingleton<Phone>.InstanceExists && PlayerSingleton<Phone>.Instance.IsOpen) return true;
            }
            catch { }
            return false;
        }

        static void EnsureLayer(Canvas hud)
        {
            if (layer != null) return;
            var go = new GameObject("HylandSkillsPoliceMarkers");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            var hudScaler = hud.GetComponent<CanvasScaler>();
            if (hudScaler != null)
            {
                scaler.uiScaleMode = hudScaler.uiScaleMode;
                scaler.referenceResolution = hudScaler.referenceResolution;
                scaler.screenMatchMode = hudScaler.screenMatchMode;
                scaler.matchWidthOrHeight = hudScaler.matchWidthOrHeight;
                scaler.scaleFactor = hudScaler.scaleFactor;
            }
            layer = go.GetComponent<RectTransform>();
        }

        public static void Tick(RectTransform hudRoot, Canvas hud)
        {
            bool active = ActiveManager.IsRunning("police_scanner") && hud != null && hud.enabled
                          && hudRoot.gameObject.activeInHierarchy && !GameUiOpen();
            if (!active)
            {
                if (layer != null) layer.gameObject.SetActive(false);
                return;
            }
            EnsureLayer(hud);
            canvas.sortingOrder = hud.sortingOrder - 1; // under the HUD and everything drawn above it
            layer.gameObject.SetActive(true);

            var cam = Camera.main;
            var me = Player.Local;
            var officers = PoliceOfficer.Officers;
            int used = 0;
            if (cam != null && me != null && officers != null)
            {
                float range = Data.SkillDb.Get("police_scanner").Value;
                var canvasRect = layer;
                foreach (var o in officers)
                {
                    if (o == null || !o.gameObject.activeInHierarchy || !o.IsConscious) continue;
                    Vector3 world = o.transform.position + Vector3.up * 2.1f;
                    float dist = Vector3.Distance(world, me.transform.position);
                    if (dist > range) continue;

                    Vector3 sp = cam.WorldToScreenPoint(world);
                    bool behind = sp.z < 0;
                    if (behind) { sp.x = Screen.width - sp.x; sp.y = 0; }
                    float m = 40f;
                    bool clamped = behind || sp.x < m || sp.x > Screen.width - m || sp.y < m || sp.y > Screen.height - m;
                    sp.x = Mathf.Clamp(sp.x, m, Screen.width - m);
                    sp.y = Mathf.Clamp(sp.y, m, Screen.height - m);

                    RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out var local);
                    var mk = Get(used++);
                    mk.Rt.anchoredPosition = local;
                    mk.Bg.color = clamped ? new Color(0.36f, 0.5f, 0.72f, 0.75f) : new Color(0.36f, 0.5f, 0.72f, 0.95f);
                    mk.Dist.text = $"{Mathf.RoundToInt(dist)}m";
                    float s = Mathf.Lerp(1f, 0.7f, dist / range);
                    mk.Rt.localScale = Vector3.one * s;
                }
            }
            for (int i = 0; i < pool.Count; i++) pool[i].Rt.gameObject.SetActive(i < used);
        }

        static Marker Get(int i)
        {
            while (pool.Count <= i)
            {
                var m = new Marker();
                m.Rt = Rect("Officer" + pool.Count, layer);
                m.Rt.anchorMin = m.Rt.anchorMax = new Vector2(0.5f, 0.5f);
                m.Rt.sizeDelta = new Vector2(34, 34);
                m.Bg = Image("Bg", m.Rt, Color.white, Circle);
                m.Bg.rectTransform.Stretch();
                var icon = Image("Icon", m.Rt, Color.white, Icons.Get("ui_police"));
                icon.preserveAspect = true;
                icon.rectTransform.Stretch(5, 5, 5, 5);
                m.Dist = Label("Dist", m.Rt, "", 16, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                m.Dist.enableWordWrapping = false;
                m.Dist.outlineWidth = 0.25f;
                m.Dist.outlineColor = new Color32(0, 0, 0, 220);
                m.Dist.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0, -12), new Vector2(72, 22));
                pool.Add(m);
            }
            pool[i].Rt.gameObject.SetActive(true);
            return pool[i];
        }

        public static void Clear()
        {
            pool.Clear();
            if (layer != null) Object.Destroy(layer.gameObject);
            layer = null;
            canvas = null;
        }
    }
}
