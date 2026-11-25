using DominoCommon.com.domino.common.Scripts.Enums;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;

namespace Domain.Game;

public sealed class EloPlayerMatchInput
{
    public long PlayerId { get; init; }
    public string Username { get; init; } = string.Empty;
    public int CurrentElo { get; init; } = RoatanRankTierHelper.DefaultElo;
    public bool IsHuman { get; init; } = true;
    public int TeamId { get; init; }
    public bool IsWinner { get; init; }
    public bool IsDraw { get; init; }
    public bool IsKeyWin { get; init; }
    public bool IsGameBrukWin { get; init; }
    public int WinAssists { get; init; }
    public int TotalMoves { get; init; }
    public int OptimalMoves { get; init; }
    public int ForcedPasses { get; init; }
    public int TrappedDoubles { get; init; }
    public double AverageTurnSeconds { get; init; }
}

public sealed class EloMatchContext
{
    public int GameId { get; init; }
    public bool IsOnlineMatch { get; init; } = true;
    public bool IsTeam2v2 { get; init; }
    public List<EloPlayerMatchInput> Players { get; init; } = [];
}

/// <summary>
/// Online-only Chess-style Elo rating calculator for Roatan Domino.
/// Base Chess expected-score delta (K = 32, minimum floor 100 Elo)
/// + +6 Elo bonus for Key / Capicu or Game Bruk wins
/// + performance modifier (-4 to +8 Elo) from Win Assists, Playing Accuracy, and Playing Speed (< 6s).
/// </summary>
public static class RoatanEloCalculator
{
    public const int KFactor = 32;
    public const int DefaultElo = RoatanRankTierHelper.DefaultElo;
    public const int MinElo = RoatanRankTierHelper.MinElo;
    public const int SpecialWinBonus = 6;
    public const int MinPerformanceModifier = -4;
    public const int MaxPerformanceModifier = 8;
    public const double FastSpeedThresholdSeconds = 6.0;
    public const double BlazingSpeedThresholdSeconds = 3.5;
    public const double SlowSpeedThresholdSeconds = 9.0;

    public static double CalculateExpectedScore(int playerElo, double opponentAverageElo)
    {
        double clampedPlayer = RoatanRankTierHelper.ClampElo(playerElo);
        double clampedOpponent = Math.Max(MinElo, opponentAverageElo);
        return 1.0 / (1.0 + Math.Pow(10.0, (clampedOpponent - clampedPlayer) / 400.0));
    }

    /// <summary>
    /// Computes the clamped performance modifier (-4 to +8 Elo) from:
    /// - Win Assists (partner setup plays in Six-Lov / 2v2): up to +3
    /// - Playing Accuracy (optimal moves vs. forced passes &amp; trapped doubles): -5 to +3
    /// - Playing Speed (avg turn time under 6s): -1 to +2
    /// </summary>
    public static int CalculatePerformanceModifier(
        int winAssists,
        int optimalMoves,
        int totalMoves,
        int forcedPasses,
        int trappedDoubles,
        double averageTurnSeconds)
    {
        int assistBonus = Math.Clamp(winAssists, 0, 3);

        int accuracyScore = 0;
        if (totalMoves > 0)
        {
            double accuracyPercent = RoatanRankTierHelper.CalculateAccuracyPercent(optimalMoves, totalMoves);
            if (accuracyPercent >= 80.0) accuracyScore = 3;
            else if (accuracyPercent >= 65.0) accuracyScore = 2;
            else if (accuracyPercent >= 50.0) accuracyScore = 1;
            else if (accuracyPercent < 35.0) accuracyScore = -2;
            else accuracyScore = -1;
        }

        int trappedPenalty = Math.Min(2, Math.Max(0, trappedDoubles));
        int forcedPassPenalty = forcedPasses >= 3 ? 1 : 0;

        int speedScore = 0;
        if (averageTurnSeconds > 0.0)
        {
            if (averageTurnSeconds < BlazingSpeedThresholdSeconds) speedScore = 2;
            else if (averageTurnSeconds < FastSpeedThresholdSeconds) speedScore = 1;
            else if (averageTurnSeconds >= SlowSpeedThresholdSeconds) speedScore = -1;
        }

        int raw = assistBonus + accuracyScore - trappedPenalty - forcedPassPenalty + speedScore;
        return Math.Clamp(raw, MinPerformanceModifier, MaxPerformanceModifier);
    }

