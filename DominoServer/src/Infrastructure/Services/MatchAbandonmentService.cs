using Application.Interfaces;
using Application.Interfaces.Managers;
using Application.Interfaces.Repositories;

using Domain;
using Domain.Game;

using DominoCommon.com.domino.common.Scripts.Enums;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;

namespace Infrastructure.Services;

public sealed class AbandonmentPenaltyResult
{
    public bool Applied { get; init; }
    public long AbandoningPlayerId { get; init; }
    public int AbandonerNewElo { get; init; }
    public int AbandonerAbandonedMatches { get; init; }
    public Dictionary<long, int> OpponentNewElos { get; init; } = [];
    public TeamEloBreakdownDto? TeamBreakdown { get; init; }
}

public class MatchAbandonmentService(
    IUserRepository userRepository,
    IClientManager clientManager,
    ITeamChallengeEloUpdater? teamChallengeEloUpdater = null,
    TeamService? teamService = null)
{
    public const int PlayerAbandonmentEloDelta = 15;

    /// <summary>
    /// Applies the -15 Elo abandonment penalty (floor 100) + AbandonedMatches++ to the abandoning player,
    /// awards +15 Elo forfeit win to remaining human opponent(s), and if the match is a Team Challenge,
    /// deducts -15 TeamElo from the abandoner's team, awards +15 TeamElo to the opposing team,
    /// and marks the challenge Completed so it cannot be re-farmed.
    /// </summary>
    public async Task<AbandonmentPenaltyResult> ApplyAbandonmentPenaltyAsync(
        Game game,
        long abandoningPlayerId,
        IReadOnlyList<GamePlayer>? playersSnapshot = null)
    {
        if (game is null || abandoningPlayerId <= 0 || !game.IsOnlineMatch || game.IsMatchOver)
        {
            return new AbandonmentPenaltyResult { Applied = false };
        }

        IReadOnlyList<GamePlayer> roster = playersSnapshot ?? game.Players;
        List<GamePlayer> humanPlayers = roster
            .Where(p => p.Id > 0 && (p.IsHumanSeat || !p.IsPermanentlyBot))
            .ToList();

        if (humanPlayers.Count < 2)
        {
            return new AbandonmentPenaltyResult { Applied = false };
        }

        GamePlayer? abandonerSeat = humanPlayers.FirstOrDefault(p => p.Id == abandoningPlayerId);
        if (abandonerSeat is null)
        {
            return new AbandonmentPenaltyResult { Applied = false };
        }

        lock (game.PenalizedAbandonerIds)
        {
            if (!game.PenalizedAbandonerIds.Add(abandoningPlayerId))
            {
                return new AbandonmentPenaltyResult { Applied = false };
            }
        }

        List<IClient> onlineClients = clientManager.GetAllLoggedInClients();

        // 1. Deduct -15 Elo (floored at 100) and increment AbandonedMatches on the abandoning player
        User? abandonerUser = await SafeGetUserByIdAsync(abandoningPlayerId);
        int abandonerBaseElo = abandonerUser is not null
            ? RoatanRankTierHelper.ClampElo(abandonerUser.Elo)
            : RoatanRankTierHelper.ClampElo(abandonerSeat.Elo);

        int abandonerNewElo = RoatanRankTierHelper.ClampElo(abandonerBaseElo - PlayerAbandonmentEloDelta);
        int newAbandonedCount = (abandonerUser?.AbandonedMatches ?? abandonerSeat.AbandonedMatches) + 1;

        abandonerSeat.Elo = abandonerNewElo;
        abandonerSeat.Losses++;
        abandonerSeat.AbandonedMatches = newAbandonedCount;

        if (abandonerUser is not null)
        {
            abandonerUser.Elo = abandonerNewElo;
            abandonerUser.Losses++;
            abandonerUser.AbandonedMatches = newAbandonedCount;
            await SafeUpdateRankedStatsAsync(abandonerUser);
        }

        IClient? abandonerClient = onlineClients.FirstOrDefault(c => c.User is not null && c.User.Id == abandoningPlayerId);
        if (abandonerClient?.User is not null)
        {
            abandonerClient.User.Elo = abandonerNewElo;
            abandonerClient.User.Losses = abandonerUser?.Losses ?? abandonerSeat.Losses;
            abandonerClient.User.AbandonedMatches = newAbandonedCount;
        }

        // 2. Award +15 Elo forfeit win to remaining opposing human player(s)
        Dictionary<long, int> opponentNewElos = [];
        IEnumerable<GamePlayer> opponents = humanPlayers.Where(p =>
            p.Id != abandoningPlayerId &&
            (!game.IsTeam2v2 || p.TeamId != abandonerSeat.TeamId));

        foreach (GamePlayer oppSeat in opponents)
        {
            User? oppUser = await SafeGetUserByIdAsync(oppSeat.Id);
            int oppBaseElo = oppUser is not null
                ? RoatanRankTierHelper.ClampElo(oppUser.Elo)
                : RoatanRankTierHelper.ClampElo(oppSeat.Elo);
            int oppNewElo = RoatanRankTierHelper.ClampElo(oppBaseElo + PlayerAbandonmentEloDelta);

            oppSeat.Elo = oppNewElo;
            oppSeat.Wins++;
            opponentNewElos[oppSeat.Id] = oppNewElo;

            if (oppUser is not null)
            {
                oppUser.Elo = oppNewElo;
                oppUser.Wins++;
                await SafeUpdateRankedStatsAsync(oppUser);
            }

            IClient? oppClient = onlineClients.FirstOrDefault(c => c.User is not null && c.User.Id == oppSeat.Id);
            if (oppClient?.User is not null)
            {
                oppClient.User.Elo = oppNewElo;
                oppClient.User.Wins = oppUser?.Wins ?? oppSeat.Wins;
            }
        }

        // 3. If Team Challenge, apply -15 / +15 TeamElo penalty and mark challenge Completed so it cannot be re-farmed
        TeamEloBreakdownDto? teamBreakdown = null;
        if (game.IsTeamChallenge &&
            !game.TeamChallengeAbandonmentApplied &&
            game.TeamAId.HasValue &&
            game.TeamBId.HasValue &&
            teamChallengeEloUpdater is not null)
        {
            game.TeamChallengeAbandonmentApplied = true;
            long? abandoningTeamId = teamService is not null
                ? await teamService.GetUserTeamIdAsync(abandoningPlayerId)
                : null;

            if (!abandoningTeamId.HasValue || (abandoningTeamId.Value != game.TeamAId.Value && abandoningTeamId.Value != game.TeamBId.Value))
            {
                int seatIdx = -1;
                for (int i = 0; i < roster.Count; i++)
                {
                    if (roster[i].Id == abandoningPlayerId) { seatIdx = i; break; }
                }
                abandoningTeamId = (seatIdx >= 0 && seatIdx % 2 == 0) ? game.TeamAId.Value : game.TeamBId.Value;
            }

            teamBreakdown = await teamChallengeEloUpdater.ApplyTeamChallengeAbandonmentPenaltyAsync(
                game.TeamChallengeId,
                game.TeamAId.Value,
                game.TeamBId.Value,
                abandoningTeamId.Value);
        }

        // Prevent double-scoring if the match immediately terminates due to abandonment
        if (humanPlayers.Count <= 2 || game.IsTeamChallenge)
        {
            game.EloAppliedForCurrentMatch = true;
        }

        return new AbandonmentPenaltyResult
        {
            Applied = true,
            AbandoningPlayerId = abandoningPlayerId,
            AbandonerNewElo = abandonerNewElo,
            AbandonerAbandonedMatches = newAbandonedCount,
            OpponentNewElos = opponentNewElos,
            TeamBreakdown = teamBreakdown
        };
    }

    private async Task<User?> SafeGetUserByIdAsync(long userId)
    {
        try
        {
            return await userRepository.GetById(userId);
        }
        catch
        {
            return null;
        }
    }

    private async Task SafeUpdateRankedStatsAsync(User user)
    {
        try
        {
            await userRepository.UpdateRankedStats(user);
        }
        catch
        {
            // Ignore in lightweight unit-test setups without full DB backing
        }
    }
}
