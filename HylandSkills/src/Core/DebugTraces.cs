using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HylandSkills.Effects;
using Il2CppScheduleOne.Vision;
using UnityEngine;

namespace HylandSkills.Core
{
    /// <summary>Debug-only measurements of game behaviour the skills change (logged with DebugLogging = true).</summary>
    static class DebugTraces
    {
        static readonly Dictionary<IntPtr, float> halfAt = new Dictionary<IntPtr, float>();

        static bool Local(VisionEvent e)
        {
            try { return e != null && e.Target != null && G.IsLocalPlayer(e.Target.NetworkObject); } catch { return false; }
        }

        /// <summary>Police notice speed: time from half to full notice for events on the local player.</summary>
        [HarmonyPatch(typeof(VisionCone), nameof(VisionCone.EventHalfNoticed))]
        static class Half
        {
            static void Prefix(VisionCone __instance, VisionEvent _event)
            {
                if (!Prefs.DebugLogging.Value || !Local(_event)) return;
                halfAt[_event.Pointer] = Time.time;
                if (Mod.Tracing) Mod.Debug($"[trace] Vision half: {__instance.name} attentiveness {__instance.Attentiveness:0.###}, full notice time {_event.FullNoticeTime:0.###}, state {_event.State?.state}");
            }
        }

        [HarmonyPatch(typeof(VisionCone), nameof(VisionCone.EventFullyNoticed))]
        static class Full
        {
            static void Prefix(VisionCone __instance, VisionEvent _event)
            {
                if (!Prefs.DebugLogging.Value || !Local(_event)) return;
                string dt = halfAt.TryGetValue(_event.Pointer, out var t0) ? (Time.time - t0).ToString("0.###") : "?";
                halfAt.Remove(_event.Pointer);
                if (Mod.Tracing) Mod.Debug($"[trace] Vision full: {__instance.name} attentiveness {__instance.Attentiveness:0.###}, half->full {dt}s, full notice time {_event.FullNoticeTime:0.###}");
            }
        }

        /// <summary>Body search flow: which steps run and with what verdict.</summary>
        [HarmonyPatch]
        static class SearchFlow
        {
            static IEnumerable<MethodBase> TargetMethods()
            {
                var b = typeof(Il2CppScheduleOne.NPCs.Behaviour.BodySearchBehaviour);
                foreach (var n in new[] { "ConcludeSearch", "SearchClean", "SearchFail", "NoItemsOfInterestFound", "Escalate", "DoesPlayerContainItemsOfInterest" })
                {
                    var m = AccessTools.Method(b, n);
                    if (m != null) yield return m;
                }
                var sc = typeof(Il2CppScheduleOne.UI.BodySearchScreen);
                foreach (var n in new[] { "Open", "ItemDetected" })
                {
                    var m = AccessTools.Method(sc, n);
                    if (m != null) yield return m;
                }
            }

            static void Postfix(MethodBase __originalMethod)
            {
                if (Prefs.DebugLogging.Value) Mod.Debug($"[trace] Search step {__originalMethod.DeclaringType.Name}.{__originalMethod.Name}");
            }
        }
    }
}
