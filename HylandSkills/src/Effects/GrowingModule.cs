using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HylandSkills.Actives;
using HylandSkills.State;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Growing;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.PlayerScripts;
using UnityEngine;

namespace HylandSkills.Effects
{
    /// <summary>CULTIVATION: growth speed, water, yield, harvest quality, seeds, soil and mushrooms.</summary>
    public class GrowingModule : EffectModule
    {
        static readonly TrackedBase baseGrowSpeed = new TrackedBase();
        static readonly TrackedBase baseDrain = new TrackedBase();

        public override void Reset()
        {
            baseGrowSpeed.Clear();
            baseDrain.Clear();
            baseShroomTime.Clear();
        }

        public override void CollectStatus(System.Collections.Generic.List<StatusInfo> list) { }

        // ---------------------------------------------------------------- helpers
        static bool InOverdriveArea(GrowContainer c)
        {
            if (!ActiveManager.IsRunning("grow_overdrive") || c == null) return false;
            var prop = c.ParentProperty;
            return prop != null && Skills.Data.EffectProperty.TryGetValue("grow_overdrive", out var code) && prop.PropertyCode == code;
        }

        static float GrowthMultiplier(GrowContainer c)
        {
            float m = 1f;
            if (Skills.Has("living_soil")) m *= 1f - Skills.Pct2("living_soil");
            if (InOverdriveArea(c)) m *= 1f + Data.SkillDb.Get("grow_overdrive").Value / 100f;
            return m;
        }

        static float DrainMultiplier(GrowContainer c)
        {
            float m = 1f - Skills.Pct("deep_roots");
            // Hydro Rig combo: Overdrive stops costing extra water.
            if (InOverdriveArea(c) && !Skills.Combo("hydro_rig")) m *= Data.SkillDb.Get("grow_overdrive").Value2;
            return Mathf.Max(0.05f, m);
        }

        static bool UsesBoosters(Pot pot)
        {
            if (pot == null || pot.AppliedAdditives == null) return false;
            foreach (var a in pot.AppliedAdditives)
            {
                if (a == null) continue;
                if (a.InstantGrowth > 0f || a.YieldMultiplier > 1.001f) return true;
            }
            return false;
        }

        static string PotKey(Pot pot)
        {
            try { return pot.GUID.ToString(); } catch { return null; }
        }

        public static bool IsPruned(Pot pot)
        {
            var k = PotKey(pot);
            return k != null && Skills.Data.PrunedPots.Contains(k);
        }

        public static void MarkPruned(Pot pot)
        {
            var k = PotKey(pot);
            if (k != null && !Skills.Data.PrunedPots.Contains(k)) Skills.Data.PrunedPots.Add(k);
        }

        // ---------------------------------------------------------------- growth speed
        [HarmonyPatch(typeof(Plant), nameof(Plant.MinPass))]
        static class PlantMinPassPatch
        {
            static void Prefix(Plant __instance)
            {
                var pot = __instance != null ? __instance.Pot : null;
                if (pot == null) return;
                float m = GrowthMultiplier(pot);
                float v = baseGrowSpeed.Scale(pot.Pointer, pot.GrowSpeedMultiplier, b => b * m);
                if (!Mathf.Approximately(v, pot.GrowSpeedMultiplier)) pot.GrowSpeedMultiplier = v;
            }
        }

        // ---------------------------------------------------------------- water drain (pots and mushroom beds)
        static void ApplyDrain(GrowContainer c)
        {
            if (c == null) return;
            float m = DrainMultiplier(c);
            float v = baseDrain.Scale(c.Pointer, c._moistureDrainPerHour, b => b * m);
            if (!Mathf.Approximately(v, c._moistureDrainPerHour)) c._moistureDrainPerHour = v;
        }

        [HarmonyPatch(typeof(GrowContainer), nameof(GrowContainer.OnMinPass))]
        static class DrainMinPatch
        {
            static void Prefix(GrowContainer __instance) => ApplyDrain(__instance);
        }

