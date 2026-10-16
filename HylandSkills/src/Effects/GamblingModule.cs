using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HylandSkills.Actives;
using HylandSkills.State;
using Il2CppScheduleOne.Casino;
using Il2CppScheduleOne.Casino.UI;
using UnityEngine;

namespace HylandSkills.Effects
{
    /// <summary>GAMBLING: casino payouts, table limits, loss insurance, laundered winnings and Peek.</summary>
    public class GamblingModule : EffectModule
    {
        /// <summary>Money moves that wait a moment, so the game has paid the win in cash before we move part of it.</summary>
        static readonly List<(float at, Action act)> pending = new List<(float, Action)>();

        public override void Reset() => pending.Clear();

        public override void Update(float dt)
        {
            if (pending.Count == 0) return;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (Time.time < pending[i].at) continue;
                var a = pending[i].act;
                pending.RemoveAt(i);
                try { a(); } catch (Exception e) { Mod.Debug("Casino money: " + e.Message); }
            }
        }

        static void Later(Action a) => pending.Add((Time.time + 1.5f, a));

        /// <summary>Refunds (Loss Insurance, Card Shark, Comps) stop at this much per game day, so the tables
        /// can never become a guaranteed money printer.</summary>
        public const float DailyRefundCap = 1000f;
        static int refundDay = -1;
        static float refundToday;

        static float CapRefund(float amount)
        {
            int day = ActiveManager.GameNow / 1440;
            if (day != refundDay) { refundDay = day; refundToday = 0f; }
            float allowed = Mathf.Clamp(amount, 0f, Mathf.Max(0f, DailyRefundCap - refundToday));
            refundToday += allowed;
            return allowed;
        }

        static Color BranchColor => Data.SkillDb.Info(Data.Branch.Gambling).Color;

        /// <summary>Bonus on the winnings part of every casino payout (not on the stake).</summary>
        static float WinBonus(float gameSpecific)
        {
            float m = gameSpecific;
            if (Skills.Has("the_house")) m += Skills.Pct("the_house");
            if (ActiveManager.IsRunning("hot_streak")) m += Data.SkillDb.Get("hot_streak").Value / 100f;
            return m;
        }

        /// <summary>Laundered Luck: part of a win goes from cash to the bank as clean money.</summary>
        static void Launder(float payout)
        {
            if (!Skills.Has("laundered_luck") || payout <= 0) return;
            float amount = Mathf.Round(payout * Skills.Pct("laundered_luck"));
            if (amount < 1) return;
            Later(() =>
            {
                float move = Mathf.Min(amount, Money.Cash);
                if (move < 1 || !Money.TrySpendCash(move)) return;
                Money.AddOnline(move, "Casino winnings", "Laundered Luck");
                UI.Hud.ShowToast($"Laundered Luck: ${move:N0} to your bank", BranchColor);
            });
        }

        /// <summary>Loss Insurance, Write-Off, Comps and the Double or Nothing drawback, applied to a lost stake.</summary>
        static void OnLoss(float bet, bool blackjack, bool bust)
        {
            if (bet <= 0) return;
            float refundCash = 0, refundBank = 0, extraCost = 0;
            if (Skills.Has("loss_insurance"))
            {
                float r = bet * Skills.Pct("loss_insurance");
                if (Skills.Combo("write_off")) refundBank += r * 2f; else refundCash += r;
            }
            if (Skills.SynergyActive("comps")) refundBank += bet * 0.03f;
            if (blackjack && bust && Skills.Has("card_shark") && EffectHub.Roll(Skills.Pct("card_shark"))) refundCash += bet;
            if (blackjack && Skills.Has("double_or_nothing")) extraCost += bet * Skills.Pct2("double_or_nothing");

            refundCash = CapRefund(refundCash);
            refundBank = CapRefund(refundBank);
            Later(() =>
            {
                var notes = new List<string>();
                if (refundCash >= 1) { Money.AddCash(Mathf.Round(refundCash)); notes.Add($"+${refundCash:N0}"); }
                if (refundBank >= 1) { Money.AddOnline(Mathf.Round(refundBank), "Casino refund", "Hyland Skills"); notes.Add($"+${refundBank:N0} to bank"); }
                if (extraCost >= 1) { float c = Mathf.Min(extraCost, Money.Cash); if (c >= 1) { Money.TrySpendCash(Mathf.Round(c)); notes.Add($"-${c:N0} (Double or Nothing)"); } }
                if (notes.Count > 0) UI.Hud.ShowToast("Casino: " + string.Join(", ", notes), BranchColor);
            });
        }