    public static MatchEloBreakdownDto CalculateMatchRatings(EloMatchContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        List<EloPlayerMatchInput> humans = context.Players
            .Where(p => p.IsHuman && p.PlayerId > 0)
            .ToList();

        // Online-only rule: strictly require an online match with >= 2 real human players.
        bool isEligible = context.IsOnlineMatch && humans.Count >= 2;

        MatchEloBreakdownDto result = new()
        {
            GameId = context.GameId,
            IsRankedMatch = isEligible,
            Players = []
        };

        if (!isEligible)
        {
            foreach (EloPlayerMatchInput human in humans)
            {
                int currentElo = RoatanRankTierHelper.ClampElo(human.CurrentElo);
                RoatanRankTierInfo tierInfo = RoatanRankTierHelper.GetInfoForElo(currentElo);
                result.Players.Add(new PlayerEloBreakdownDto
                {
                    PlayerId = human.PlayerId,
                    Username = human.Username,
                    PreviousElo = currentElo,
                    NewElo = currentElo,
                    Delta = 0,
                    PreviousTier = tierInfo.Tier,
                    NewTier = tierInfo.Tier,
                    TierName = tierInfo.DisplayName,
                    TierColorHex = tierInfo.BadgeColorHex,
                    ResultLabel = "Unranked",
                    BaseEloDelta = 0,
                    SpecialWinBonus = 0,
                    PerformanceModifier = 0,
                    WinAssists = human.WinAssists,
                    AccuracyPercent = RoatanRankTierHelper.CalculateAccuracyPercent(human.OptimalMoves, human.TotalMoves),
                    AverageTurnSeconds = Math.Round(human.AverageTurnSeconds, 2),
                    TotalMoves = human.TotalMoves,
                    OptimalMoves = human.OptimalMoves,
                    IsRankedApplied = false
                });
            }

            return result;
        }

        foreach (EloPlayerMatchInput player in humans)
        {
            int previousElo = RoatanRankTierHelper.ClampElo(player.CurrentElo);
            List<EloPlayerMatchInput> opponents = context.IsTeam2v2
                ? context.Players.Where(p => p.TeamId != player.TeamId).ToList()
                : context.Players.Where(p => p.PlayerId != player.PlayerId).ToList();

            double opponentAvgElo = opponents.Count > 0
                ? opponents.Average(o => (double)RoatanRankTierHelper.ClampElo(o.CurrentElo))
                : DefaultElo;

            double expectedScore = CalculateExpectedScore(previousElo, opponentAvgElo);
            double actualScore = player.IsDraw ? 0.5 : (player.IsWinner ? 1.0 : 0.0);
            int baseDelta = (int)Math.Round(KFactor * (actualScore - expectedScore), MidpointRounding.AwayFromZero);

            bool earnedSpecialWin = player.IsWinner && (player.IsKeyWin || player.IsGameBrukWin);
            int specialBonus = earnedSpecialWin ? SpecialWinBonus : 0;

            int perfModifier = CalculatePerformanceModifier(
                player.WinAssists,
                player.OptimalMoves,
                player.TotalMoves,
                player.ForcedPasses,
                player.TrappedDoubles,
                player.AverageTurnSeconds);

            int rawNewElo = previousElo + baseDelta + specialBonus + perfModifier;
            int newElo = Math.Max(MinElo, rawNewElo);
            int finalDelta = newElo - previousElo;

            RoatanRankTier prevTier = RoatanRankTierHelper.FromElo(previousElo);
            RoatanRankTierInfo newTierInfo = RoatanRankTierHelper.GetInfoForElo(newElo);

            string resultLabel;
            if (player.IsDraw)
            {
                resultLabel = "Draw";
            }
            else if (player.IsWinner)
            {
                if (player.IsKeyWin) resultLabel = "Key / Capicu Win";
                else if (player.IsGameBrukWin) resultLabel = "Game Bruk Win";
                else resultLabel = "Win";
            }
            else
            {
                resultLabel = "Loss";
            }

            result.Players.Add(new PlayerEloBreakdownDto
            {
                PlayerId = player.PlayerId,
                Username = player.Username,
                PreviousElo = previousElo,
                NewElo = newElo,
                Delta = finalDelta,
                PreviousTier = prevTier,
                NewTier = newTierInfo.Tier,
                TierName = newTierInfo.DisplayName,
                TierColorHex = newTierInfo.BadgeColorHex,
                ResultLabel = resultLabel,
                BaseEloDelta = baseDelta,
                SpecialWinBonus = specialBonus,
                PerformanceModifier = perfModifier,
                WinAssists = Math.Max(0, player.WinAssists),
                AccuracyPercent = RoatanRankTierHelper.CalculateAccuracyPercent(player.OptimalMoves, player.TotalMoves),
                AverageTurnSeconds = Math.Round(Math.Max(0.0, player.AverageTurnSeconds), 2),
                TotalMoves = Math.Max(0, player.TotalMoves),
                OptimalMoves = Math.Clamp(player.OptimalMoves, 0, Math.Max(0, player.TotalMoves)),
                IsRankedApplied = true
            });
        }

        return result;
    }

