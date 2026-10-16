using System;
using System.Collections.Generic;
using System.Linq;
using HylandSkills.Data;
using S1API.Internal.Abstraction;
using S1API.Saveables;

namespace HylandSkills.State
{
    /// <summary>Everything the mod persists, stored inside the game's own save folder through S1API.</summary>
    public class SkillSaveData
    {
        public int Version = 1;
        public Dictionary<string, int> Ranks = new Dictionary<string, int>();
        public string[] Loadout = new string[ActiveLoadout.Slots];
        /// <summary>Game-clock cooldowns: absolute game minute when the skill is ready again.</summary>
        public Dictionary<string, int> GameCooldownUntil = new Dictionary<string, int>();
        /// <summary>Real-time cooldowns: seconds remaining at the moment of saving.</summary>
        public Dictionary<string, float> RealCooldownLeft = new Dictionary<string, float>();
        /// <summary>Running game-clock effects: absolute game minute when they end.</summary>
        public Dictionary<string, int> GameEffectUntil = new Dictionary<string, int>();
        /// <summary>GUIDs of pots whose current plant was pruned (quality bonus at harvest).</summary>
        public List<string> PrunedPots = new List<string>();
        /// <summary>Property code where the current Overdrive / Overclock window applies.</summary>
        public Dictionary<string, string> EffectProperty = new Dictionary<string, string>();
        public int Respecs;
        public int LastKnownEarnedPoints = -1;
    }

    public static class ActiveLoadout
    {
        public const int Slots = 4;
    }

    /// <summary>S1API discovers this class automatically and calls Save/Load with the game's save cycle.</summary>
    public class SkillSaveable : Saveable
    {
        [SaveableField("HylandSkills")]
        public SkillSaveData Data = new SkillSaveData();

        public static SkillSaveable Instance { get; private set; }

        public SkillSaveable()
        {
            Instance = this;
            Data = Skills.Data; // share the live object so a brand-new game saves what the player learned
        }

        protected override void OnLoaded()
        {
            Instance = this;
            Data ??= new SkillSaveData();
            Data.Ranks ??= new Dictionary<string, int>();
            Data.GameCooldownUntil ??= new Dictionary<string, int>();
            Data.RealCooldownLeft ??= new Dictionary<string, float>();
            Data.GameEffectUntil ??= new Dictionary<string, int>();
            Data.PrunedPots ??= new List<string>();
            Data.EffectProperty ??= new Dictionary<string, string>();
            if (Data.Loadout == null || Data.Loadout.Length != ActiveLoadout.Slots)
            {
                var l = new string[ActiveLoadout.Slots];
                if (Data.Loadout != null) Array.Copy(Data.Loadout, l, Math.Min(l.Length, Data.Loadout.Length));
                Data.Loadout = l;
            }
            Skills.OnStateLoaded(Data);
        }

        protected override void OnSaved()
        {
        }
    }

    /// <summary>Static facade over the current save: ranks, points and unlock rules.</summary>
    public static class Skills
    {
        public static SkillSaveData Data { get; private set; } = new SkillSaveData();
        public static bool Loaded { get; private set; }

        public static event Action Changed;
        public static event Action StateLoaded;

        internal static void OnStateLoaded(SkillSaveData data)
        {
            Data = data;
            Loaded = true;
            Sanitize();
            Effects.EffectHub.Recalculate();
            StateLoaded?.Invoke();
            Changed?.Invoke();
        }

        /// <summary>
        /// Called when the main menu loads. A save without our file never triggers OnLoaded with new data,
        /// so the previous session's data has to be wiped here, before the next save is loaded.
        /// </summary>
        internal static void ResetForNewSession()
        {
            var fresh = new SkillSaveData();
            if (SkillSaveable.Instance != null) SkillSaveable.Instance.Data = fresh;
            Data = fresh;
            Loaded = false;
        }

        /// <summary>A brand-new game never loads our file; treat the world becoming ready as "loaded".</summary>
        internal static void EnsureLoaded()
        {
            if (Loaded) return;
            if (SkillSaveable.Instance != null) SkillSaveable.Instance.Data = Data;
            OnStateLoaded(Data);
        }

        public static void NotifyChanged()
        {
            Effects.EffectHub.Recalculate();
            Changed?.Invoke();
        }

        // ---------- queries ----------
        public static int Rank(string id) => Data.Ranks.TryGetValue(id, out var r) ? r : 0;
        public static bool Has(string id) => Rank(id) > 0;

        /// <summary>Rank × per-rank value. 0 when not learned.</summary>
        public static float Val(string id)
        {
            var d = SkillDb.Get(id);
            return d == null ? 0f : d.Value * Rank(id);
        }

        public static float Val2(string id)
        {
            var d = SkillDb.Get(id);
            return d == null || !Has(id) ? 0f : d.Value2;
        }

        /// <summary>Percent value as a 0-1 fraction.</summary>
        public static float Pct(string id) => Val(id) / 100f;
        public static float Pct2(string id) => Val2(id) / 100f;

