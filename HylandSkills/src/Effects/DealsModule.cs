using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HylandSkills.Actives;
using HylandSkills.State;
using Il2CppFishNet.Object;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.NPCs.Relation;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.SpecialCustomers;
using UnityEngine;

namespace HylandSkills.Effects
{
    /// <summary>HUSTLE (+ deal-payment effects from other branches): payments, haggling, samples, relationships, addiction and dealers.</summary>
    public class DealsModule : EffectModule
    {
        static readonly TrackedBase baseCut = new TrackedBase();

        public override void Reset() => baseCut.Clear();

        public override void Apply()
        {
            ApplyDealerCut();
            ApplyCrowdReturn();
        }

        public override void MinutePass(int now, int previous)
        {
            if (now % 30 == 0) ApplyDealerCut(); // newly recruited dealers
        }

        // ---------------------------------------------------------------- payment on handover
        [HarmonyPatch(typeof(Customer), nameof(Customer.RpcLogic___ProcessHandoverServerSide_3315874220))]
        static class Payment
        {
            static void Prefix(Customer __instance, Il2CppSystem.Collections.Generic.List<ItemInstance> items, bool handoverByPlayer, ref float totalPayment, NetworkObject dealerObject)
            {
                if (totalPayment <= 0) return;
                float m = 1f;
                var notes = new List<string>();

                if (Skills.Has("premium_brand")) m += Skills.Pct("premium_brand");
                if (handoverByPlayer && ActiveManager.IsRunning("rush_hour")) { m += Data.SkillDb.Get("rush_hour").Value / 100f; notes.Add("Rush Hour"); }
                if (G.IsCurfew)
                {
                    if (Skills.Has("night_king")) m += Skills.Pct("night_king");
                    if (Skills.SynergyActive("back_alley")) m += 0.10f;
                }
                if (dealerObject != null && Skills.Has("kingmaker")) m += 0.10f;

                if (Skills.Has("complex_formulas"))
                {
                    int complex = 0, simple = 0;
                    foreach (var it in items)
                    {
                        var p = it?.TryCast<ProductItemInstance>();
                        var def = p?.Definition?.TryCast<ProductDefinition>();
                        if (def == null) continue;
                        int effects = def.Properties != null ? def.Properties.Count : 0;
                        if (effects >= 3) complex++; else simple++;
                    }
                    if (complex > 0 && simple == 0) m += Skills.Pct("complex_formulas");
                    else if (simple > 0) m -= Skills.Pct2("complex_formulas");
                }

                if (handoverByPlayer && Skills.Has("street_legend") && EffectHub.Roll(0.15f)) { m += 0.10f; notes.Add("Tip"); }

                if (Mod.Tracing) Mod.Debug($"[trace] Payment: x{m:0.###} on {totalPayment:0.##} (player {handoverByPlayer}, dealer {dealerObject != null}, curfew {G.IsCurfew})");
                if (Mathf.Approximately(m, 1f)) return;
                float before = totalPayment;
                totalPayment = Mathf.Max(1f, Mathf.Round(totalPayment * m));
                float diff = totalPayment - before;
                if (handoverByPlayer && Mathf.Abs(diff) >= 1f)
                {
                    string label = notes.Count > 0 ? string.Join(" + ", notes) : "Skills";
                    UI.Hud.ShowToast($"{label}: {(diff > 0 ? "+" : "-")}${Mathf.Abs(diff):N0}", diff > 0 ? UI.UiKit.Theme.Accent : UI.UiKit.Theme.Warn);
                }
            }
        }

        // ---------------------------------------------------------------- haggling and samples
        /// <summary>How much higher than normal a customer will go on a counteroffer (0.24 = 24%).</summary>
        public static float CounterofferBonus
        {
            get
            {
                float c = Skills.Pct("silver_tongue");
                if (Skills.Has("street_legend")) c += Skills.Pct("street_legend");
                if (Skills.SynergyActive("respect")) c += 0.10f;
                // Limited Drop combo: Rush Hour + Premium Brand.
                if (Skills.Combo("limited_drop") && ActiveManager.IsRunning("rush_hour")) c += 0.15f;
                return c;
            }
        }

        /// <summary>
        /// The customer judges the counteroffer as if it were cheaper, so it raises the price ceiling by a bounded
        /// amount instead of turning any absurd price into a coin flip.
        /// </summary>
        [HarmonyPatch(typeof(Customer), nameof(Customer.EvaluateCounteroffer))]
        static class Counteroffer
        {
            static void Prefix(ref float price, out float __state)
            {
                __state = price;
                float c = CounterofferBonus;
                if (c > 0 && price > 0) price /= 1f + c;
            }

            static void Postfix(float __state, float price, bool __result)
            {
                if (Mod.Tracing) Mod.Debug($"[trace] Counteroffer: asked {__state:0.##}, judged as {price:0.##}, accepted {__result}");
            }
        }

        [HarmonyPatch(typeof(Customer), nameof(Customer.GetSampleSuccess))]
        static class Samples
        {
            static void Postfix(ref float __result)
            {
                float game = __result;
                if (Skills.Has("free_taste")) __result = Mathf.Clamp01(__result + Skills.Pct("free_taste"));
                if (Mod.Tracing) Mod.Debug($"[trace] Sample: game {game:0.###}, final {__result:0.###}");
            }
        }

