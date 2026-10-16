using System;
using HylandSkills.Data;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Levelling;
using MelonLoader;

namespace HylandSkills.State
{
    /// <summary>
    /// Converts the game's own rank (11 ranks × 5 tiers) into skill points.
    /// Street Rat I is the starting tier, so Kingpin I is the 50th tier-up and the last one that pays points.
    /// Each tier-up pays <see cref="PointsPerTierUp"/> points: 100 in total, enough to complete half of the
    /// eight trees (4 × 24) with a few points left to dip into a fifth.
    /// </summary>
    public static class Progression
    {
        public const int PointsPerTierUp = 2;

        /// <summary>Tier-ups that grant a mastery point: Bagman I, Shot Caller I, Underlord I and Kingpin I.
        /// Each one lands shortly after a full tree becomes affordable (24, 48, 72 and 96 points).</summary>
        public static readonly int[] MasteryTierUps = { 20, 30, 40, 50 };
        public static int MasteryCount => MasteryTierUps.Length;

        public static int TierUps { get; private set; }
        /// <summary>Developer testing only (F10 with debug logging on), in skill points; never saved.</summary>
        public static int DebugBonus;
        public static int EarnedSkillPoints => Math.Min(SkillDb.TotalSkillPoints, TierUps * PointsPerTierUp + DebugBonus);
        public static int EarnedMasteryPoints => MasteryFor(TierUps + DebugBonus / PointsPerTierUp);

        public static event Action<int, int> PointsEarned; // (skill points gained, mastery points gained)

        static int MasteryFor(int tierUps)
        {
            int n = 0;
            foreach (var t in MasteryTierUps) if (tierUps >= t) n++;
            return n;
        }

        /// <summary>"Bagman I, Shot Caller I, Underlord I and Kingpin I".</summary>
        public static string MasteryRanksLabel()
        {
            var names = new string[MasteryTierUps.Length];
            for (int i = 0; i < names.Length; i++) names[i] = TierUpLabel(MasteryTierUps[i]);
            return names.Length == 1 ? names[0] : string.Join(", ", names, 0, names.Length - 1) + " and " + names[names.Length - 1];
        }

        public static int TiersPerRank
        {
            get
            {
                try { return LevelManager.TIERS_PER_RANK; } catch { return 5; }
            }
        }

        /// <summary>Tier-ups needed from Street Rat I to reach the given rank (tier is 1-based).</summary>
        public static int TierUpsFor(ERank rank, int tier) => (int)rank * TiersPerRank + Math.Max(0, tier - 1);

        public static string NextPointRankLabel()
        {
            if (EarnedSkillPoints >= SkillDb.TotalSkillPoints) return null;
            return TierUpLabel(TierUps + 1);
        }

        static string TierUpLabel(int tierUps)
        {
            int tpr = TiersPerRank;
            return RankName((ERank)(tierUps / tpr)) + " " + Roman(tierUps % tpr + 1);
        }

        public static string RankName(ERank r) => r.ToString().Replace('_', ' ');

        public static string Roman(int n) => n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", _ => n.ToString() };

        /// <summary>Polled from the mod's update loop; cheap and robust against missed rank-up events.</summary>
        public static void Refresh(bool notify)
        {
            if (!NetworkSingleton<LevelManager>.InstanceExists) return;
            var lm = NetworkSingleton<LevelManager>.Instance;
            int ups = TierUpsFor(lm.Rank, lm.Tier);
            if (ups == TierUps && Skills.Data.LastKnownEarnedPoints >= 0) return;

            TierUps = ups;

            int last = Skills.Data.LastKnownEarnedPoints;
            Skills.Data.LastKnownEarnedPoints = EarnedSkillPoints;
            if (notify && last >= 0 && EarnedSkillPoints > last)
            {
                PointsEarned?.Invoke(EarnedSkillPoints - last, EarnedMasteryPoints - MasteryFor(last / PointsPerTierUp));
            }
            Skills.NotifyChanged();
        }

        public static void ResetSession()
        {
            TierUps = 0;
            DebugBonus = 0;
        }
    }
}