        [HarmonyPatch(typeof(GrowContainer), nameof(GrowContainer.OnTimeSkipped))]
        static class DrainSkipPatch
        {
            static void Prefix(GrowContainer __instance) => ApplyDrain(__instance);
        }

        // ---------------------------------------------------------------- yield (decided when the plant finishes growing)
        [HarmonyPatch(typeof(Plant), nameof(Plant.GrowthDone))]
        static class GrowthDonePatch
        {
            // The game reads Plant.YieldMultiplier only here, to pick how many buds grow. The skill bonus is applied
            // for this call and the original value is restored afterwards, so a repeated GrowthDone never compounds.
            static void Prefix(Plant __instance, out float __state)
            {
                __state = -1f;
                if (__instance == null || !Mod.WorldReady) return; // plants restored from a save keep their saved buds
                float y = 1f + Skills.Pct("green_thumb");
                var pot = __instance.Pot;
                if (Skills.Has("organic_methods") && UsesBoosters(pot)) y *= 1f - Skills.Pct2("organic_methods");
                if (pot != null && IsPruned(pot))
                {
                    float loss = Data.SkillDb.Get("prune").Value / 100f;
                    // Hand-Tended combo: pruning an organic plant only costs half the yield.
                    if (Skills.Combo("hand_tended") && !UsesBoosters(pot)) loss *= 0.5f;
                    y *= 1f - loss;
                }
                if (Mathf.Approximately(y, 1f)) return;
                __state = __instance.YieldMultiplier;
                __instance.YieldMultiplier = __state * y;
                if (Mod.Tracing) Mod.Debug($"[trace] GrowthDone: yield x{y:0.###} ({__state:0.###} -> {__instance.YieldMultiplier:0.###}), base {__instance.BaseYieldQuantity}");
            }

            static void Postfix(Plant __instance, float __state)
            {
                if (__instance == null) return;
                if (__state >= 0f) __instance.YieldMultiplier = __state;
                if (Mod.Tracing) Mod.Debug($"[trace] GrowthDone: active buds {__instance.ActiveHarvestables?.Count ?? -1}");
            }
        }

        // ---------------------------------------------------------------- harvest quality
        /// <summary>
        /// WeedPlant and CocaPlant override GetHarvestedProduct without calling the base method, so each override is
        /// patched. A depth counter makes sure the bonus is rolled once even if an override does call the base.
        /// </summary>
        [HarmonyPatch]
        static class HarvestPatch
        {
            static int depth;

            static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(Plant), nameof(Plant.GetHarvestedProduct));
                yield return AccessTools.Method(typeof(WeedPlant), nameof(WeedPlant.GetHarvestedProduct));
                yield return AccessTools.Method(typeof(CocaPlant), nameof(CocaPlant.GetHarvestedProduct));
            }

            static int frame;

            static void Prefix()
            {
                if (frame != Time.frameCount) { frame = Time.frameCount; depth = 0; } // never stuck after an exception
                depth++;
            }

