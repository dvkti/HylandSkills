using System;
using System.Collections.Generic;
using System.Linq;
using HylandSkills.Data;
using HylandSkills.State;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.GameTime;
using UnityEngine;

namespace HylandSkills.Actives
{
    /// <summary>Behaviour of one active skill. Effects that need a running window read <see cref="ActiveManager.IsRunning"/>.</summary>
    public abstract class ActiveSkill
    {
        public abstract string Id { get; }
        public SkillDef Def => SkillDb.Get(Id);

        /// <summary>Return false and a short reason ("No plant in sight") to block activation.</summary>
        public virtual bool CanActivate(out string reason) { reason = null; return true; }
        /// <summary>Return false if activation failed after all; the cooldown is then not consumed.</summary>
        public abstract bool Activate();
        public virtual void OnEnd() { }
        /// <summary>Effect length for this activation (combos can change it). Same clock as the skill.</summary>
        public virtual float Duration => Def.Duration;
        /// <summary>Cooldown for this activation (combos can change it). Same clock as the skill.</summary>
        public virtual float Cooldown => Def.Cooldown;
        public virtual void Tick() { }
    }

    public static class ActiveManager
    {
        static readonly Dictionary<string, ActiveSkill> skills = new Dictionary<string, ActiveSkill>();
        /// <summary>Real-time effects: seconds remaining.</summary>
        static readonly Dictionary<string, float> realRunning = new Dictionary<string, float>();
        static readonly Dictionary<string, float> totalDuration = new Dictionary<string, float>();
        static readonly Dictionary<string, float> totalCooldown = new Dictionary<string, float>();

        /// <summary>Adds time to a running real-time effect (e.g. Free Runner extending Sprint Burst).</summary>
        public static void ExtendRealWindow(string id, float seconds)
        {
            if (!realRunning.TryGetValue(id, out var left)) return;
            realRunning[id] = left + seconds;
            totalDuration[id] = (totalDuration.TryGetValue(id, out var t) ? t : left) + seconds;
        }

        public static event Action<string, string> ActivationFailed; // (skill id, reason)
        public static event Action<string> Activated;

        public static void Register(ActiveSkill s) => skills[s.Id] = s;
        public static ActiveSkill Get(string id) => skills.TryGetValue(id, out var s) ? s : null;

        public static int GameNow
        {
            get
            {
                if (!NetworkSingleton<TimeManager>.InstanceExists) return 0;
                return NetworkSingleton<TimeManager>.Instance.GetTotalMinSum();
            }
        }

        static SkillSaveData D => Skills.Data;

        // ---------------- loadout ----------------
        public static string Slot(int i) => i >= 0 && i < D.Loadout.Length ? D.Loadout[i] : null;

        public static void Equip(int slot, string id)
        {
            if (slot < 0 || slot >= D.Loadout.Length) return;
            for (int i = 0; i < D.Loadout.Length; i++) if (D.Loadout[i] == id) D.Loadout[i] = null;
            D.Loadout[slot] = id;
            Skills.NotifyChanged();
        }

        public static void Unequip(int slot)
        {
            if (slot < 0 || slot >= D.Loadout.Length) return;
            D.Loadout[slot] = null;
            Skills.NotifyChanged();
        }

        public static int SlotOf(string id) => Array.IndexOf(D.Loadout, id);

        public static void AutoEquip(string id)
        {
            if (SlotOf(id) >= 0) return;
            for (int i = 0; i < D.Loadout.Length; i++)
            {
                if (string.IsNullOrEmpty(D.Loadout[i])) { D.Loadout[i] = id; return; }
            }
        }

        // ---------------- timing ----------------
        /// <summary>Seconds (real) or game minutes left on cooldown, in the skill's own clock.</summary>
        public static float CooldownLeft(string id)
        {
            var d = SkillDb.Get(id);
            if (d == null) return 0;
            if (d.Clock == CooldownClock.GameMinutes)
                return D.GameCooldownUntil.TryGetValue(id, out var until) ? Math.Max(0, until - GameNow) : 0;
            return D.RealCooldownLeft.TryGetValue(id, out var left) ? Math.Max(0, left) : 0;
        }

        public static float CooldownFraction(string id)
        {
            var d = SkillDb.Get(id);
            if (d == null) return 0;
            float total = totalCooldown.TryGetValue(id, out var t) ? t : d.Cooldown;
            if (total <= 0) return 0;
            return Mathf.Clamp01(CooldownLeft(id) / total);
        }

        public static bool IsRunning(string id)
        {
            if (realRunning.ContainsKey(id)) return true;
            return D.GameEffectUntil.TryGetValue(id, out var until) && until > GameNow;
        }

        /// <summary>Remaining effect time in the skill's own clock (seconds or game minutes).</summary>
        public static float RunningLeft(string id)
        {
            if (realRunning.TryGetValue(id, out var s)) return s;
            return D.GameEffectUntil.TryGetValue(id, out var until) ? Math.Max(0, until - GameNow) : 0;
        }

