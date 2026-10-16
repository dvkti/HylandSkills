using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using HarmonyLib;
using HylandSkills.Actives;
using HylandSkills.State;
using Il2CppScheduleOne.Law;
using Il2CppScheduleOne.Noise;
using Il2CppScheduleOne.NPCs.Behaviour;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Police;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.Vehicles;
using UnityEngine;

namespace HylandSkills.Effects
{
    /// <summary>SHADOWS: police attention, body searches, search time, fines, footsteps and getaway driving.</summary>
    public class LawModule : EffectModule
    {
        static readonly TrackedBase baseAttention = new TrackedBase();
        static readonly TrackedBase baseTopSpeed = new TrackedBase();
        static float refreshTimer;
        static LandVehicle boostedVehicle;

        public override void Reset()
        {
            RestoreVehicle();
            baseAttention.Clear();
            baseTopSpeed.Clear();
            searchPass = false;
        }

        /// <summary>How much longer than normal police need to notice the player (2 = twice as long).</summary>
        public static float NoticeSlowdown
        {
            get
            {
                float m = 1f + Skills.Pct("low_profile");
                bool curfew = G.IsCurfew;
                if (curfew) m += Skills.Pct("night_owl");
                if (curfew && Skills.Has("ghost_protocol")) m /= 1f + Skills.Pct2("ghost_protocol");
                if (Skills.Has("notorious")) m /= 1f + Skills.Pct2("notorious");
                return Mathf.Clamp(m, 0.4f, 4f);
            }
        }

        public override void Apply() => ApplyAttention();

        public override void Update(float dt)
        {
            refreshTimer -= dt;
            if (refreshTimer > 0) return;
            refreshTimer = 2f; // officers spawn/despawn and curfew toggles: keep it fresh cheaply
            ApplyAttention();
            UpdateVehicle();
        }

        static void ApplyAttention()
        {
            var list = PoliceOfficer.Officers;
            if (list == null) return;
            float slow = NoticeSlowdown;
            foreach (var o in list)
            {
                var cone = o?.Awareness?.VisionCone;
                if (cone == null) continue;
                float v = baseAttention.Scale(cone.Pointer, cone.Attentiveness, b => b / slow);
                if (!Mathf.Approximately(v, cone.Attentiveness)) cone.Attentiveness = v;
            }
        }

        // ---------------------------------------------------------------- body searches
        // The search is a minigame (BodySearchScreen): the officer scans the hotbar and ItemDetected fails the search
        // on the first visible illegal item. Hidden Pockets rolls once per search; on a pass nothing is detected and
        // the screen closes clean.
        static bool searchPass;

        [HarmonyPatch(typeof(BodySearchBehaviour), nameof(BodySearchBehaviour.Activate))]
        static class SearchStart
        {
            static void Prefix()
            {
                searchPass = Skills.Has("hidden_pockets") && EffectHub.Roll(Skills.Pct("hidden_pockets"));
                if (Mod.Tracing) Mod.Debug($"[trace] BodySearch start: hidden pockets {Skills.Pct("hidden_pockets"):0.###}, pass {searchPass}");
            }
        }

        [HarmonyPatch(typeof(BodySearchBehaviour), nameof(BodySearchBehaviour.DoesPlayerContainItemsOfInterest))]
        static class SearchResult
        {
            static void Postfix(BodySearchBehaviour __instance, ref bool __result)
            {
                if (!__result || !searchPass || __instance == null || __instance.TargetPlayer != Player.Local) return;
                __result = false;
                if (Mod.Tracing) Mod.Debug("[trace] BodySearch items of interest hidden");
            }
        }

        // Nothing to See combo: an officer whose search came up clean ignores you for an hour.
        static readonly System.Collections.Generic.List<(PoliceOfficer officer, int until)> ignoring = new System.Collections.Generic.List<(PoliceOfficer, int)>();

        [HarmonyPatch(typeof(BodySearchBehaviour), nameof(BodySearchBehaviour.ConcludeSearch))]
        static class SearchConcluded
        {
            static void Postfix(BodySearchBehaviour __instance, bool clear)
            {
                if (!clear || __instance == null || __instance.TargetPlayer != Player.Local || !Skills.Combo("nothing_to_see")) return;
                var o = __instance.officer;
                if (o == null) return;
                try { o.SetIgnorePlayers(true); ignoring.Add((o, ActiveManager.GameNow + 60)); }
                catch (System.Exception e) { Mod.Debug("Nothing to See: " + e.Message); }
            }
        }

        public override void MinutePass(int now, int previous)
        {
            for (int i = ignoring.Count - 1; i >= 0; i--)
            {
                if (now < ignoring[i].until) continue;
                try { ignoring[i].officer?.SetIgnorePlayers(false); } catch { }
                ignoring.RemoveAt(i);
            }
        }

        [HarmonyPatch(typeof(BodySearchScreen), nameof(BodySearchScreen.ItemDetected))]
        static class SearchDetect
        {
            static bool Prefix()
            {
                if (Mod.Tracing) Mod.Debug($"[trace] BodySearch item detected, hidden {searchPass}");
                return !searchPass;
            }
        }

        [HarmonyPatch(typeof(BodySearchScreen), nameof(BodySearchScreen.Close))]
        static class SearchClose
        {
            static void Prefix(ref bool clear)
            {
                if (Mod.Tracing) Mod.Debug($"[trace] BodySearch close: clear {clear}, hidden {searchPass}");
                if (searchPass) clear = true;
                searchPass = false;
            }
        }