            static void Postfix(Plant __instance, ItemInstance __result)
            {
                if (--depth > 0) return;
                if (__instance == null || __result == null) return;
                var q = __result.TryCast<QualityItemInstance>();
                if (q == null) return;
                var pot = __instance.Pot;
                int shift = 0;
                float chance = Skills.Pct("green_sense");
                if (Skills.Has("organic_methods") && !UsesBoosters(pot)) chance += Skills.Pct("organic_methods");
                if (EffectHub.Roll(chance)) shift += 1;
                if (Skills.Has("heirloom_genetics") && EffectHub.Roll(Skills.Pct("heirloom_genetics"))) shift += 2;
                if (pot != null && IsPruned(pot)) shift += 1;
                var before = q.Quality;
                if (shift > 0) q.SetQuality(G.Shift(q.Quality, shift));
                if (Mod.Tracing) Mod.Debug($"[trace] Harvest: chance {chance:0.###}, shift {shift}, quality {before} -> {q.Quality}");
            }
        }

        // ---------------------------------------------------------------- seeds + soil
        [HarmonyPatch(typeof(Pot), nameof(Pot.OnPlantFullyHarvested))]
        static class FullyHarvestedPatch
        {
            static void Prefix(Pot __instance, out int __state)
            {
                __state = -1;
                if (__instance == null) return;

                // Living Soil: the pot spends one soil use here (and clears the soil at 0). One use is added first, so
                // the count ends where it started; the postfix undoes it if the game did not spend a use after all.
                if (Skills.Has("living_soil") && __instance._remainingSoilUses > 0)
                {
                    __state = __instance._remainingSoilUses;
                    __instance._remainingSoilUses = __state + 1;
                }

                var key = PotKey(__instance);
                if (key != null) Skills.Data.PrunedPots.Remove(key);

                var seed = __instance.Plant != null ? __instance.Plant.SeedDefinition : null;
                // Closed Loop combo: Living Soil + Seed Saver doubles the chance.
                float seedChance = Skills.Pct("seed_saver") * (Skills.Combo("closed_loop") ? 2f : 1f);
                if (seed == null || !Skills.Has("seed_saver") || !EffectHub.Roll(seedChance)) return;
                try
                {
                    var inv = PlayerSingleton<PlayerInventory>.Instance;
                    var item = seed.GetDefaultInstance(1);
                    if (inv != null && inv.CanItemFitInInventory(item, 1))
                    {
                        inv.AddItemToInventory(item);
                        UI.Hud.ShowToast($"Seed Saver: +1 {seed.Name}", Data.SkillDb.Info(Data.Branch.Cultivation).Color);
                    }
                }
                catch (Exception e) { Mod.Debug("Seed saver: " + e.Message); }
            }

            static void Postfix(Pot __instance, int __state)
            {
                if (__instance == null || __state < 0) return;
                if (__instance._remainingSoilUses > __state) __instance._remainingSoilUses = __state;
                if (Mod.Tracing) Mod.Debug($"[trace] Living Soil: soil uses {__state} -> {__instance._remainingSoilUses}");
            }
        }

        // ---------------------------------------------------------------- mushrooms
        // GetCurrentGrowthRate is inlined into the colony's minute tick, so the speed comes from its grow time.
        static readonly TrackedBase baseShroomTime = new TrackedBase();

        [HarmonyPatch(typeof(ShroomColony), nameof(ShroomColony.OnMinPass))]
        static class ShroomGrowthPatch
        {
            static void Prefix(ShroomColony __instance)
            {
                if (__instance == null) return;
                float speed = 1f;
                if (Skills.Has("mycologist")) speed *= 1f + Skills.Pct("mycologist");
                if (Skills.Has("living_soil")) speed *= 1f - Skills.Pct2("living_soil");
                float t = baseShroomTime.Scale(__instance.Pointer, __instance._growTime, b => Mathf.Max(1f, Mathf.Round(b / speed)));
                if ((int)t != __instance._growTime) __instance._growTime = (int)t;
            }
        }

        [HarmonyPatch(typeof(ShroomColony), nameof(ShroomColony.GetHarvestedShroom))]
        static class ShroomHarvestPatch
        {
            static void Postfix(ItemInstance __result)
            {
                if (__result == null) return;
                if (Skills.Has("mycologist") && EffectHub.Roll(Skills.Pct("mycologist"))) __result.ChangeQuantity(1);
                var q = __result.TryCast<QualityItemInstance>();
                if (q != null && Skills.Has("heirloom_genetics") && EffectHub.Roll(Skills.Pct("heirloom_genetics")))
                    q.SetQuality(G.Shift(q.Quality, 2));
            }
        }
    }
}
