using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HylandSkills.Actives;
using HylandSkills.State;
using Il2CppFishNet.Connection;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.PlayerScripts;
using UnityEngine;

namespace HylandSkills.Effects
{
    /// <summary>CHEMISTRY: station speed, Overclock + burnout, output quality, ingredient refunds and bonus units.</summary>
    public class StationModule : EffectModule
    {
        public const string BurnoutKey = "overclock_burnout";

        static readonly MinuteAccumulator acc = new MinuteAccumulator();

        public override void Reset()
        {
            acc.Clear();
            pendingAlchemy.Clear();
        }

        public override void CollectStatus(System.Collections.Generic.List<StatusInfo> list)
        {
            if (ActiveManager.GameWindowActive(BurnoutKey))
            {
                list.Add(new StatusInfo
                {
                    Label = "Burnout",
                    TimeLeft = ActiveManager.FormatTime(ActiveManager.GameWindowLeft(BurnoutKey), Data.CooldownClock.GameMinutes),
                    Icon = "gauge",
                    Color = UI.UiKit.Theme.Warn
                });
            }
        }

        static bool InArea(string key, Il2CppScheduleOne.Property.Property p)
        {
            if (p == null) return false;
            return Skills.Data.EffectProperty.TryGetValue(key, out var code) && p.PropertyCode == code;
        }

        enum Kind { Chemistry, Oven, Cauldron, Mixer }

        static float SpeedFor(Kind k, Il2CppScheduleOne.Property.Property p)
        {
            float m = 1f;
            switch (k)
            {
                case Kind.Chemistry:
                case Kind.Oven:
                    m += Skills.Pct("steady_hands");
                    if (Skills.Has("volatile_chemistry")) m += Skills.Pct("volatile_chemistry");
                    break;
                case Kind.Cauldron:
                    if (Skills.Has("volatile_chemistry")) m += Skills.Pct("volatile_chemistry");
                    break;
                case Kind.Mixer:
                    m += Skills.Pct("mixologist");
                    break;
            }
            if (ActiveManager.IsRunning("overclock") && InArea("overclock", p)) m *= 1f + Data.SkillDb.Get("overclock").Value / 100f;
            else if (ActiveManager.GameWindowActive(BurnoutKey) && InArea("overclock", p)) m *= 1f - Data.SkillDb.Get("overclock").Value2 / 100f;
            return m;
        }

        // ---------------------------------------------------------------- speed: shared pattern
        // Each station's minute tick gets its extra (or missing) minutes added straight to the progress field after
        // the game ticked. Time skips (sleeping) scale the minutes passed to OnTimePass instead. Some ticks call
        // OnTimePass(1) internally and Mk2 overrides call the base, so only the outermost call outside a tick scales.
        static int tickDepth, skipDepth, guardFrame;

        static void Guard()
        {
            if (guardFrame == Time.frameCount) return;
            guardFrame = Time.frameCount; // a thrown exception can never leave the counters stuck
            tickDepth = 0;
            skipDepth = 0;
        }

        static void TickStart() { Guard(); tickDepth++; }
        static void TickEnd() { if (tickDepth > 0) tickDepth--; }

        static void SkipStart(IntPtr key, ref int minutes, float mult)
        {
            Guard();
            if (skipDepth++ == 0 && tickDepth == 0) minutes = acc.Scale(key, minutes, mult);
        }

        static void SkipEnd() { if (skipDepth > 0) skipDepth--; }

        /// <summary>Extra minutes for one tick (negative while slowed).</summary>
        static int TickExtra(IntPtr key, float mult) => acc.Scale(key, 1, mult) - 1;

        // ---------------------------------------------------------------- chemistry station
        [HarmonyPatch(typeof(ChemistryStation), nameof(ChemistryStation.OnMinPass))]
        static class ChemMin
        {
            static void Prefix() => TickStart();

            static void Postfix(ChemistryStation __instance)
            {
                TickEnd();
                var op = __instance != null ? __instance.CurrentCookOperation : null;
                if (op == null || op.Recipe == null || op.CurrentTime <= 0 || op.CurrentTime >= op.Recipe.CookTime_Mins) return;
                int extra = TickExtra(__instance.Pointer, SpeedFor(Kind.Chemistry, __instance.ParentProperty));
                if (extra != 0) op.CurrentTime = Mathf.Clamp(op.CurrentTime + extra, 0, op.Recipe.CookTime_Mins);
            }
        }

        [HarmonyPatch(typeof(ChemistryStation), nameof(ChemistryStation.OnTimePass))]
        static class ChemSkip
        {
            static void Prefix(ChemistryStation __instance, ref int minutes)
            {
                if (__instance != null) SkipStart(__instance.Pointer, ref minutes, SpeedFor(Kind.Chemistry, __instance.ParentProperty));
            }

