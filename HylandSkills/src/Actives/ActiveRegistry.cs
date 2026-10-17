using HylandSkills.Effects;
using HylandSkills.State;
using Il2CppScheduleOne.Delivery;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.ObjectScripts;
using UnityEngine;

namespace HylandSkills.Actives
{
    public static class ActiveRegistry
    {
        public static void RegisterAll()
        {
            ActiveManager.Register(new Prune());
            ActiveManager.Register(new GrowOverdrive());
            ActiveManager.Register(new LabFocus());
            ActiveManager.Register(new Overclock());
            ActiveManager.Register(new RushDelivery());
            ActiveManager.Register(new CookTheBooks());
            ActiveManager.Register(new PoliceScanner());
            ActiveManager.Register(new Vanish());
            ActiveManager.Register(new HypeText());
            ActiveManager.Register(new RushHour());
            ActiveManager.Register(new Adrenaline());
            ActiveManager.Register(new Intimidate());
            ActiveManager.Register(new HotStreak());
            ActiveManager.Register(new Peek());
            ActiveManager.Register(new SprintBurst());
            ActiveManager.Register(new BoardBoost());
        }

        /// <summary>The property the player stands in, if they own it.</summary>
        internal static bool InOwnedProperty(out Il2CppScheduleOne.Property.Property prop)
        {
            prop = G.PlayerProperty;
            return prop != null && prop.IsOwned;
        }
    }

    // ============================================================ CULTIVATION
    public sealed class Prune : ActiveSkill
    {
        public override string Id => "prune";
        Pot target;

        public override bool CanActivate(out string reason)
        {
            reason = null;
            target = LookedAtPot();
            if (target == null || target.Plant == null) { reason = "Look at a growing plant"; return false; }
            if (target.Plant.IsFullyGrown) { reason = "This plant is already fully grown"; return false; }
            if (GrowingModule.IsPruned(target)) { reason = "This plant was already pruned"; return false; }
            return true;
        }

        public override bool Activate()
        {
            if (target == null) return false;
            GrowingModule.MarkPruned(target);
            return true;
        }

        static Pot LookedAtPot()
        {
            var cam = Camera.main;
            if (cam == null) return null;
            var hits = Physics.RaycastAll(cam.transform.position, cam.transform.forward, 3.5f);
            float best = float.MaxValue;
            Pot found = null;
            foreach (var h in hits)
            {
                if (h.collider == null || h.distance >= best) continue;
                var pot = h.collider.GetComponentInParent<Pot>();
                if (pot == null) continue;
                best = h.distance;
                found = pot;
            }
            return found;
        }
    }

    public sealed class GrowOverdrive : ActiveSkill
    {
        public override string Id => "grow_overdrive";

        public override bool CanActivate(out string reason)
        {
            reason = null;
            if (!ActiveRegistry.InOwnedProperty(out _)) { reason = "Use it inside one of your properties"; return false; }
            return true;
        }

        public override bool Activate()
        {
            if (!ActiveRegistry.InOwnedProperty(out var prop)) return false;
            Skills.Data.EffectProperty["grow_overdrive"] = prop.PropertyCode;
            return true;
        }
    }

    // ============================================================ CHEMISTRY
    public sealed class LabFocus : ActiveSkill
    {
        public override string Id => "lab_focus";
        public override bool Activate() => true;
    }

    public sealed class Overclock : ActiveSkill
    {
        public override string Id => "overclock";

        public override bool CanActivate(out string reason)
        {
            reason = null;
            if (!ActiveRegistry.InOwnedProperty(out _)) { reason = "Use it inside one of your properties"; return false; }
            return true;
        }

        public override bool Activate()
        {
            if (!ActiveRegistry.InOwnedProperty(out var prop)) return false;
            Skills.Data.EffectProperty["overclock"] = prop.PropertyCode;
            return true;
        }

        public override void OnEnd()
        {
            ActiveManager.StartGameWindow(StationModule.BurnoutKey, (int)Def.Duration);
            UI.Hud.ShowToast("Overclock ended: stations are burned out", UI.UiKit.Theme.Warn);
        }
    }

