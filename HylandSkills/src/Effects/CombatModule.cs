using System;
using System.Collections.Generic;
using HarmonyLib;
using HylandSkills.State;
using Il2CppScheduleOne.Cartel;
using Il2CppScheduleOne.Combat;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.Equipping;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.PlayerScripts;
using UnityEngine;

namespace HylandSkills.Effects
{
    /// <summary>MUSCLE: outgoing damage, weapon spread, cartel ambushes/robberies and the Intimidate flee.</summary>
    public class CombatModule : EffectModule
    {
        static readonly HashSet<IntPtr> tunedWeapons = new HashSet<IntPtr>();
        static readonly List<(NPC npc, float until)> fleeing = new List<(NPC, float)>();

        public override void Reset()
        {
            tunedWeapons.Clear();
            fleeing.Clear();
        }

        public override void Update(float dt)
        {
            if (fleeing.Count == 0) return;
            for (int i = fleeing.Count - 1; i >= 0; i--)
            {
                if (Time.time < fleeing[i].until) continue;
                try { fleeing[i].npc?.Behaviour?.FleeBehaviour?.Disable_Networked(null); } catch { }
                fleeing.RemoveAt(i);
            }
        }

        // ---------------------------------------------------------------- outgoing damage
        [HarmonyPatch(typeof(NPC), nameof(NPC.RpcLogic___ReceiveImpact_427288424))]
        static class Damage
        {
            static void Prefix(Impact impact)
            {
                if (impact == null) return;
                if (Mod.Tracing) Mod.Debug($"[trace] NPC impact: {impact.ImpactType} {impact.ImpactDamage:0.##} from local {G.IsLocalPlayer(impact.ImpactSource)}");
                if (!G.IsLocalPlayer(impact.ImpactSource)) return;
                switch (impact.ImpactType)
                {
                    case EImpactType.Punch:
                    case EImpactType.BluntMetal:
                    case EImpactType.SharpMetal:
                        impact.ImpactDamage *= 1f + Skills.Pct("heavy_hands");
                        // Momentum synergy (Mobility + Muscle): hits thrown while sprinting land harder.
                        if (Skills.SynergyActive("momentum") && Il2CppScheduleOne.DevUtilities.PlayerSingleton<Il2CppScheduleOne.PlayerScripts.PlayerMovement>.InstanceExists
                            && Il2CppScheduleOne.DevUtilities.PlayerSingleton<Il2CppScheduleOne.PlayerScripts.PlayerMovement>.Instance.IsSprinting)
                            impact.ImpactDamage *= 1.25f;
                        break;
                    case EImpactType.Bullet:
                        impact.ImpactDamage *= 1f + Skills.Pct("gunslinger");
                        break;
                }
            }
        }

        // ---------------------------------------------------------------- spread
        [HarmonyPatch(typeof(Equippable_RangedWeapon), nameof(Equippable_RangedWeapon.Equip))]
        static class Spread
        {
            static void Postfix(Equippable_RangedWeapon __instance)
            {
                if (__instance == null || !Skills.Has("steady_aim") || tunedWeapons.Contains(__instance.Pointer)) return;
                float m = 1f - Skills.Pct("steady_aim");
                __instance.MinSpread *= m;
                __instance.MaxSpread *= m;
                tunedWeapons.Add(__instance.Pointer);
            }
        }

        // ---------------------------------------------------------------- cartel
        [HarmonyPatch(typeof(Ambush), nameof(Ambush.CanPlayerBeAmbushed))]
        static class Ambushes
        {
            static void Postfix(Player player, ref bool __result)
            {
                if (!__result || player != Player.Local || !Skills.Has("notorious")) return;
                if (EffectHub.Roll(Skills.Pct("notorious"))) __result = false;
                if (Mod.Tracing) Mod.Debug($"[trace] Ambush check: notorious, final {__result}");
            }
        }

        [HarmonyPatch(typeof(Dealer), nameof(Dealer.TryRobDealer))]
        static class NoRobbery
        {
            static bool Prefix() => !Skills.Has("protection_racket");
        }

        [HarmonyPatch(typeof(RobDealer), nameof(RobDealer.GetDealerToRob))]
        static class NoRobberyTarget
        {
            static void Postfix(ref Dealer __result)
            {
                if (Mod.Tracing) Mod.Debug($"[trace] GetDealerToRob: game {(__result != null ? __result.name : "none")}, racket {Skills.Has("protection_racket")}");
                if (Skills.Has("protection_racket")) __result = null;
            }
        }

        // ---------------------------------------------------------------- helpers for actives
        public static int Intimidate(float radius, float seconds)
        {
            var p = Player.Local;
            if (p == null) return 0;
            int n = 0;
            foreach (var goon in UnityEngine.Object.FindObjectsOfType<CartelGoon>())
            {
                if (goon == null || !goon.IsConscious) continue;
                if (Vector3.Distance(goon.transform.position, p.transform.position) > radius) continue;
                var flee = goon.Behaviour?.FleeBehaviour;
                if (flee == null) continue;
                try
                {
                    flee.SetEntityToFlee(p.NetworkObject);
                    flee.Enable_Networked();
                    fleeing.Add((goon, Time.time + seconds));
                    n++;
                    // Reputation combo: fleeing goons drop some cash.
                    if (Skills.Combo("reputation")) Money.AddCash(Mathf.Round(UnityEngine.Random.Range(50f, 150f)));
                }
                catch (Exception e) { Mod.Debug("Intimidate: " + e.Message); }
            }
            return n;
        }
    }
}
