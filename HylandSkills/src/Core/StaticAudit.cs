using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.Law;
using Il2CppScheduleOne.Levelling;
using Il2CppScheduleOne.Money;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Police;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.PlayerScripts.Health;

namespace HylandSkills
{
    /// <summary>Debug report: which of the static tuning values the mod relies on are real statics vs compiled constants.</summary>
    public static class StaticAudit
    {
        public static void Run()
        {
            void Check<T>(string f) => CheckT(typeof(T), f);
            void CheckT(System.Type t, string f) => Mod.Debug($"static {t.Name}.{f}: {(Il2CppStatics.IsWritable(t, f) ? "writable" : "CONST")}");
            Check<PlayerHealth>("MaxHealth");
            Check<PlayerHealth>("HealthRecoveryPerMinute");
            Check<ATM>("WeeklyDepositLimit");
            Check<ATM>("WeeklyDepositSum");
            Check<Dealer>("MAX_CUSTOMERS");
            Check<Cauldron>("COCA_LEAF_REQUIRED");
            Check<DryingRack>("DRY_MINS_PER_TIER");
            Check<PlayerCrimeData>("SEARCH_TIME_INVESTIGATING");
            Check<PlayerCrimeData>("SEARCH_TIME_ARRESTING");
            Check<PoliceOfficer>("BODY_SEARCH_CHANCE_DEFAULT");
            CheckT(typeof(PenaltyHandler), "CONTROLLED_SUBSTANCE_FINE");
            Check<Customer>("ADDICTION_DRAIN_PER_DAY");
            Check<Customer>("DEAL_REJECTED_RELATIONSHIP_CHANGE");
            Check<Customer>("APPROACH_CHANCE_PER_DAY_MAX");
            Check<LevelManager>("TIERS_PER_RANK");
            Check<CurfewManager>("CURFEW_START_TIME");
        }
    }
}
