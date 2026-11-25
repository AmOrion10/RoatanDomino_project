using Application.Interfaces;
using Application.Interfaces.Managers;
using Application.Interfaces.Repositories;

using Domain;
using Domain.Game;

using DominoCommon.com.domino.common.Scripts.Enums;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame;

namespace Infrastructure.Services;

public class PresenceService(
    IUserRepository userRepository,
    IClientManager clientManager,
    ILobbyManager lobbyManager,
    IGameManager gameManager)
{
    public async Task<PlayerRosterDto> BuildRosterAsync()
    {
        List<User> registeredUsers = await userRepository.GetAllUsers();
        List<IClient> onlineClients = clientManager.GetAllLoggedInClients();
        Dictionary<long, IClient> onlineById = [];
        foreach (IClient c in onlineClients)
        {
            if (c.User is not null && c.User.Id > 0)
            {
                onlineById[c.User.Id] = c;
            }
        }

        Dictionary<long, PlayerPresenceDto> entries = [];
        foreach (User user in registeredUsers)
        {
            if (user.Id <= 0) continue;
            entries[user.Id] = CreatePresenceEntry(user, onlineById.GetValueOrDefault(user.Id));
        }

        // Include any logged-in users that may exist in test/mock setups without DB rows
        foreach (KeyValuePair<long, IClient> kv in onlineById)
        {
            if (!entries.ContainsKey(kv.Key) && kv.Value.User is not null)
            {
                entries[kv.Key] = CreatePresenceEntry(kv.Value.User, kv.Value);
            }
        }

        List<PlayerPresenceDto> sorted = entries.Values
            .OrderByDescending(p => p.IsOnline)
            .ThenBy(p => p.Status switch
            {
                "Available" => 0,
                "At Table" => 1,
                "In Match" => 2,
                _ => 3
            })
            .ThenBy(p => p.Username, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new PlayerRosterDto { Players = sorted };
    }

    public async Task BroadcastRosterAsync(int? excludeClientId = null)
    {
        List<IClient> loggedIn = clientManager.GetAllLoggedInClients();
        if (loggedIn.Count == 0) return;

        PlayerRosterDto roster = await BuildRosterAsync();
        NetworkFrame frame = new(MessageFrameBuilder.PlayerRosterUpdatedFrame(roster));

        foreach (IClient target in loggedIn)
        {
            if (excludeClientId.HasValue && target.Id == excludeClientId.Value) continue;
            try
            {
                await target.SendAsync(frame);
            }
            catch
            {
                // Ignore broken sockets during presence broadcast
            }
        }
    }

    private PlayerPresenceDto CreatePresenceEntry(User user, IClient? activeClient)
    {
        int resolvedElo = user.Elo >= RoatanRankTierHelper.MinElo ? user.Elo : RoatanRankTierHelper.DefaultElo;
        PlayerPresenceDto dto = new()
        {
            UserId = user.Id,
            Username = user.Login,
            Elo = resolvedElo,
            Wins = user.Wins,
            Losses = user.Losses,
            WinAssists = user.WinAssists,
            TotalMoves = user.TotalMoves,
            OptimalMoves = user.OptimalMoves,
            AverageTurnSeconds = user.AverageTurnSeconds,
            OnlineMatchesLast7Days = user.OnlineMatchesLast7Days,
            LastOnlineMatchUtc = user.LastOnlineMatchUtc,
            AvatarUrl = user.AvatarUrl,
            AvatarBase64 = user.AvatarBase64
        };

        if (activeClient is null)
        {
            dto.IsOnline = false;
            dto.Status = "Offline";
            return dto;
        }

        dto.IsOnline = true;
        Game? game = gameManager.GetForClient(activeClient);
        if (game is not null)
        {
            dto.Status = "In Match";
            dto.TableId = game.LobbyId > 0 ? game.LobbyId : game.Id;
            dto.TableName = game.MatchTitle;
            return dto;
        }

        Lobby? lobby = lobbyManager.GetLobbyForClient(activeClient);
        if (lobby is not null)
        {
            dto.Status = "At Table";
            dto.TableId = lobby.Id;
            dto.TableName = lobby.Name;
            return dto;
        }

        dto.Status = "Available";
        return dto;
    }
}