        // ---------------------------------------------------------------- relationships and addiction
        [HarmonyPatch(typeof(NPCRelationData), nameof(NPCRelationData.ChangeRelationship))]
        static class Relationship
        {
            static void Prefix(NPCRelationData __instance, ref float deltaChange)
            {
                if (__instance?.NPC == null || __instance.NPC.GetComponent<Customer>() == null) return;
                if (deltaChange > 0) deltaChange *= 1f + Skills.Pct("people_person");
                else if (deltaChange < 0 && Skills.Has("addiction_economics")) deltaChange *= 1f + Skills.Pct2("addiction_economics");
            }
        }

        /// <summary>
        /// ChangeAddiction is a server RPC whose tiny RpcLogic body is inlined into the reader, so the change is
        /// scaled where it is written instead (every caller goes through the writer, the host included).
        /// </summary>
        [HarmonyPatch(typeof(Customer), nameof(Customer.RpcWriter___Server_ChangeAddiction_431000436))]
        static class Addiction
        {
            static void Prefix(ref float change)
            {
                if (change > 0 && Skills.Has("addiction_economics")) change *= 1f + Skills.Pct("addiction_economics");
            }
        }

        /// <summary>Premium Brand: each customer skips ordering on ~20% of days (stable for the whole day).</summary>
        [HarmonyPatch(typeof(Customer), nameof(Customer.ShouldTryGenerateDeal))]
        static class OrderFrequency
        {
            static void Postfix(Customer __instance, ref bool __result)
            {
                if (!__result || __instance == null || !Skills.Has("premium_brand")) return;
                int day = ActiveManager.GameNow / 1440;
                int h = (__instance.NPC != null ? __instance.NPC.ID.GetHashCode() : 0) ^ (day * 7919);
                float r = (Math.Abs(h) % 1000) / 1000f;
                if (r < Skills.Pct2("premium_brand")) __result = false;
            }
        }

        // ---------------------------------------------------------------- dealers
        static void ApplyDealerCut()
        {
            var dealers = Dealer.AllPlayerDealers;
            if (dealers == null) return;
            float reduction = Skills.Val("dealer_network") / 100f;
            if (Skills.Has("kingmaker")) reduction += 0.05f;
            foreach (var d in dealers)
            {
                var data = d?.DealerData;
                if (data == null) continue;
                // SalesCutPercentage is stored as a 0-1 fraction.
                float cut = baseCut.Scale(data.Pointer, data.SalesCutPercentage, b => Mathf.Max(0.01f, b - reduction));
                if (!Mathf.Approximately(cut, data.SalesCutPercentage)) data.SalesCutPercentage = cut;
            }
        }

        // ---------------------------------------------------------------- special customers
        [HarmonyPatch(typeof(SpecialCustomerManager), nameof(SpecialCustomerManager.GetProductQualityPriceMultiplier))]
        static class VipPrice
        {
            static void Postfix(ref float __result)
            {
                if (Skills.Has("vip_host")) __result *= 1f + Skills.Pct("vip_host");
            }
        }

        // ---------------------------------------------------------------- special customers (Hippies, Bikers, Party Bus)
        [HarmonyPatch(typeof(SpecialCustomerData), nameof(SpecialCustomerData.GetBuyQuantityForPlayerRank))]
        static class CrowdQuantity
        {
            static void Postfix(SpecialCustomerData __instance, ref int __result)
            {
                if (!Skills.Has("party_planner") || __instance == null) return;
                __result = Mathf.RoundToInt(__result * (1f + Skills.Pct("party_planner")));
            }
        }

        static readonly TrackedBase crowdReturn = new TrackedBase();

        /// <summary>Scene Regular: crowds become more likely to come back for every day they stay away.</summary>
        static void ApplyCrowdReturn()
        {
            if (!Il2CppScheduleOne.DevUtilities.NetworkSingleton<SpecialCustomerManager>.InstanceExists) return;
            var m = Il2CppScheduleOne.DevUtilities.NetworkSingleton<SpecialCustomerManager>.Instance;
            float mult = 1f + Skills.Pct("scene_regular");
            float v = crowdReturn.Scale(m.Pointer, m.bonusPerDayUnseen, b => b * mult);
            if (!Mathf.Approximately(v, m.bonusPerDayUnseen)) m.bonusPerDayUnseen = v;
        }

        /// <summary>Afterparty combo: a crowd that just arrived stays one extra day.</summary>
        [HarmonyPatch(typeof(SpecialCustomerManager), nameof(SpecialCustomerManager.RunArrivalPhase))]
        static class Afterparty
        {
            static void Postfix(SpecialCustomerManager __instance)
            {
                if (__instance == null || !Skills.Combo("afterparty") || __instance._currentData == null) return;
                __instance._currentData.DaysLeftInPhase += 1;
                Mod.Debug("Afterparty: crowd stays one more day");
            }
        }

        // ---------------------------------------------------------------- helpers for actives
        public static int HypeText(int count)
        {
            var list = Customer.UnlockedCustomers;
            if (list == null) return 0;
            var candidates = new List<Customer>();
            foreach (var c in list)
            {
                if (c == null || c.CurrentContract != null || c.OfferedContractInfo != null || c.AssignedDealer != null) continue;
                if (c.NPC == null || !c.NPC.IsConscious) continue;
                candidates.Add(c);
            }
            int sent = 0;
            foreach (var c in candidates.OrderBy(_ => EffectHub.Rand()))
            {
                if (sent >= count) break;
                try
                {
                    c.ForceDealOffer();
                    sent++;
                    // Viral combo: Hype Text + Free Taste makes the people you text like you more.
                    if (Skills.Combo("viral")) c.NPC?.RelationData?.ChangeRelationship(0.25f, true);
                }
                catch (Exception e) { Mod.Debug("Hype: " + e.Message); }
            }
            return sent;
        }
    }
}
