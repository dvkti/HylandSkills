using System;
using System.Collections.Generic;
using HarmonyLib;
using HylandSkills.State;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Employees;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Money;
using Il2CppScheduleOne.Property;
using Il2CppScheduleOne.Tools;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Shop;
using UnityEngine;

namespace HylandSkills.Effects
{
    /// <summary>EMPIRE: laundering, shop prices, wages, employee speed, ATM limit and front businesses.</summary>
    public class EconomyModule : EffectModule
    {
        const string WorkLabel = "HylandSkills.Work";

        static readonly TrackedBase baseLaunder = new TrackedBase();
        static readonly TrackedBase baseWage = new TrackedBase();
        static readonly BaseCache<float> basePrice = new BaseCache<float>();
        static readonly BaseCache<float> baseOverride = new BaseCache<float>();
        static readonly Dictionary<IntPtr, float> appliedWork = new Dictionary<IntPtr, float>();
        static readonly MinuteAccumulator launderAcc = new MinuteAccumulator();
        static float appliedPriceMult = 1f;

        public override void Reset()
        {
            atmOffset = 0f; // the scene (and ATM statics) are reloaded with the next save
            openAtm = null;
            RestorePrices();
            baseLaunder.Clear();
            baseWage.Clear();
            basePrice.Clear();
            baseOverride.Clear();
            appliedWork.Clear();
            launderAcc.Clear();
            appliedPriceMult = 1f;
        }

        public override void Apply()
        {
            ApplyLaundering();
            foreach (var e in Employees()) ApplyEmployee(e, force: true);
            ApplyPrices();
        }

        // ---------------------------------------------------------------- laundering
        static float LaunderMultiplier()
        {
            float m = 1f + Skills.Pct("bookkeeper");
            if (Skills.Has("tycoon")) m *= 1f + Skills.Pct("tycoon");
            if (Skills.Has("legit_fronts")) m *= 1f - Skills.Pct2("legit_fronts");
            return m;
        }

        static void ApplyLaundering()
        {
            if (Business.Businesses == null) return;
            float m = LaunderMultiplier();
            foreach (var b in Business.Businesses)
            {
                if (b == null) continue;
                float cap = baseLaunder.Scale(b.Pointer, b.LaunderCapacity, c => Mathf.Round(c * m));
                if (!Mathf.Approximately(cap, b.LaunderCapacity)) b.LaunderCapacity = cap;
            }
        }

        /// <summary>Tycoon: each business's operations progress 25% faster (one extra minute every fourth tick).</summary>
        [HarmonyPatch(typeof(Business), nameof(Business.MinPass))]
        static class LaunderSpeed
        {
            static void Postfix(Business __instance)
            {
                if (__instance == null || !Skills.Has("tycoon") || __instance.LaunderingOperations == null) return;
                int extra = launderAcc.Scale(__instance.Pointer, 1, 1f + Skills.Pct("tycoon")) - 1;
                if (extra <= 0) return;
                foreach (var op in __instance.LaunderingOperations)
                    if (op != null && op.minutesSinceStarted < op.completionTime_Minutes - extra) op.minutesSinceStarted += extra;
            }
        }

        public static int FinishAllLaundering(float feePct)
        {
            int n = 0;
            if (Business.OwnedBusinesses == null) return 0;
            foreach (var b in Business.OwnedBusinesses)
            {
                if (b?.LaunderingOperations == null) continue;
                foreach (var op in b.LaunderingOperations)
                {
                    if (op == null) continue;
                    op.amount *= 1f - feePct;
                    op.minutesSinceStarted = op.completionTime_Minutes;
                    n++;
                }
            }
            return n;
        }

        // ---------------------------------------------------------------- front businesses (daily income)
        public override void MinutePass(int now, int previous)
        {
            if (previous < 0 || now / 1440 == previous / 1440) return;
            if (!Skills.Has("legit_fronts") || Business.OwnedBusinesses == null) return;
            int count = Business.OwnedBusinesses.Count;
            if (count <= 0) return;
            float amount = Skills.Val("legit_fronts") * count;
            Money.AddOnline(amount, "Front business income", $"{count} business{(count == 1 ? "" : "es")}");
            Mod.Notify("Front businesses paid out", $"+{MoneyManager.FormatAmount(amount)} clean money", UI.Icons.Get("app_icon"));
        }

