using System;
using System.Collections.Generic;
using HylandSkills.Actives;
using HylandSkills.State;
using Il2CppFishNet.Object;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Law;
using Il2CppScheduleOne.PlayerScripts;
using UnityEngine;

namespace HylandSkills.Effects
{
    /// <summary>Small game-state helpers shared by every effect module.</summary>
    public static class G
    {
        public static bool IsCurfew
        {
            get
            {
                try { return NetworkSingleton<CurfewManager>.InstanceExists && NetworkSingleton<CurfewManager>.Instance.IsCurrentlyActive; }
                catch { return false; }
            }
        }

        /// <summary>Daytime = 06:00-18:00 game time.</summary>
        public static bool IsDay
        {
            get
            {
                if (!NetworkSingleton<TimeManager>.InstanceExists) return true;
                int t = NetworkSingleton<TimeManager>.Instance.CurrentTime;
                return t >= 600 && t < 1800;
            }
        }

        public static bool IsLocalPlayer(NetworkObject obj)
        {
            if (obj == null || Player.Local == null) return false;
            return Player.Local.NetworkObject != null && obj.ObjectId == Player.Local.NetworkObject.ObjectId;
        }

        public static EQuality Shift(EQuality q, int amount) => ItemQuality.ShiftQuality(q, amount);

        /// <summary>One quality tier up with probability <paramref name="chance"/>, plus Farm to Lab and Lab Focus.</summary>
        public static EQuality RollStationQuality(EQuality q, float chance)
        {
            // Mad Scientist combo: Overclock + Lab Focus running together = guaranteed tier up.
            if (Skills.Combo("mad_scientist") && ActiveManager.IsRunning("overclock") && ActiveManager.IsRunning("lab_focus"))
                return Shift(q, 1);
            float c = chance;
            if (Skills.SynergyActive("farm_to_lab")) c += 0.10f;
            if (ActiveManager.IsRunning("lab_focus")) c += Data.SkillDb.Get("lab_focus").Value / 100f;
            if (EffectHub.Roll(Mathf.Clamp01(c))) q = Shift(q, 1);
            // Volatile Chemistry drawback; Controlled Burn turns the drop into another chance to go up.
            if (Skills.Has("volatile_chemistry") && EffectHub.Roll(Skills.Pct2("volatile_chemistry")))
            {
                if (Skills.Combo("controlled_burn")) { if (EffectHub.Roll(Mathf.Clamp01(Skills.Pct("precise_measures")))) q = Shift(q, 1); }
                else q = Shift(q, -1);
            }
            return q;
        }

        public static Il2CppScheduleOne.Property.Property PlayerProperty =>
            Player.Local != null ? Player.Local.CurrentProperty : null;
    }

    /// <summary>
    /// Converts a fractional speed multiplier into whole game minutes per object,
    /// so "+6% speed" on a 1-minute tick still adds up correctly over time.
    /// </summary>
    public sealed class MinuteAccumulator
    {
        readonly Dictionary<IntPtr, float> carry = new Dictionary<IntPtr, float>();

        public int Scale(IntPtr key, int minutes, float multiplier)
        {
            if (Mathf.Approximately(multiplier, 1f) || minutes <= 0) return minutes;
            carry.TryGetValue(key, out var c);
            float total = minutes * multiplier + c;
            int whole = Mathf.FloorToInt(total);
            carry[key] = total - whole;
            return Math.Max(0, whole);
        }

        public void Clear() => carry.Clear();
    }

    /// <summary>
    /// Base value of a game field the mod scales, so multipliers never compound. When the game itself changes the
    /// field after the mod wrote it (a save finishing loading, a hire, an upgrade), the new value becomes the base
    /// instead of being overwritten with a stale one.
    /// </summary>
    public sealed class TrackedBase
    {
        readonly Dictionary<IntPtr, (float Base, float Written)> values = new Dictionary<IntPtr, (float, float)>();

        /// <summary>Returns the base and records <paramref name="transform"/>(base) as the value the caller writes.</summary>
        public float Scale(IntPtr key, float current, Func<float, float> transform)
        {
            float b = current;
            if (values.TryGetValue(key, out var e) && Mathf.Approximately(e.Written, current)) b = e.Base;
            float v = transform(b);
            values[key] = (b, v);
            return v;
        }

        public bool TryGetBase(IntPtr key, out float b)
        {
            bool ok = values.TryGetValue(key, out var e);
            b = e.Base;
            return ok;
        }

        public void Clear() => values.Clear();
    }

    /// <summary>Remembers the original value of an instance field so multipliers never compound.</summary>
    public sealed class BaseCache<T>
    {
        readonly Dictionary<IntPtr, T> values = new Dictionary<IntPtr, T>();

        public T Get(IntPtr key, Func<T> read)
        {
            if (!values.TryGetValue(key, out var v)) { v = read(); values[key] = v; }
            return v;
        }

        public bool TryGet(IntPtr key, out T v) => values.TryGetValue(key, out v);
        public IEnumerable<KeyValuePair<IntPtr, T>> All => values;
        public void Clear() => values.Clear();
    }
}