    // ============================================================ EMPIRE
    public sealed class RushDelivery : ActiveSkill
    {
        public override string Id => "rush_delivery";
        DeliveryInstance target;

        public override bool CanActivate(out string reason)
        {
            reason = null;
            target = null;
            int limit = (int)Def.Value;
            if (NetworkSingleton<DeliveryManager>.InstanceExists)
            {
                foreach (var d in NetworkSingleton<DeliveryManager>.Instance.Deliveries)
                {
                    if (d != null && d.Status == EDeliveryStatus.InTransit && d.TimeUntilArrival > limit) { target = d; break; }
                }
            }
            if (target == null) { reason = "No delivery on the way"; return false; }
            if (Money.Cash < Def.Value2) { reason = $"You need ${Def.Value2:0} in cash"; return false; }
            return true;
        }

        public override bool Activate()
        {
            if (target == null || !Money.TrySpendCash(Def.Value2)) return false;
            target.TimeUntilArrival = (int)Def.Value;
            return true;
        }
    }

    public sealed class CookTheBooks : ActiveSkill
    {
        public override string Id => "cook_the_books";

        public override bool CanActivate(out string reason)
        {
            reason = null;
            bool any = false;
            var owned = Il2CppScheduleOne.Property.Business.OwnedBusinesses;
            if (owned != null)
                foreach (var b in owned)
                    if (b?.LaunderingOperations != null && b.LaunderingOperations.Count > 0) { any = true; break; }
            if (!any) { reason = "Nothing is being laundered"; return false; }
            return true;
        }

        // Paper Trail combo (Legit Fronts + Cook the Books): no fee.
        public override bool Activate() => EconomyModule.FinishAllLaundering(Skills.Combo("paper_trail") ? 0f : Def.Value / 100f) > 0;
    }

    // ============================================================ SHADOWS
    public sealed class PoliceScanner : ActiveSkill
    {
        public override string Id => "police_scanner";
        public override bool Activate() => true;

        /// <summary>Night Vision combo: used during curfew, it lasts until curfew ends (06:00).</summary>
        public override float Duration
        {
            get
            {
                if (!Skills.Combo("night_vision") || !G.IsCurfew) return Def.Duration;
                int now = NetworkSingleton<Il2CppScheduleOne.GameTime.TimeManager>.Instance.CurrentTime; // HHMM
                int nowMin = now / 100 * 60 + now % 100;
                int endMin = 6 * 60;
                int left = (endMin - nowMin + 1440) % 1440;
                return Mathf.Max(Def.Duration, left);
            }
        }
    }

    public sealed class Vanish : ActiveSkill
    {
        public override string Id => "vanish";
        public override bool CanActivate(out string reason)
        {
            reason = null;
            var crime = Il2CppScheduleOne.PlayerScripts.Player.Local?.CrimeData;
            if (crime == null || crime.CurrentPursuitLevel == Il2CppScheduleOne.PlayerScripts.PlayerCrimeData.EPursuitLevel.None) { reason = "No one is after you"; return false; }
            if (crime.TimeSinceSighted < 2f) { reason = "They can still see you"; return false; }
            return true;
        }

        public override bool Activate()
        {
            if (!LawModule.TryVanish(out _)) return false;
            if (Skills.Combo("smoke_and_mirrors")) MobilityModule.StartSmoke(10f);
            return true;
        }
    }

    // ============================================================ HUSTLE
    public sealed class HypeText : ActiveSkill
    {
        public override string Id => "hype_text";
        public override bool Activate()
        {
            int n = DealsModule.HypeText((int)Def.Value);
            if (n > 0) UI.Hud.ShowToast($"Hype Text: {n} customer{(n == 1 ? "" : "s")} hit you up", Data.SkillDb.Info(Data.Branch.Hustle).Color);
            if (n == 0) FailReason = "Nobody answered, try again later";
            return n > 0; // nobody answered: the cooldown is not spent
        }

        public override bool CanActivate(out string reason)
        {
            reason = null;
            if (Il2CppScheduleOne.Economy.Customer.UnlockedCustomers == null || Il2CppScheduleOne.Economy.Customer.UnlockedCustomers.Count == 0)
            {
                reason = "You don't have any customers yet";
                return false;
            }
            if (DealsModule.HypeCandidates().Count == 0)
            {
                reason = "No free customer wants what you sell (list products in the Products app)";
                return false;
            }
            return true;
        }
    }