        public static int SpentInBranch(Branch b)
        {
            int sum = 0;
            foreach (var kv in Data.Ranks)
            {
                var d = SkillDb.Get(kv.Key);
                if (d != null && d.Branch == b && !d.IsMastery) sum += kv.Value * d.CostPerRank;
            }
            return sum;
        }

        public static int SpentTotal()
        {
            int sum = 0;
            foreach (var kv in Data.Ranks)
            {
                var d = SkillDb.Get(kv.Key);
                if (d != null && !d.IsMastery) sum += kv.Value * d.CostPerRank;
            }
            return sum;
        }

        public static int MasteriesTaken() => Data.Ranks.Count(kv => kv.Value > 0 && SkillDb.Get(kv.Key)?.IsMastery == true);

        public static int EarnedPoints => Progression.EarnedSkillPoints;
        public static int AvailablePoints => Math.Max(0, EarnedPoints - SpentTotal());
        public static int EarnedMasteryPoints => Progression.EarnedMasteryPoints;
        public static int AvailableMasteryPoints => Math.Max(0, EarnedMasteryPoints - MasteriesTaken());

        public static bool SynergyActive(string synergyId)
        {
            var s = SkillDb.Synergies.FirstOrDefault(x => x.Id == synergyId);
            return s != null && SynergyActive(s);
        }

        public static bool SynergyActive(SynergyDef s) =>
            SpentInBranch(s.A) >= SkillDb.SynergyRequirement && SpentInBranch(s.B) >= SkillDb.SynergyRequirement;

        public static int TierRequirement(int tier) => SkillDb.TierGate[Math.Clamp(tier, 0, SkillDb.TierGate.Length - 1)];

        /// <summary>True when both skills of a combo are learned.</summary>
        public static bool Combo(string comboId)
        {
            var c = SkillDb.Combos.FirstOrDefault(x => x.Id == comboId);
            return c != null && Has(c.A) && Has(c.B);
        }

        public static bool ParentsMet(SkillDef d) => d.Parents.All(Has);

        public enum LockReason { None, Maxed, NoPoints, TierLocked, ParentMissing, NoMasteryPoint, MasteryTaken, BranchIncomplete }

        public static LockReason CanLearn(SkillDef d)
        {
            int rank = Rank(d.Id);
            if (rank >= d.MaxRank) return LockReason.Maxed;
            int spent = SpentInBranch(d.Branch);
            if (d.IsMastery)
            {
                if (spent < SkillDb.PointsPerBranch) return LockReason.BranchIncomplete;
                if (SkillDb.All.Any(o => o.Branch == d.Branch && o.IsMastery && Has(o.Id))) return LockReason.MasteryTaken;
                if (AvailableMasteryPoints <= 0) return LockReason.NoMasteryPoint;
                return LockReason.None;
            }
            if (!ParentsMet(d)) return LockReason.ParentMissing;
            if (spent < TierRequirement(d.Tier)) return LockReason.TierLocked;
            if (AvailablePoints < d.CostPerRank) return LockReason.NoPoints;
            return LockReason.None;
        }

        public static bool TryLearn(SkillDef d)
        {
            if (CanLearn(d) != LockReason.None) return false;
            Data.Ranks[d.Id] = Rank(d.Id) + 1;
            if (d.IsActive) Actives.ActiveManager.AutoEquip(d.Id);
            NotifyChanged();
            return true;
        }

        /// <summary>Most a reset can ever cost.</summary>
        public const int RespecCap = 75000;

        /// <summary>
        /// Paid from the bank: $5,000, $15,000, $30,000, $50,000, then $75,000 for every reset after that.
        /// Each one hurts more, so skills are worth reading before learning, but it never grows without limit.
        /// </summary>
        public static int RespecCost(int previousResets) => Math.Min(RespecCap, 5000 * (previousResets + 1) * (previousResets + 2) / 2);
        public static int RespecCost() => RespecCost(Data.Respecs);

        public static void Respec()
        {
            Data.Ranks.Clear();
            Data.Loadout = new string[ActiveLoadout.Slots];
            Data.GameEffectUntil.Clear();
            Data.Respecs++;
            Actives.ActiveManager.CancelAll();
            NotifyChanged();
        }

        /// <summary>Drops data that no longer matches the current skill table (renamed skills, over-spent points).</summary>
        static void Sanitize()
        {
            foreach (var key in Data.Ranks.Keys.ToList())
            {
                var d = SkillDb.Get(key);
                if (d == null || Data.Ranks[key] <= 0) { Data.Ranks.Remove(key); continue; }
                if (Data.Ranks[key] > d.MaxRank) Data.Ranks[key] = d.MaxRank;
            }
            // one Major Mastery per tree: keep the first one found if a save somehow has more
            foreach (var group in Data.Ranks.Keys.Select(SkillDb.Get).Where(d => d != null && d.IsMastery).GroupBy(d => d.Branch).ToList())
                foreach (var extra in group.Skip(1)) Data.Ranks.Remove(extra.Id);
            for (int i = 0; i < Data.Loadout.Length; i++)
            {
                var id = Data.Loadout[i];
                if (id != null && (SkillDb.Get(id)?.IsActive != true || !Has(id))) Data.Loadout[i] = null;
            }
        }
    }
}
