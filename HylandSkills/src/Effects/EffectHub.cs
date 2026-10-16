using System;
using System.Collections.Generic;
using UnityEngine;

namespace HylandSkills.Effects
{
    /// <summary>A timed window shown as a chip under the HUD timers.</summary>
    public struct StatusInfo
    {
        public string Label;
        public string TimeLeft;
        public string Icon;
        public Color Color;
    }

    /// <summary>One gameplay domain (player, growing, police…). Harmony patches live next to their module.</summary>
    public abstract class EffectModule
    {
        /// <summary>Re-applies persistent values (static fields, stacks) after any skill change.</summary>
        public virtual void Apply() { }
        public virtual void Update(float dt) { }
        public virtual void MinutePass(int now, int previous) { }
        /// <summary>Restores every value the module changed (leaving a save, respec).</summary>
        public virtual void Reset() { }
        public virtual void CollectStatus(List<StatusInfo> list) { }
    }

    public static class EffectHub
    {
        static readonly List<EffectModule> modules = new List<EffectModule>
        {
            new PlayerModule(),
            new GrowingModule(),
            new StationModule(),
            new EconomyModule(),
            new LawModule(),
            new DealsModule(),
            new CombatModule(),
            new GamblingModule(),
            new MobilityModule(),
        };

        static readonly List<StatusInfo> status = new List<StatusInfo>();

        public static void Register(EffectModule m) => modules.Add(m);

        public static void Recalculate()
        {
            if (!Mod.InGame) return;
            foreach (var m in modules)
            {
                try { m.Apply(); }
                catch (Exception e) { Mod.Log.Warning($"{m.GetType().Name}.Apply: {e.Message}"); }
            }
        }

        public static void Update(float dt)
        {
            foreach (var m in modules)
            {
                try { m.Update(dt); }
                catch (Exception e) { Mod.Debug($"{m.GetType().Name}.Update: {e.Message}"); }
            }
        }

        public static void MinutePass(int now, int previous)
        {
            foreach (var m in modules)
            {
                try { m.MinutePass(now, previous); }
                catch (Exception e) { Mod.Debug($"{m.GetType().Name}.MinutePass: {e.Message}"); }
            }
        }

        public static void ResetSession()
        {
            foreach (var m in modules)
            {
                try { m.Reset(); }
                catch (Exception e) { Mod.Debug($"{m.GetType().Name}.Reset: {e.Message}"); }
            }
        }

        public static List<StatusInfo> StatusEffects()
        {
            status.Clear();
            foreach (var m in modules)
            {
                try { m.CollectStatus(status); }
                catch { }
            }
            return status;
        }

        static readonly System.Random rng = new System.Random();
        public static bool Roll(float chance01) => chance01 > 0 && rng.NextDouble() < chance01;
        public static float Rand() => (float)rng.NextDouble();
    }
}