            static void Postfix() => SkipEnd();
        }

        // ---------------------------------------------------------------- lab oven
        [HarmonyPatch(typeof(LabOven), nameof(LabOven.OnUncappedMinPass))]
        static class OvenMin
        {
            static void Prefix() => TickStart();

            static void Postfix(LabOven __instance)
            {
                TickEnd();
                var op = __instance != null ? __instance.CurrentOperation : null;
                if (op == null || op.CookProgress <= 0) return;
                int duration = op.GetCookDuration();
                if (op.CookProgress >= duration) return;
                int extra = TickExtra(__instance.Pointer, SpeedFor(Kind.Oven, __instance.ParentProperty));
                if (extra != 0) op.CookProgress = Mathf.Clamp(op.CookProgress + extra, 0, duration);
            }
        }

        [HarmonyPatch(typeof(LabOven), nameof(LabOven.OnTimePass))]
        static class OvenSkip
        {
            static void Prefix(LabOven __instance, ref int minutes)
            {
                if (__instance != null) SkipStart(__instance.Pointer, ref minutes, SpeedFor(Kind.Oven, __instance.ParentProperty));
            }

            static void Postfix() => SkipEnd();
        }

        /// <summary>The oven's hammer step finalises meth and cocaine: roll quality here.</summary>
        [HarmonyPatch(typeof(LabOven), nameof(LabOven.Shatter))]
        static class OvenQuality
        {
            static void Prefix(LabOven __instance)
            {
                var op = __instance != null ? __instance.CurrentOperation : null;
                if (op == null) return;
                float chance = Skills.Pct("precise_measures");
                bool meth = op.ProductID != null && op.ProductID.ToLowerInvariant().Contains("meth");
                if (meth && Skills.Has("crystal_purity")) chance += Skills.Pct("crystal_purity");
                op.IngredientQuality = G.RollStationQuality(op.IngredientQuality, chance);
            }
        }

        // ---------------------------------------------------------------- cauldron
        [HarmonyPatch(typeof(Cauldron), nameof(Cauldron.OnMinPass))]
        static class CauldronMin
        {
            static void Prefix() => TickStart();

            static void Postfix(Cauldron __instance)
            {
                TickEnd();
                if (__instance == null || __instance.RemainingCookTime <= 0) return;
                int extra = TickExtra(__instance.Pointer, SpeedFor(Kind.Cauldron, __instance.ParentProperty));
                if (extra != 0) __instance.RemainingCookTime = Math.Max(0, __instance.RemainingCookTime - extra);
            }
        }

        [HarmonyPatch(typeof(Cauldron), nameof(Cauldron.OnTimePass))]
        static class CauldronSkip
        {
            static void Prefix(Cauldron __instance, ref int minutes)
            {
                if (__instance != null) SkipStart(__instance.Pointer, ref minutes, SpeedFor(Kind.Cauldron, __instance.ParentProperty));
            }

            static void Postfix() => SkipEnd();
        }

        [HarmonyPatch(typeof(Cauldron), nameof(Cauldron.RpcLogic___StartCookOperation_4210838825))]
        static class CauldronQuality
        {
            static void Prefix(ref EQuality quality)
            {
                quality = G.RollStationQuality(quality, Skills.Pct("precise_measures"));
            }
        }

        /// <summary>Coca Refiner: part of the leaves goes back into the cauldron's input.</summary>
        [HarmonyPatch(typeof(Cauldron), nameof(Cauldron.RemoveIngredients))]
        static class CauldronRefund
        {
            static void Prefix(Cauldron __instance, out ItemInstance __state)
            {
                __state = null;
                if (__instance == null || !Skills.Has("coca_refiner")) return;
                foreach (var slot in __instance.IngredientSlots)
                {
                    var inst = slot?.ItemInstance;
                    if (inst != null && inst.ID != null && inst.ID.ToLowerInvariant().Contains("coca"))
                    {
                        int refund = Mathf.RoundToInt(20 * Skills.Pct("coca_refiner"));
                        __state = inst.GetCopy(refund);
                        break;
                    }
                }
            }

            static void Postfix(Cauldron __instance, ItemInstance __state)
            {
                if (__state == null || __instance == null) return;
                foreach (var slot in __instance.IngredientSlots)
                {
                    if (slot != null && (slot.ItemInstance == null || slot.ItemInstance.CanStackWith(__state, false)))
                    {
                        slot.InsertItem(__state);
                        return;
                    }
                }
                GiveToPlayer(__state);
            }
        }