        static bool BlockSearch(string playerCode)
        {
            if (!Skills.Has("ghost_protocol") || !G.IsDay || Player.Local == null) return false;
            return string.IsNullOrEmpty(playerCode) || playerCode == Player.Local.PlayerCode;
        }

        [HarmonyPatch(typeof(PoliceOfficer), nameof(PoliceOfficer.BeginBodySearch_Networked))]
        static class GhostSearchNet
        {
            static bool Prefix(string playerCode) => !BlockSearch(playerCode);
        }

        [HarmonyPatch(typeof(PoliceOfficer), nameof(PoliceOfficer.BeginBodySearch))]
        static class GhostSearch
        {
            static bool Prefix(string playerCode) => !BlockSearch(playerCode);
        }

        // ---------------------------------------------------------------- pursuit search time
        [HarmonyPatch(typeof(PlayerCrimeData), nameof(PlayerCrimeData.GetSearchTime))]
        static class SearchTime
        {
            static void Postfix(PlayerCrimeData __instance, ref float __result)
            {
                if (__instance == null || __instance.Player != Player.Local) return;
                float m = 1f;
                if (Skills.Has("clean_hands")) m *= 1f - Skills.Pct("clean_hands");
                if (Skills.Has("phantom")) m *= 1f - Skills.Pct("phantom");
                if (Skills.SynergyActive("getaway")) m *= 0.8f;
                if (Skills.Has("wheelman") && Player.Local.IsInVehicle) m *= 0.6f;
                if (Mod.Tracing && !Mathf.Approximately(m, 1f)) Mod.Debug($"[trace] SearchTime: {__result:0.##} x{m:0.###}");
                __result *= m;
            }
        }

        // ---------------------------------------------------------------- fines
        [HarmonyPatch(typeof(PenaltyHandler), nameof(PenaltyHandler.ProcessCrimeList))]
        static class Fines
        {
            static void Prefix(out float __state) => __state = Money.Cash;

            static void Postfix(float __state, Il2CppSystem.Collections.Generic.List<string> __result)
            {
                float paid = __state - Money.Cash;
                Mod.Debug($"Fines processed: cash change {paid:0.##}");
                if (paid <= 0) return;
                float m = 1f;
                if (Skills.Has("clean_hands")) m *= 1f + Skills.Pct2("clean_hands");
                if (Skills.SynergyActive("offshore_accounts")) m *= 0.5f;
                float extra = paid * (m - 1f);
                if (Mathf.Abs(extra) < 1f) return;
                if (extra > 0) Money.TrySpendCash(Mathf.Min(extra, Money.Cash));
                else Money.AddCash(-extra);
                ScaleFineLines(__result, m);
            }
        }

        /// <summary>The offense notice lists "$50.00 fine (paid in cash)": show the amount actually charged.</summary>
        static void ScaleFineLines(Il2CppSystem.Collections.Generic.List<string> lines, float m)
        {
            if (lines == null) return;
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                if (line == null || line.IndexOf("fine", StringComparison.OrdinalIgnoreCase) < 0) continue;
                lines[i] = Regex.Replace(line, @"\$([\d,]+(?:\.\d+)?)", match =>
                {
                    if (!float.TryParse(match.Groups[1].Value.Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return match.Value;
                    return "$" + (v * m).ToString("N2", CultureInfo.InvariantCulture);
                });
            }
        }

        // ---------------------------------------------------------------- footsteps
        [HarmonyPatch(typeof(NoiseUtility), nameof(NoiseUtility.EmitNoise))]
        static class Footsteps
        {
            static void Prefix(ENoiseType type, ref float range, GameObject source)
            {
                if (type != ENoiseType.Footstep || !Skills.Has("light_steps") || Player.Local == null) return;
                if (source == null || source.transform.root != Player.Local.transform.root) return;
                range *= 1f - Skills.Pct("light_steps");
            }
        }

        // ---------------------------------------------------------------- getaway driving
        static void UpdateVehicle()
        {
            LandVehicle current = null;
            if (Skills.Has("wheelman") && Player.Local != null && Player.Local.IsInVehicle && Player.Local.CurrentVehicle != null)
                current = Player.Local.CurrentVehicle.GetComponent<LandVehicle>();
            if (current == boostedVehicle) return;
            RestoreVehicle();
            if (current == null) return;
            current.TopSpeed = baseTopSpeed.Scale(current.Pointer, current.TopSpeed, b => b * (1f + Skills.Pct("wheelman")));
            boostedVehicle = current;
        }

        static void RestoreVehicle()
        {
            if (boostedVehicle != null && baseTopSpeed.TryGetBase(boostedVehicle.Pointer, out var b)) boostedVehicle.TopSpeed = b;
            boostedVehicle = null;
        }

        // ---------------------------------------------------------------- helpers for actives
        public static bool TryVanish(out string reason)
        {
            reason = null;
            var crime = Player.Local?.CrimeData;
            if (crime == null || crime.CurrentPursuitLevel == PlayerCrimeData.EPursuitLevel.None) { reason = "No one is after you"; return false; }
            if (crime.TimeSinceSighted < 2f) { reason = "They can still see you"; return false; }
            crime.TimeoutPursuit();
            return true;
        }
    }
}
