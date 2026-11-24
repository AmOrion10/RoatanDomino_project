using Application.Interfaces;
using Application.Interfaces.Managers;
using Application.Interfaces.Repositories;

using Domain;
using Domain.Game;

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
        if (activeClient is null)
        {
            return new PlayerPresenceDto
            {
                UserId = user.Id,
                Username = user.Login,
                Elo = user.Score,
                IsOnline = false,
                Status = "Offline"
            };
        }

        Game? game = gameManager.GetForClient(activeClient);
        if (game is not null)
        {
            return new PlayerPresenceDto
            {
                UserId = user.Id,
                Username = user.Login,
                Elo = user.Score,
                IsOnline = true,
                Status = "In Match",
                TableId = game.LobbyId > 0 ? game.LobbyId : game.Id,
                TableName = game.MatchTitle
            };
        }

        Lobby? lobby = lobbyManager.GetLobbyForClient(activeClient);
        if (lobby is not null)
        {
            return new PlayerPresenceDto
            {
                UserId = user.Id,
                Username = user.Login,
                Elo = user.Score,
                IsOnline = true,
                Status = "At Table",
                TableId = lobby.Id,
                TableName = lobby.Name
            };
        }

        return new PlayerPresenceDto
        {
            UserId = user.Id,
            Username = user.Login,
            Elo = user.Score,
            IsOnline = true,
            Status = "Available"
        };
    }
}