        /// <summary>
        /// Pays the skill bonus on a table win as extra cash. The game computes the payout inside its own round code
        /// (GetPayout / GetNetBetMultiplier are inlined there), so patching those changed nothing in a real round.
        /// </summary>
        static void PayWinBonus(float winnings, float bonus, string game)
        {
            float extra = Mathf.Round(winnings * bonus);
            if (Mod.Tracing) Mod.Debug($"[trace] {game} win: winnings {winnings}, bonus {bonus:0.###}, extra {extra}");
            if (extra < 1) return;
            Later(() =>
            {
                Money.AddCash(extra);
                UI.Hud.ShowToast($"{game}: +${extra:N0} skill bonus", BranchColor);
            });
        }

        // ================================================================ BLACKJACK
        [HarmonyPatch(typeof(BlackjackGameController), nameof(BlackjackGameController.RemoveLocalPlayerFromGame))]
        static class BlackjackResult
        {
            static void Prefix(BlackjackGameController __instance, BlackjackGameController.EPayoutType payout)
            {
                if (__instance == null) return;
                float bet = __instance.LocalPlayerBet;
                if (Mod.Tracing) Mod.Debug($"[trace] Blackjack end: {payout}, bet {bet}, cash {Money.Cash}");
                if (payout == BlackjackGameController.EPayoutType.None) { OnLoss(bet, true, __instance.IsLocalPlayerBust); return; }
                if (payout == BlackjackGameController.EPayoutType.Push) return;
                float winnings = __instance.GetPayout(bet, payout) - bet;
                float bonus = WinBonus(Skills.Pct("card_sense") + (Skills.Has("double_or_nothing") ? Skills.Pct("double_or_nothing") : 0f));
                if (winnings > 0 && bonus > 0) PayWinBonus(winnings, bonus, "Blackjack");
                Launder(winnings * (1f + bonus));
            }
        }

        // ================================================================ RIDE THE BUS
        [HarmonyPatch(typeof(RTBGameController), nameof(RTBGameController.RemoveLocalPlayerFromGame))]
        static class BusResult
        {
            static void Prefix(RTBGameController __instance, bool payout)
            {
                if (__instance == null) return;
                float bet = __instance.LocalPlayerBet;
                if (Mod.Tracing) Mod.Debug($"[trace] Ride the Bus end: payout {payout}, bet {bet}, total {__instance.MultipliedLocalPlayerBet}, cash {Money.Cash}");
                if (!payout) { OnLoss(bet, false, false); return; }
                float winnings = __instance.MultipliedLocalPlayerBet - bet;
                float bonus = WinBonus(Skills.Pct("bus_rider"));
                if (winnings > 0 && bonus > 0) PayWinBonus(winnings, bonus, "Ride the Bus");
                Launder(winnings * (1f + bonus));
            }
        }

        // ================================================================ TABLE LIMITS (High Roller)
        /// <summary>
        /// High Roller raises the top of the bet slider when the bet panel opens. (A postfix on GetBetLimits wrote
        /// through its out parameters and left the slider at $0..$0, so the limits are changed on the slider instead.)
        /// </summary>
        [HarmonyPatch(typeof(CasinoGameBetPanel), nameof(CasinoGameBetPanel.Open))]
        static class BetLimits
        {
            static readonly Dictionary<IntPtr, (float vanilla, float set)> raised = new Dictionary<IntPtr, (float, float)>();