        // ---------------------------------------------------------------- mixing station
        [HarmonyPatch(typeof(MixingStation), nameof(MixingStation.OnMinPass))]
        static class MixMin
        {
            static void Prefix() => TickStart();

            static void Postfix(MixingStation __instance)
            {
                TickEnd();
                if (pendingAlchemy.Count > 0) ApplyAlchemy(__instance);
                if (__instance == null || __instance.CurrentMixOperation == null || __instance.CurrentMixTime <= 0) return;
                int total = __instance.GetMixTimeForCurrentOperation();
                if (__instance.CurrentMixTime >= total) return;
                int extra = TickExtra(__instance.Pointer, SpeedFor(Kind.Mixer, __instance.ParentProperty));
                if (extra != 0) __instance.CurrentMixTime = Mathf.Clamp(__instance.CurrentMixTime + extra, 0, total);
            }
        }

        /// <summary>MixingStationMk2 overrides OnTimePass, so both versions are patched (outermost call scales).</summary>
        [HarmonyPatch]
        static class MixSkip
        {
            static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(MixingStation), nameof(MixingStation.OnTimePass));
                yield return AccessTools.Method(typeof(MixingStationMk2), nameof(MixingStationMk2.OnTimePass));
            }

            static void Prefix(MixingStation __instance, ref int minutes)
            {
                if (__instance != null) SkipStart(__instance.Pointer, ref minutes, SpeedFor(Kind.Mixer, __instance.ParentProperty));
            }

            static void Postfix() => SkipEnd();
        }

        [HarmonyPatch(typeof(MixingStation), nameof(MixingStation.TryCreateOutputItems))]
        static class MixOutput
        {
            static void Prefix(MixingStation __instance, out MixOperation __state)
            {
                __state = __instance != null ? __instance.CurrentMixOperation : null;
            }

            static void Postfix(MixingStation __instance, MixOperation __state)
            {
                if (__instance == null || __state == null) return;
                try
                {
                    if (Skills.Has("alchemist") && EffectHub.Roll(Skills.Pct("alchemist")))
                    {
                        // The output item can land in the slot after this call, so the bonus may wait for it.
                        pendingAlchemy[__instance.Pointer] = Math.Max(1, Mathf.RoundToInt(__state.Quantity * 0.25f));
                        ApplyAlchemy(__instance);
                    }
                    // Signature Recipe combo: mixes that make a 3+ effect product always refund the ingredient.
                    bool signature = false;
                    if (Skills.Combo("signature_recipe"))
                    {
                        try
                        {
                            __state.GetOutput(out var props);
                            signature = props != null && props.Count >= 3;
                        }
                        catch { }
                    }
                    if (Skills.Has("ingredient_saver") && (signature || EffectHub.Roll(Skills.Pct("ingredient_saver"))))
                    {
                        var def = Registry.GetItem(__state.IngredientID);
                        if (def != null)
                        {
                            var item = def.GetDefaultInstance(__state.Quantity);
                            var slot = __instance.MixerSlot;
                            if (slot != null && (slot.ItemInstance == null || slot.ItemInstance.CanStackWith(item, false))) slot.InsertItem(item);
                            else GiveToPlayer(item);
                            UI.Hud.ShowToast("Ingredient Saver: ingredients recovered", Data.SkillDb.Info(Data.Branch.Chemistry).Color);
                        }
                    }
                }
                catch (Exception e) { Mod.Debug("Mix output: " + e.Message); }
            }
        }

        static readonly Dictionary<IntPtr, int> pendingAlchemy = new Dictionary<IntPtr, int>();

        static void ApplyAlchemy(MixingStation station)
        {
            if (station == null || !pendingAlchemy.TryGetValue(station.Pointer, out int bonus)) return;
            var output = station.OutputSlot;
            if (output == null || output.ItemInstance == null) return;
            pendingAlchemy.Remove(station.Pointer); // first: ChangeQuantity raises OutputChanged again
            int before = output.Quantity;
            output.ChangeQuantity(bonus, false);
            if (Mod.Tracing) Mod.Debug($"[trace] Alchemist: output {before} -> {output.Quantity}");
        }

        [HarmonyPatch(typeof(MixingStation), nameof(MixingStation.OutputChanged))]
        static class MixOutputChanged
        {
            static void Postfix(MixingStation __instance)
            {
                if (pendingAlchemy.Count > 0) ApplyAlchemy(__instance);
            }
        }

        static void GiveToPlayer(ItemInstance item)
        {
            var inv = PlayerSingleton<PlayerInventory>.InstanceExists ? PlayerSingleton<PlayerInventory>.Instance : null;
            if (inv != null && inv.CanItemFitInInventory(item, item.Quantity)) inv.AddItemToInventory(item);
        }
    }
}
