using System;
using System.Collections.Generic;

namespace DominoCommon.com.domino.common.Scripts.Enums
{
    /// <summary>
    /// 7-tier Chess-style Domino Elo rank ladder for Roatan Domino.
    /// Elo starts at 1000 (Sailor) and is floored at 100 (Cobbler).
    /// </summary>
    public enum RoatanRankTier
    {
        Cobbler = 0,
        Deckhand = 1,
        Sailor = 2,
        Navigator = 3,
        ReefMaster = 4,
        IslandChampion = 5,
        GrandSlamLegend = 6
    }

    public sealed class RoatanRankTierInfo
    {
        public RoatanRankTier Tier { get; }
        public string DisplayName { get; }
        public string BadgeColorHex { get; }
        public int MinElo { get; }
        public int? MaxElo { get; }

        public RoatanRankTierInfo(RoatanRankTier tier, string displayName, string badgeColorHex, int minElo, int? maxElo)
        {
            Tier = tier;
            DisplayName = displayName;
            BadgeColorHex = badgeColorHex;
            MinElo = minElo;
            MaxElo = maxElo;
        }
    }

    public static class RoatanRankTierHelper
    {
        public const int DefaultElo = 1000;
        public const int MinElo = 100;

        private static readonly RoatanRankTierInfo[] Tiers =
        {
            new RoatanRankTierInfo(RoatanRankTier.Cobbler, "Cobbler", "#A39081", 100, 499),
            new RoatanRankTierInfo(RoatanRankTier.Deckhand, "Deckhand", "#CD7F32", 500, 899),
            new RoatanRankTierInfo(RoatanRankTier.Sailor, "Sailor", "#4ECDC4", 900, 1199),
            new RoatanRankTierInfo(RoatanRankTier.Navigator, "Navigator", "#38BDF8", 1200, 1499),
            new RoatanRankTierInfo(RoatanRankTier.ReefMaster, "Reef Master", "#C084FC", 1500, 1799),
            new RoatanRankTierInfo(RoatanRankTier.IslandChampion, "Island Champion", "#F7C948", 1800, 2099),
            new RoatanRankTierInfo(RoatanRankTier.GrandSlamLegend, "Grand Slam Legend", "#FF5E3A", 2100, null)
        };

        public static IReadOnlyList<RoatanRankTierInfo> AllTiers => Tiers;

        public static int ClampElo(int elo)
        {
            return elo < MinElo ? MinElo : elo;
        }

        public static RoatanRankTier FromElo(int elo)
        {
            int clamped = ClampElo(elo);
            if (clamped < 500) return RoatanRankTier.Cobbler;
            if (clamped < 900) return RoatanRankTier.Deckhand;
            if (clamped < 1200) return RoatanRankTier.Sailor;
            if (clamped < 1500) return RoatanRankTier.Navigator;
            if (clamped < 1800) return RoatanRankTier.ReefMaster;
            if (clamped < 2100) return RoatanRankTier.IslandChampion;
            return RoatanRankTier.GrandSlamLegend;
        }

        public static RoatanRankTierInfo GetInfo(RoatanRankTier tier)
        {
            int idx = (int)tier;
            if (idx < 0 || idx >= Tiers.Length)
            {
                return Tiers[(int)RoatanRankTier.Sailor];
            }
            return Tiers[idx];
        }

        public static RoatanRankTierInfo GetInfoForElo(int elo)
        {
            return GetInfo(FromElo(elo));
        }

        public static string GetDisplayName(this RoatanRankTier tier)
        {
            return GetInfo(tier).DisplayName;
        }

        public static string GetBadgeColorHex(this RoatanRankTier tier)
        {
            return GetInfo(tier).BadgeColorHex;
        }

        public static string FormatPlainBadge(int elo)
        {
            int clamped = ClampElo(elo);
            RoatanRankTierInfo info = GetInfoForElo(clamped);
            return $"[{info.DisplayName}] \u2022 {clamped}";
        }

        public static string FormatRichBadge(int elo)
        {
            int clamped = ClampElo(elo);
            RoatanRankTierInfo info = GetInfoForElo(clamped);
            return $"<color={info.BadgeColorHex}>[{info.DisplayName}]</color> \u2022 {clamped}";
        }

        public static double CalculateAccuracyPercent(int optimalMoves, int totalMoves)
        {
            if (totalMoves <= 0) return 0.0;
            int clampedOptimal = optimalMoves < 0 ? 0 : (optimalMoves > totalMoves ? totalMoves : optimalMoves);
            return Math.Round((double)clampedOptimal * 100.0 / totalMoves, 1);
        }
    }
}