            static void Postfix(CasinoGameBetPanel __instance)
            {
                var slider = __instance != null ? __instance._betSlider : null;
                if (slider == null) return;
                if (Mod.Tracing) Mod.Debug($"[trace] Bet slider {slider.minValue}..{slider.maxValue}, value {slider.value}");
                // the panel can be reopened without the game resetting the slider: start from the game's own maximum
                float before = slider.maxValue;
                if (raised.TryGetValue(slider.Pointer, out var r) && Mathf.Approximately(before, r.set)) before = r.vanilla;
                float max = Skills.Has("high_roller") ? Mathf.Round(before * (1f + Skills.Pct("high_roller"))) : before;
                if (max <= slider.minValue) return;
                slider.maxValue = max;
                raised[slider.Pointer] = (before, max);
                if (Mod.Tracing) Mod.Debug($"[trace] High Roller: slider max {before} -> {slider.maxValue}");
            }
        }

        // ================================================================ SLOTS
        /// <summary>Machines whose handle the local player pulled, with the time (other players' spins are left alone).</summary>
        static readonly Dictionary<IntPtr, float> localSpins = new Dictionary<IntPtr, float>();

        [HarmonyPatch(typeof(SlotMachine), nameof(SlotMachine.HandleInteracted))]
        static class SlotHandle
        {
            static void Prefix(SlotMachine __instance)
            {
                if (__instance != null && !__instance.IsSpinning) localSpins[__instance.Pointer] = Time.time;
            }
        }

        /// <summary>
        /// The spin coroutine evaluates the reels and computes the win itself (EvaluateOutcome / GetWinAmount are
        /// inlined there), then shows it with DisplayOutcome. So the skills act here, paid as extra cash: Lucky Seven /
        /// Jackpot Fever pay a Mini Win on a losing spin, Lucky Charm / The House / Hot Streak raise the win.
        /// </summary>
        [HarmonyPatch(typeof(SlotMachine), nameof(SlotMachine.DisplayOutcome))]
        static class SlotOutcome
        {
            static void Prefix(SlotMachine __instance, SlotMachine.EOutcome outcome, int winAmount)
            {
                if (__instance == null || !localSpins.TryGetValue(__instance.Pointer, out var at) || Time.time - at > 20f) return;
                localSpins.Remove(__instance.Pointer);
                int bet = __instance.currentBetAmount;
                int before = winAmount;
                var shown = outcome;

                if (outcome == SlotMachine.EOutcome.NoWin)
                {
                    float chance = Skills.Pct("lucky_seven");
                    if (Skills.Combo("jackpot_fever") && ActiveManager.IsRunning("hot_streak")) chance += 0.05f;
                    if (chance > 0 && EffectHub.Roll(chance))
                    {
                        outcome = SlotMachine.EOutcome.MiniWin;
                        winAmount = __instance.GetWinAmount(outcome, bet);
                    }
                }
                if (outcome != SlotMachine.EOutcome.NoWin && winAmount > bet)
                {
                    float bonus = WinBonus(Skills.Pct("lucky_charm"));
                    if (bonus > 0) winAmount = bet + Mathf.RoundToInt((winAmount - bet) * (1f + bonus));
                }
                int extra = winAmount - before;
                if (Mod.Tracing) Mod.Debug($"[trace] Slots {shown} -> {outcome}: bet {bet}, win {before} -> {winAmount}, cash {Money.Cash}");

                if (extra > 0)
                    Later(() =>
                    {
                        Money.AddCash(extra);
                        UI.Hud.ShowToast(before == 0 ? $"Lucky Seven: +${extra:N0}" : $"Slots: +${extra:N0} skill bonus", BranchColor);
                    });
                if (winAmount > 0) Launder(winAmount);
                else if (Skills.SynergyActive("comps"))
                {
                    float comp = CapRefund(Mathf.Round(bet * 0.03f));
                    if (comp >= 1) Later(() => Money.AddOnline(comp, "Casino comps", "Hyland Skills"));
                }
            }
        }

        // ================================================================ PEEK
        public static BlackjackGameController ActiveTable()
        {
            foreach (var t in UnityEngine.Object.FindObjectsOfType<BlackjackGameController>())
                if (t != null && t.IsOpen) return t;
            return null;
        }

        public static string HiddenDealerCard(BlackjackGameController table)
        {
            var hand = table?.dealerHand;
            if (hand == null) return null;
            foreach (var card in hand)
            {
                if (card == null || card.IsFaceUp || card.Value == PlayingCard.ECardValue.Blank) continue;
                return $"{card.Value} of {card.Suit}";
            }
            return null;
        }
    }
}