    public sealed class RushHour : ActiveSkill
    {
        public override string Id => "rush_hour";
        public override bool Activate() => true;
    }

    // ============================================================ MUSCLE
    /// <summary>Numbers live in PlayerModule (damage taken, move speed).</summary>
    public sealed class Adrenaline : ActiveSkill
    {
        public override string Id => "adrenaline";
        public override bool Activate()
        {
            // Rush combo: Adrenaline + Second Wind heals 30% of missing health.
            var h = Il2CppScheduleOne.PlayerScripts.Player.Local?.Health;
            if (Skills.Combo("rush") && h != null && h.IsAlive)
            {
                float missing = Il2CppScheduleOne.PlayerScripts.Health.PlayerHealth.MaxHealth - h.CurrentHealth;
                if (missing > 1f) h.RecoverHealth(missing * 0.3f);
            }
            return true;
        }
    }

    public sealed class Intimidate : ActiveSkill
    {
        public override string Id => "intimidate";
        public override bool Activate()
        {
            int n = CombatModule.Intimidate(Def.Value, Def.Value2);
            if (n == 0) return false;
            UI.Hud.ShowToast($"Intimidate: {n} goon{(n == 1 ? "" : "s")} running", Data.SkillDb.Info(Data.Branch.Muscle).Color);
            return true;
        }

        public override bool CanActivate(out string reason)
        {
            reason = null;
            var p = Il2CppScheduleOne.PlayerScripts.Player.Local;
            if (p == null) return false;
            foreach (var g in Object.FindObjectsOfType<Il2CppScheduleOne.Cartel.CartelGoon>())
                if (g != null && g.IsConscious && Vector3.Distance(g.transform.position, p.transform.position) <= Def.Value) return true;
            reason = "No cartel goons nearby";
            return false;
        }
    }
}

namespace HylandSkills.Actives
{
    // ============================================================ GAMBLING
    public sealed class HotStreak : ActiveSkill
    {
        public override string Id => "hot_streak";
        public override bool Activate() => true;
    }

    public sealed class Peek : ActiveSkill
    {
        public override string Id => "peek";
        string card;

        /// <summary>Read the Table combo: recharges twice as fast.</summary>
        public override float Cooldown => Skills.Combo("read_the_table") ? Def.Cooldown / 2f : Def.Cooldown;

        public override bool CanActivate(out string reason)
        {
            reason = null;
            var table = GamblingModule.ActiveTable();
            if (table == null) { reason = "Sit at a blackjack table first"; return false; }
            card = GamblingModule.HiddenDealerCard(table);
            if (card == null) { reason = "The dealer has no hidden card right now"; return false; }
            return true;
        }

        public override bool Activate()
        {
            if (card == null) return false;
            UI.Hud.ShowToast($"Dealer's hidden card: {card}", Data.SkillDb.Info(Data.Branch.Gambling).Color);
            return true;
        }
    }

    // ============================================================ MOBILITY
    public sealed class SprintBurst : ActiveSkill
    {
        public override string Id => "sprint_burst";
        public override bool Activate() => true;

        /// <summary>The burst also keeps stamina full, so it can be spent sprinting.</summary>
        public override void Tick()
        {
            if (!PlayerSingleton<Il2CppScheduleOne.PlayerScripts.PlayerMovement>.InstanceExists) return;
            var pm = PlayerSingleton<Il2CppScheduleOne.PlayerScripts.PlayerMovement>.Instance;
            float max = Il2CppScheduleOne.PlayerScripts.PlayerMovement.StaminaReserveMax;
            if (pm != null && pm.CurrentStaminaReserve < max) pm.SetStamina(max, false);
        }
    }

    public sealed class BoardBoost : ActiveSkill
    {
        public override string Id => "board_boost";

        public override bool CanActivate(out string reason)
        {
            reason = null;
            if (!MobilityModule.IsSkating) { reason = "Get on your skateboard first"; return false; }
            return true;
        }

        public override bool Activate() => MobilityModule.Boost(Def.Value);
    }
}