        // ---------------------------------------------------------------- employees
        static IEnumerable<Employee> Employees()
        {
            if (!NetworkSingleton<EmployeeManager>.InstanceExists) yield break;
            var all = NetworkSingleton<EmployeeManager>.Instance.AllEmployees;
            if (all == null) yield break;
            foreach (var e in all) if (e != null) yield return e;
        }

        static float WorkMultiplier(Employee e)
        {
            float m = 1f;
            if (Skills.Has("overtime_culture")) m *= 1f + Skills.Pct("overtime_culture");
            if (Skills.Has("union_boss")) m *= 1f + Skills.Pct("union_boss");
            if (e.TryCast<Botanist>() != null && Skills.Has("head_botanist")) m *= 1f + Skills.Pct("head_botanist");
            if (e.TryCast<Chemist>() != null && Skills.SynergyActive("industrial_scale")) m *= 1.2f;
            return m;
        }

        static float WageMultiplier(Employee e)
        {
            float m = 1f - Skills.Pct("payroll");
            // Company Culture combo: Payroll + Overtime Culture drops Overtime's wage raise.
            if (Skills.Has("overtime_culture") && !Skills.Combo("company_culture")) m *= 1f + Skills.Pct2("overtime_culture");
            if (Skills.Has("union_boss")) m *= 0.9f;
            if (e.TryCast<Botanist>() != null && Skills.Has("head_botanist")) m *= 1f + Skills.Pct2("head_botanist");
            return m;
        }

        static void ApplyEmployee(Employee e, bool force)
        {
            float wm = WageMultiplier(e);
            float wage = baseWage.Scale(e.Pointer, e.DailyWage, w => Mathf.Round(w * wm));
            if (!Mathf.Approximately(e.DailyWage, wage)) e.DailyWage = wage;

            float m = WorkMultiplier(e);
            if (!force && appliedWork.TryGetValue(e.Pointer, out var last) && Mathf.Approximately(last, m)) return;
            var stack = e.WorkSpeedController;
            if (stack == null) return;
            stack.Remove(WorkLabel);
            if (!Mathf.Approximately(m, 1f)) stack.Add(new FloatStack.StackEntry(WorkLabel, m, FloatStack.EStackMode.Multiplicative, 50));
            appliedWork[e.Pointer] = m;
        }

        [HarmonyPatch(typeof(Employee), nameof(Employee.UpdateBehaviour))]
        static class EmployeeTick
        {
            static void Postfix(Employee __instance)
            {
                if (__instance == null || __instance.Fired) return;
                try { ApplyEmployee(__instance, force: false); } catch { }
            }
        }

        // ---------------------------------------------------------------- shop prices
        static void ApplyPrices()
        {
            float m = 1f - Skills.Pct("bulk_buyer");
            if (Mathf.Approximately(m, appliedPriceMult)) return;
            try
            {
                foreach (var def in Resources.FindObjectsOfTypeAll<StorableItemDefinition>())
                {
                    if (def == null) continue;
                    float b = basePrice.Get(def.Pointer, () => def.BasePurchasePrice);
                    if (b > 0) def.BasePurchasePrice = Mathf.Max(1f, Mathf.Round(b * m * 100f) / 100f);
                }
                foreach (var shop in UnityEngine.Object.FindObjectsOfType<ShopInterface>())
                {
                    if (shop?.Listings == null) continue;
                    foreach (var l in shop.Listings)
                    {
                        if (l == null || !l.OverridePrice) continue;
                        float b = baseOverride.Get(l.Pointer, () => l.OverriddenPrice);
                        l.OverriddenPrice = Mathf.Max(1f, Mathf.Round(b * m * 100f) / 100f);
                    }
                }
                appliedPriceMult = m;
            }
            catch (Exception e) { Mod.Debug("Prices: " + e.Message); }
        }

        static void RestorePrices()
        {
            try
            {
                foreach (var def in Resources.FindObjectsOfTypeAll<StorableItemDefinition>())
                    if (def != null && basePrice.TryGet(def.Pointer, out var b)) def.BasePurchasePrice = b;
            }
            catch { }
        }

        [HarmonyPatch(typeof(ShopInterface), nameof(ShopInterface.Awake))]
        static class ShopAwake
        {
            static void Postfix() { if (Mod.WorldReady) appliedPriceMult = -1f; }
        }

