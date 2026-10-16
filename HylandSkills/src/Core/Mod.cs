using System;
using HylandSkills.Actives;
using HylandSkills.Core;
using HylandSkills.Effects;
using HylandSkills.State;
using HylandSkills.UI;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI.Phone;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(HylandSkills.Mod), "Hyland Skills", "1.1.0", "GetToWorkTylerPls")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace HylandSkills
{
    public class Mod : MelonMod
    {
        public static MelonLogger.Instance Log { get; private set; }
        public static Mod Instance { get; private set; }

        public static bool InGame { get; private set; }
        public static bool WorldReady => worldReady;
        static bool worldReady;
        static float pointPoll;
        static int lastGameMinute = -1;

        public override void OnInitializeMelon()
        {
            Instance = this;
            Log = LoggerInstance;
            Prefs.Init();
            Icons.Load();
            ActiveRegistry.RegisterAll();
            Progression.PointsEarned += OnPointsEarned;
            Log.Msg($"Hyland Skills ready: {Data.SkillDb.All.Count} skills, {Data.SkillDb.Synergies.Count} synergies.");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (sceneName == "Menu")
            {
                LeaveGame();
                Skills.ResetForNewSession();
            }
            else if (sceneName == "Main")
            {
                InGame = true;
                worldReady = false;
                lastGameMinute = -1;
            }
        }

        static void LeaveGame()
        {
            InGame = false;
            worldReady = false;
            ActiveManager.ResetSession();
            Progression.ResetSession();
            EffectHub.ResetSession();
            Hud.Destroy();
        }

        public override void OnUpdate()
        {
            try { DebugBridge.Update(); } catch (Exception e) { Log.Error("Bridge error: " + e); }
            if (!InGame) return;
            try
            {
                if (!worldReady)
                {
                    if (!IsWorldReady()) return;
                    worldReady = true;
                    if (Prefs.DebugLogging.Value) StaticAudit.Run();
                    Debug("init: state");
                    Skills.EnsureLoaded();
                    Debug("init: progression");
                    Progression.Refresh(notify: false);
                    Debug("init: hud");
                    Hud.Create();
                    Debug("init: effects");
                    EffectHub.Recalculate();
                    Log.Msg($"World ready. Tier-ups: {Progression.TierUps}, points: {Skills.AvailablePoints}/{Skills.EarnedPoints}.");
                }

                float dt = Time.deltaTime;
                pointPoll -= dt;
                if (pointPoll <= 0)
                {
                    pointPoll = 1f;
                    Progression.Refresh(notify: true);
                }

                int minute = ActiveManager.GameNow;
                if (minute != lastGameMinute)
                {
                    if (lastGameMinute >= 0) ActiveManager.MinutePass();
                    EffectHub.MinutePass(minute, lastGameMinute);
                    lastGameMinute = minute;
                }

                ActiveManager.Update(dt);
                EffectHub.Update(dt);
                KeyRebinder.Update();
                HandleInput();
                Hud.Tick();
                SkillsApp.Instance?.Tick();
            }
            catch (Exception e)
            {
                Log.Error("Update error: " + e);
            }
        }

        static bool IsWorldReady()
        {
            if (Player.Local == null) return false;
            if (!Singleton<LoadManager>.InstanceExists) return false;
            var lm = Singleton<LoadManager>.Instance;
            return lm.IsGameLoaded && !lm.IsLoading;
        }

        static void HandleInput()
        {
            if (Prefs.DebugLogging.Value && Input.GetKeyDown(KeyCode.F10))
            {
                Progression.DebugBonus += 10;
                Skills.NotifyChanged();
                Log.Msg($"[debug] +10 test points (bonus {Progression.DebugBonus}).");
            }
            if (Prefs.DebugLogging.Value && Input.GetKeyDown(KeyCode.F9))
            {
                bool ok = S1API.Internal.Abstraction.Saveable.RequestGameSave();
                Log.Msg($"[debug] save requested: {ok}");
            }
            if (KeyRebinder.IsListening) return;
            if (GameInput.IsTyping) return;
            if (PlayerSingleton<Phone>.InstanceExists && PlayerSingleton<Phone>.Instance.IsOpen) return;
            if (Player.Local == null || !Player.Local.Health.IsAlive || Player.Local.IsArrested) return;

            for (int i = 0; i < Prefs.SlotKeys.Length; i++)
            {
                if (Input.GetKeyDown(Prefs.SlotKeys[i].Value)) ActiveManager.TryActivateSlot(i);
            }
        }

        static void OnPointsEarned(int points, int mastery)
        {
            if (!Prefs.PointNotifications.Value || points <= 0) return;
            string title = mastery > 0 ? "Mastery point earned" : (points == 1 ? "Skill point earned" : $"{points} skill points earned");
            string sub = mastery > 0 ? "Choose a Major Mastery in the Skills app" : "Spend it in the Skills app";
            Notify(title, sub, Icons.Get("app_icon"));
        }

        public static void Notify(string title, string subtitle, Sprite icon)
        {
            try
            {
                if (Singleton<Il2CppScheduleOne.UI.NotificationsManager>.InstanceExists)
                    Singleton<Il2CppScheduleOne.UI.NotificationsManager>.Instance.SendNotification(title, subtitle, icon, 5f, true);
            }
            catch (Exception e) { Log.Warning("Notify failed: " + e.Message); }
        }

        /// <summary>Guards [trace] messages so their strings are only built when debug logging is on.</summary>
        public static bool Tracing => Prefs.DebugLogging != null && Prefs.DebugLogging.Value;

        public static void Debug(string msg)
        {
            if (Prefs.DebugLogging != null && Prefs.DebugLogging.Value) Log.Msg("[debug] " + msg);
        }
    }
}
