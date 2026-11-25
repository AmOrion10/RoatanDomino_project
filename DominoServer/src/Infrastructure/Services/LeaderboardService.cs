using Application.Interfaces.Repositories;

using Domain;

using DominoCommon.com.domino.common.Scripts.Enums;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;

using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class LeaderboardService(IUserRepository userRepository, ApplicationDbContext? dbContext = null)
{
    public async Task<LeaderboardDto> BuildLeaderboardAsync(long? requestingUserId = null, int limit = 100)
    {
        List<User> allUsers = await userRepository.GetAllUsers();
        LeaderboardDto dto = BuildFromUsers(allUsers, requestingUserId, limit);
        if (dbContext is not null)
        {
            Dictionary<long, (long TeamId, string TeamTag)> teamByUser = [];
            var memberships = await dbContext.TeamMembers
                .Include(m => m.Team)
                .Select(m => new { m.UserId, m.TeamId, Tag = m.Team.Tag })
                .ToListAsync();
            foreach (var m in memberships)
            {
                teamByUser[m.UserId] = (m.TeamId, m.Tag);
            }
            foreach (LeaderboardEntryDto entry in dto.Entries)
            {
                if (teamByUser.TryGetValue(entry.UserId, out (long TeamId, string TeamTag) tm))
                {
                    entry.TeamId = tm.TeamId;
                    entry.TeamTag = tm.TeamTag;
                }
            }
            if (dto.LocalPlayerEntry is not null && teamByUser.TryGetValue(dto.LocalPlayerEntry.UserId, out (long TeamId, string TeamTag) localTm))
            {
                dto.LocalPlayerEntry.TeamId = localTm.TeamId;
                dto.LocalPlayerEntry.TeamTag = localTm.TeamTag;
            }
        }
        return dto;
    }

    public static LeaderboardDto BuildFromUsers(IEnumerable<User> users, long? requestingUserId = null, int limit = 100)
    {
        int cappedLimit = Math.Clamp(limit, 1, 500);

        List<User> ranked = users
            .Where(u => u is not null && u.Id > 0)
            .OrderByDescending(u => RoatanRankTierHelper.ClampElo(u.Elo))
            .ThenByDescending(u => u.Wins)
            .ThenByDescending(u => u.AccuracyPercent)
            .ThenByDescending(u => u.WinAssists)
            .ThenBy(u => u.Login, StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<LeaderboardEntryDto> topEntries = [];
        LeaderboardEntryDto? localEntry = null;

        for (int i = 0; i < ranked.Count; i++)
        {
            User u = ranked[i];
            int rankPosition = i + 1;
            int clampedElo = RoatanRankTierHelper.ClampElo(u.Elo);
            RoatanRankTierInfo tierInfo = RoatanRankTierHelper.GetInfoForElo(clampedElo);

            LeaderboardEntryDto entry = new()
            {
                Rank = rankPosition,
                UserId = u.Id,
                Username = u.Login,
                AvatarUrl = u.AvatarUrl,
                AvatarBase64 = u.AvatarBase64,
                Elo = clampedElo,
                Tier = tierInfo.Tier,
                TierName = tierInfo.DisplayName,
                TierColorHex = tierInfo.BadgeColorHex,
                Wins = u.Wins,
                Losses = u.Losses,
                WinAssists = u.WinAssists,
                TotalMoves = u.TotalMoves,
                OptimalMoves = u.OptimalMoves,
                AccuracyPercent = u.AccuracyPercent,
                AverageTurnSeconds = u.AverageTurnSeconds,
                OnlineMatchesLast7Days = u.OnlineMatchesLast7Days,
                LastOnlineMatchUtc = u.LastOnlineMatchUtc
            };

            if (i < cappedLimit)
            {
                topEntries.Add(entry);
            }

            if (requestingUserId.HasValue && u.Id == requestingUserId.Value)
            {
                localEntry = entry;
            }
        }

        return new LeaderboardDto
        {
            Entries = topEntries,
            LocalPlayerEntry = localEntry,
            TotalRankedPlayers = ranked.Count
        };
    }
}