        // ---------------------------------------------------------------- deliveries (Express Shipping, Wholesale Account)
        [HarmonyPatch(typeof(Il2CppScheduleOne.UI.Phone.Delivery.DeliveryShop), nameof(Il2CppScheduleOne.UI.Phone.Delivery.DeliveryShop.GetDeliveryTime))]
        static class DeliveryTime
        {
            static void Postfix(ref int __result)
            {
                if (!Skills.Has("express_shipping") || __result <= 0) return;
                __result = Math.Max(10, Mathf.RoundToInt(__result * (1f - Skills.Pct("express_shipping"))));
            }
        }

        [HarmonyPatch(typeof(Il2CppScheduleOne.UI.Phone.Delivery.DeliveryShop), nameof(Il2CppScheduleOne.UI.Phone.Delivery.DeliveryShop.GetDeliveryCost))]
        static class DeliveryCost
        {
            static void Postfix(ref float __result)
            {
                if (Skills.Combo("wholesale_account")) __result = 0f;
            }
        }

        // ---------------------------------------------------------------- pawn shop (Pawn Broker)
        [HarmonyPatch(typeof(PawnShopInterface), nameof(PawnShopInterface.StartNegotiation))]
        static class PawnOpen
        {
            static void Postfix(PawnShopInterface __instance)
            {
                if (__instance == null || !Skills.Has("pawn_broker")) return;
                float m = 1f + Skills.Pct("pawn_broker");
                __instance.InitialShopOffer *= m;
                __instance.LastShopOffer *= m;
            }
        }

        [HarmonyPatch(typeof(PawnShopInterface), nameof(PawnShopInterface.EvaluateCounter))]
        static class PawnCalm
        {
            static void Postfix(ref float angerChange)
            {
                if (Skills.Has("pawn_broker") && angerChange > 0) angerChange *= Mathf.Max(0.3f, 1f - 2f * Skills.Pct("pawn_broker"));
            }
        }

        // ---------------------------------------------------------------- ATM
        // ATM.WeeklyDepositLimit is a compiled constant and the ATM screen clamps every amount against it inline,
        // so patching remainingAllowedDeposit alone only fixes the label. While an ATM screen is open, the writable
        // ATM.WeeklyDepositSum is lowered by the skill bonus, so the game's own math allows exactly the extra amount.
        // The offset is removed as soon as the screen closes.
        static float atmOffset;
        static ATMInterface openAtm;

        public static float DepositLimit
        {
            get
            {
                float limit = ATM.WeeklyDepositLimit;
                float bonus = Skills.Val("atm_connections");
                if (Skills.Has("offshore_account")) bonus += limit;
                return limit + bonus;
            }
        }

        static float DepositBonus => DepositLimit - ATM.WeeklyDepositLimit;

        /// <summary>Deposits made this week, without the temporary offset.</summary>
        public static float RealDepositSum => ATM.WeeklyDepositSum + atmOffset;

        static void SetAtmOffset(float offset)
        {
            if (Mathf.Approximately(offset, atmOffset)) return;
            ATM.WeeklyDepositSum = ATM.WeeklyDepositSum + atmOffset - offset;
            atmOffset = offset;
        }

        public override void Update(float dt)
        {
            if (atmOffset == 0f) return;
            if (openAtm == null || !openAtm.IsOpen)
            {
                SetAtmOffset(0f);
                openAtm = null;
            }
        }

        [HarmonyPatch(typeof(ATMInterface), nameof(ATMInterface.remainingAllowedDeposit), MethodType.Getter)]
        static class AtmRemaining
        {
            static void Postfix(ref float __result)
            {
                if (DepositBonus <= 0f && atmOffset == 0f) return;
                __result = Mathf.Max(0f, DepositLimit - RealDepositSum);
            }
        }

        [HarmonyPatch(typeof(ATMInterface), nameof(ATMInterface.Update))]
        static class AtmLabel
        {
            static void Postfix(ATMInterface __instance)
            {
                if (__instance == null || !__instance.IsOpen) return;
                float bonus = DepositBonus;
                if (bonus <= 0f && atmOffset == 0f) return;
                openAtm = __instance;
                SetAtmOffset(bonus);
                if (__instance.depositLimitText != null)
                    __instance.depositLimitText.text = MoneyManager.FormatAmount(RealDepositSum) + " / " + MoneyManager.FormatAmount(DepositLimit);
                if (__instance.menu_DepositButton != null)
                    __instance.menu_DepositButton.interactable = RealDepositSum < DepositLimit;
            }
        }
    }
}