    public static MatchEloBreakdownDto CalculateFromGame(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);

        HandResolution? resolution = game.LastHandResolution;
        bool isDraw = resolution is not null && resolution.IsDraw;
        bool isKeyWin = resolution is not null && resolution.IsKeyWin;
        bool isBreakWin = resolution is not null && resolution.BreakTriggered;

        int winningTeamId = -1;
        long? winningPlayerId = null;

        if (!isDraw)
        {
            if (game.IsTeam2v2)
            {
                if (game.MatchWinningTeamId >= 0)
                {
                    winningTeamId = game.MatchWinningTeamId;
                }
                else if (resolution is not null && resolution.WinningTeamId >= 0)
                {
                    winningTeamId = resolution.WinningTeamId;
                }
                else if (game.Classement.Count > 0)
                {
                    winningTeamId = game.Classement[0].TeamId;
                }
            }
            else
            {
                if (game.MatchWinner is not null)
                {
                    winningPlayerId = game.MatchWinner.Id;
                }
                else if (resolution?.Winner is not null)
                {
                    winningPlayerId = resolution.Winner.Id;
                }
                else if (game.Classement.Count > 0)
                {
                    winningPlayerId = game.Classement[0].Id;
                }
            }
        }

        List<EloPlayerMatchInput> inputs = game.Players.Select(player =>
        {
            bool isWinner = !isDraw && (game.IsTeam2v2
                ? (winningTeamId >= 0 && player.TeamId == winningTeamId)
                : (winningPlayerId.HasValue && player.Id == winningPlayerId.Value));

            return new EloPlayerMatchInput
            {
                PlayerId = player.Id,
                Username = player.Login,
                CurrentElo = player.Elo > 0 ? player.Elo : DefaultElo,
                IsHuman = player.IsHumanSeat && player.Id > 0,
                TeamId = player.TeamId,
                IsWinner = isWinner,
                IsDraw = isDraw,
                IsKeyWin = isKeyWin && isWinner,
                IsGameBrukWin = isBreakWin && isWinner,
                WinAssists = player.MatchWinAssists,
                TotalMoves = player.MatchMovesCount,
                OptimalMoves = player.MatchOptimalMovesCount,
                ForcedPasses = player.MatchForcedPasses,
                TrappedDoubles = player.MatchTrappedDoubles,
                AverageTurnSeconds = player.MatchAverageTurnSeconds
            };
        }).ToList();

        return CalculateMatchRatings(new EloMatchContext
        {
            GameId = game.Id,
            IsOnlineMatch = game.IsOnlineMatch,
            IsTeam2v2 = game.IsTeam2v2,
            Players = inputs
        });
    }

    public static void ApplyMatchUpdateToUser(User user, PlayerEloBreakdownDto breakdown, DateTime? completedUtc = null)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(breakdown);

        if (!breakdown.IsRankedApplied) return;

        DateTime nowUtc = completedUtc ?? DateTime.UtcNow;

        user.Elo = RoatanRankTierHelper.ClampElo(breakdown.NewElo);

        bool isDraw = string.Equals(breakdown.ResultLabel, "Draw", StringComparison.OrdinalIgnoreCase);
        bool isWin = !isDraw && !string.Equals(breakdown.ResultLabel, "Loss", StringComparison.OrdinalIgnoreCase);
        if (isWin) user.Wins++;
        else if (!isDraw) user.Losses++;

        user.WinAssists += Math.Max(0, breakdown.WinAssists);

        int prevMoves = Math.Max(0, user.TotalMoves);
        int matchMoves = Math.Max(0, breakdown.TotalMoves);
        if (breakdown.AverageTurnSeconds > 0.0)
        {
            int weightNew = Math.Max(1, matchMoves);
            if (prevMoves > 0 && user.AverageTurnSeconds > 0.0)
            {
                user.AverageTurnSeconds = Math.Round(
                    ((user.AverageTurnSeconds * prevMoves) + (breakdown.AverageTurnSeconds * weightNew)) / (prevMoves + weightNew),
                    2);
            }
            else
            {
                user.AverageTurnSeconds = Math.Round(breakdown.AverageTurnSeconds, 2);
            }
        }

        user.TotalMoves = prevMoves + matchMoves;
        user.OptimalMoves = Math.Max(0, user.OptimalMoves) + Math.Clamp(breakdown.OptimalMoves, 0, matchMoves);

        if (user.LastOnlineMatchUtc.HasValue && (nowUtc - user.LastOnlineMatchUtc.Value).TotalDays <= 7.0)
        {
            user.OnlineMatchesLast7Days++;
        }
        else
        {
            user.OnlineMatchesLast7Days = 1;
        }
        user.LastOnlineMatchUtc = nowUtc;
    }
}
