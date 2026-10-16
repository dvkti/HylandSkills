using HarmonyLib;
using HylandSkills.Actives;
using HylandSkills.State;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.PlayerScripts.Health;
using Il2CppScheduleOne.Tools;
using UnityEngine;

namespace HylandSkills.Effects
{
    /// <summary>
    /// Damage taken, regeneration and movement speed.
    /// PlayerHealth.MaxHealth is a compiled constant in this game, so toughness is expressed as damage reduction.
    /// </summary>
    public class PlayerModule : EffectModule
    {
        const string MoveLabel = "HylandSkills.Move";
        /// <summary>Seconds without taking damage before Second Wind kicks in.</summary>
        const float RegenDelay = 8f;

        public static float MoveMultiplier
        {
            get
            {
                float m = 1f;
                m *= 1f - Skills.Pct2("bruiser_build");
                if (ActiveManager.IsRunning("adrenaline")) m *= 1f + (Data.SkillDb.Get("adrenaline")?.Value2 ?? 0f) / 100f;
                return m;
            }
        }

        /// <summary>Multiplier applied to incoming damage on the local player.</summary>
        public static float DamageTakenMultiplier
        {
            get
            {
                float m = 1f;
                m *= 1f - Skills.Pct("thick_skin");
                m *= 1f - Skills.Pct("juggernaut");
                m *= 1f - Skills.Pct("bruiser_build");
                if (ActiveManager.IsRunning("adrenaline")) m *= 1f - Data.SkillDb.Get("adrenaline").Value / 100f;
                if (Skills.SynergyActive("home_turf") && InOwnProperty()) m *= 0.7f;
                return Mathf.Max(0.1f, m);
            }
        }

        static bool InOwnProperty()
        {
            var p = Player.Local;
            return p != null && p.CurrentProperty != null && p.CurrentProperty.IsOwned;
        }

        public override void Apply() => ApplyMove();

        static void ApplyMove()
        {
            var pm = Il2CppScheduleOne.DevUtilities.PlayerSingleton<PlayerMovement>.InstanceExists
                ? Il2CppScheduleOne.DevUtilities.PlayerSingleton<PlayerMovement>.Instance
                : null;
            var stack = pm != null ? pm.MoveSpeedMultiplierStack : null;
            if (stack == null) return;
            float m = MoveMultiplier;
            stack.Remove(MoveLabel);
            if (!Mathf.Approximately(m, 1f))
                stack.Add(new FloatStack.StackEntry(MoveLabel, m, FloatStack.EStackMode.Multiplicative, 50));
        }

        /// <summary>Second Wind: extra regeneration on top of the game's own, once per game minute.</summary>
        public override void MinutePass(int now, int previous)
        {
            float pct = Skills.Pct("second_wind");
            if (pct <= 0 || previous < 0) return;
            var h = Player.Local != null ? Player.Local.Health : null;
            if (h == null || !h.IsAlive || h.TimeSinceLastDamage < RegenDelay) return;
            float max = PlayerHealth.MaxHealth;
            if (h.CurrentHealth >= max) return;
            float extra = PlayerHealth.HealthRecoveryPerMinute * pct * Mathf.Clamp(now - previous, 1, 5);
            h.RecoverHealth(Mathf.Min(extra, max - h.CurrentHealth));
        }

        public override void Reset()
        {
            var pm = Il2CppScheduleOne.DevUtilities.PlayerSingleton<PlayerMovement>.InstanceExists
                ? Il2CppScheduleOne.DevUtilities.PlayerSingleton<PlayerMovement>.Instance
                : null;
            pm?.MoveSpeedMultiplierStack?.Remove(MoveLabel);
        }

        [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.RpcLogic___TakeDamage_3505310624))]
        static class TakeDamagePatch
        {
            static void Prefix(PlayerHealth __instance, ref float damage, ref bool flinch)
            {
                if (__instance == null || __instance.Player == null || __instance.Player != Player.Local) return;
                if (damage <= 0) return;
                float before = damage;
                damage *= DamageTakenMultiplier;
                if (Mod.Tracing) Mod.Debug($"[trace] Player damage: {before:0.##} -> {damage:0.##}");
                if (Skills.Has("juggernaut")) flinch = false;
            }
        }
    }
}