        public static float RunningFraction(string id)
        {
            var d = SkillDb.Get(id);
            if (d == null) return 0;
            float total = totalDuration.TryGetValue(id, out var t) ? t : d.Duration;
            if (total <= 0) return 0;
            return Mathf.Clamp01(RunningLeft(id) / total);
        }

        /// <summary>Starts or extends a timed window that is not tied to the skill's own duration (e.g. Overclock burnout).</summary>
        public static void StartGameWindow(string key, int minutes) => D.GameEffectUntil[key] = GameNow + minutes;
        public static bool GameWindowActive(string key) => D.GameEffectUntil.TryGetValue(key, out var u) && u > GameNow;
        public static int GameWindowLeft(string key) => D.GameEffectUntil.TryGetValue(key, out var u) ? Math.Max(0, u - GameNow) : 0;

        // ---------------- activation ----------------
        public static void TryActivateSlot(int slot)
        {
            var id = Slot(slot);
            if (string.IsNullOrEmpty(id)) return;
            TryActivate(id);
        }

        public static bool TryActivate(string id)
        {
            var d = SkillDb.Get(id);
            var s = Get(id);
            if (d == null || s == null || !Skills.Has(id)) return false;
            if (IsRunning(id)) { ActivationFailed?.Invoke(id, "Already active"); return false; }
            if (CooldownLeft(id) > 0) { ActivationFailed?.Invoke(id, "Not ready"); return false; }
            if (!s.CanActivate(out var reason)) { ActivationFailed?.Invoke(id, reason ?? "Can't use that now"); return false; }
            bool ok;
            try { ok = s.Activate(); }
            catch (Exception e) { Mod.Log.Error($"Active {id} failed: {e}"); ok = false; }
            if (!ok) { ActivationFailed?.Invoke(id, "Can't use that now"); return false; }

            float dur = s.Duration, cd = s.Cooldown;
            totalDuration[id] = dur;
            totalCooldown[id] = cd;
            if (dur > 0)
            {
                if (d.Clock == CooldownClock.RealSeconds) realRunning[id] = dur;
                else D.GameEffectUntil[id] = GameNow + (int)dur;
            }
            if (d.Clock == CooldownClock.GameMinutes) D.GameCooldownUntil[id] = GameNow + (int)cd;
            else D.RealCooldownLeft[id] = cd;

            Effects.EffectHub.Recalculate();
            Activated?.Invoke(id);
            return true;
        }

        /// <summary>Called every frame while a save is loaded.</summary>
        public static void Update(float dt)
        {
            if (realRunning.Count > 0)
            {
                foreach (var id in realRunning.Keys.ToList())
                {
                    float left = realRunning[id] - dt;
                    if (left <= 0)
                    {
                        realRunning.Remove(id);
                        SafeEnd(id);
                    }
                    else realRunning[id] = left;
                }
            }
            if (D.RealCooldownLeft.Count > 0)
            {
                foreach (var id in D.RealCooldownLeft.Keys.ToList())
                {
                    float left = D.RealCooldownLeft[id] - dt;
                    if (left <= 0) D.RealCooldownLeft.Remove(id);
                    else D.RealCooldownLeft[id] = left;
                }
            }
            foreach (var kv in skills)
            {
                if (IsRunning(kv.Key))
                {
                    try { kv.Value.Tick(); } catch (Exception e) { Mod.Log.Warning($"Tick {kv.Key}: {e.Message}"); }
                }
            }
        }

        static readonly HashSet<string> wasRunningGame = new HashSet<string>();

        /// <summary>Called once per game minute; detects the end of game-clock windows.</summary>
        public static void MinutePass()
        {
            int now = GameNow;
            foreach (var kv in D.GameEffectUntil.ToList())
            {
                bool running = kv.Value > now;
                if (running) wasRunningGame.Add(kv.Key);
                else if (wasRunningGame.Remove(kv.Key))
                {
                    D.GameEffectUntil.Remove(kv.Key);
                    SafeEnd(kv.Key);
                }
                else if (!running) D.GameEffectUntil.Remove(kv.Key);
            }
        }

        static void SafeEnd(string id)
        {
            var s = Get(id);
            try { s?.OnEnd(); } catch (Exception e) { Mod.Log.Warning($"End {id}: {e.Message}"); }
            Effects.EffectHub.Recalculate();
        }

        public static void CancelAll()
        {
            foreach (var id in realRunning.Keys.ToList()) SafeEnd(id);
            realRunning.Clear();
            wasRunningGame.Clear();
        }

        public static void ResetSession()
        {
            realRunning.Clear();
            wasRunningGame.Clear();
        }

        /// <summary>Formats a remaining time for the HUD, in the skill's own clock.</summary>
        public static string FormatTime(float value, CooldownClock clock)
        {
            if (clock == CooldownClock.RealSeconds)
            {
                int s = Mathf.CeilToInt(value);
                return s >= 60 ? $"{s / 60}:{s % 60:00}" : $"{s}s";
            }
            // Game minutes: 45m, 3h 20m, 23h 05m
            int m = Mathf.CeilToInt(value);
            if (m >= 60) return m % 60 == 0 ? $"{m / 60}h" : $"{m / 60}h {m % 60:00}m";
            return $"{m}m";
        }
    }
}
