using Application.Exceptions.Lobby;
using Application.Interfaces;
using Application.Interfaces.Managers;

using Domain;
using DominoCommon.com.domino.common.Scripts.Enums;

namespace Infrastructure.Services.Managers;

public class LobbyManager(ITimerManager timerManager, ICodeGenerator codeGenerator, IChatManager chatManager) : ILobbyManager
{
    public const string HouseTableName = "Roatan House Table";
    public const string HouseHostName = "House";

    private static readonly string[] BotNames = ["Patrik (Bot)", "Anthony (Bot)", "Astor (Bot)", "Kendra (Bot)", "Carlos (Bot)", "Maya (Bot)"];

    private int lobbyIdCounter = 1;
    private long botIdCounter = -1000;
    private readonly List<Lobby> lobbies = [];

    public bool MaintainHouseTable { get; set; }

    public Lobby EnsureHouseTable()
    {
        lock (lobbies)
        {
            Lobby? existing = lobbies.FirstOrDefault(l => l.IsHouseTable && l.IsPublic);
            if (existing is not null)
            {
                return existing;
            }

            string code = codeGenerator.GenerateCodeForLobby();
            LobbyPlayer houseHost = new(new User("house@roatandomino.local", HouseHostName)
            {
                Id = 0,
                Score = 1000
            });

            Lobby houseTable = new(
                lobbyIdCounter++,
                houseHost,
                maxNumberOfPlayer: 4,
                isPublic: true,
                name: HouseTableName,
                ranked: false,
                code: code,
                tableStyle: TableStyle.DRAW,
                matchFormat: MatchFormat.SIX_LOV,
                oneOnePlayoff: false,
                isHouseTable: true)
            {
                PlayersInLobby = [],
                ChatId = chatManager.CreateChat("Table")
            };

            lobbies.Insert(0, houseTable);
            return houseTable;
        }
    }

    public Lobby CreateLobby(IClient creator, int maxNumberOfPlayer, bool lobbyPublic, string name, bool ranked = false, string? code = null, TableStyle? tableStyle = null, MatchFormat? matchFormat = null, bool? oneOnePlayoff = null)
    {
        int effectiveMax = NormalizeMaxPlayers(maxNumberOfPlayer, matchFormat);
        code ??= codeGenerator.GenerateCodeForLobby();
        LobbyPlayer lobbyCreator = new(creator.User!) { SeatIndex = 0 };
        Lobby lobby = new(lobbyIdCounter++, lobbyCreator, effectiveMax, lobbyPublic, name, ranked, code, tableStyle, matchFormat, oneOnePlayoff)
        {
            PlayersInLobby = [lobbyCreator],
            ChatId = chatManager.CreateChat("Table")
        };
        lock (lobbies)
        {
            lobbies.Add(lobby);
        }
        chatManager.ClientJoinChat(creator, lobby.ChatId);
        return lobby;
    }

    public static int NormalizeMaxPlayers(int requestedMaxPlayers, MatchFormat? matchFormat)
    {
        if (matchFormat.HasValue)
        {
            return matchFormat.Value switch
            {
                MatchFormat.SIX_LOV => 4,
                MatchFormat.TWO_BEST => requestedMaxPlayers >= 4 ? 4 : 2,
                MatchFormat.CUT_THROAT => Math.Clamp(requestedMaxPlayers, 2, 4),
                _ => Math.Clamp(requestedMaxPlayers, 2, 4)
            };
        }

        if (requestedMaxPlayers > 4) return 4;
        if (requestedMaxPlayers < 1) return 2;
        return requestedMaxPlayers;
    }

    public void AddPlayerToLobby(IClient player, int lobbyId)
    {
        Lobby lobby;
        lock (lobbies)
        {
            lobby = lobbies.First(l => l.Id == lobbyId);
            if (lobby.PlayersInLobby.Count >= lobby.MaxNumberOfPlayer)
            {
                throw new MaxNumberOfPlayerReachedException($"This table is full ({lobby.PlayersInLobby.Count}/{lobby.MaxNumberOfPlayer} players).");
            }

            int seatIndex = lobby.FindFirstOpenSeat();
            if (seatIndex < 0)
            {
                throw new MaxNumberOfPlayerReachedException($"This table is full ({lobby.PlayersInLobby.Count}/{lobby.MaxNumberOfPlayer} players).");
            }

            lobby.PlayersInLobby.Add(new LobbyPlayer(player.User!) { SeatIndex = seatIndex });
        }
        timerManager.UpdateTimerInLobby(lobby);
        chatManager.ClientJoinChat(player, lobby.ChatId);
    }

    public void RemovePlayerFromLobby(IClient player, int lobbyId)
    {
        Lobby lobby;
        bool shouldDelete = false;
        lock (lobbies)
        {
            lobby = lobbies.First(l => l.Id == lobbyId);
            LobbyPlayer? target = lobby.PlayersInLobby.FirstOrDefault(p => p.Id == player.User!.Id);
            if (target is not null)
            {
                lobby.PlayersInLobby.Remove(target);
            }

            List<LobbyPlayer> remainingHumans = lobby.GetHumanPlayers();
            if (lobby.IsHouseTable)
            {
                if (remainingHumans.Count == 0)
                {
                    lobby.PlayersInLobby.Clear();
                    lobby.Timer.HasStarted = false;
                    lobby.Timer.Duration = ITimerManager.LobbyTimerCanceled;
                }
            }
            else if (remainingHumans.Count == 0)
            {
                shouldDelete = true;
            }
            else if (lobby.Creator.Id == player.User!.Id)
            {
                lobby.Creator = remainingHumans[0];
            }
        }

        if (shouldDelete)
        {
            DeleteLobby(lobby.Id);
        }
        else
        {
            timerManager.UpdateTimerInLobby(lobby);
            try
            {
                chatManager.ClientLeaveChat(player, lobby.ChatId);
            }
            catch
            {
                // Client may not be in chat in some test setups
            }
        }
    }

    public void ReadyPlayerStateChange(IClient player, int lobbyId)
    {
        lock (lobbies)
        {
            Lobby lobby = lobbies.First(l => l.Id == lobbyId);
            LobbyPlayer lobbyPlayer = lobby.PlayersInLobby.First(p => p.Id == player.User!.Id);
            lobbyPlayer.IsReady = !lobbyPlayer.IsReady;
            timerManager.UpdateTimerInLobby(lobby);
        }
    }

    public bool SwitchSeat(IClient player, int lobbyId, int targetSeatIndex)
    {
        lock (lobbies)
        {
            Lobby? lobby = lobbies.FirstOrDefault(l => l.Id == lobbyId);
            if (lobby is null || player.User is null) return false;
            return lobby.TrySwitchSeat(player.User.Id, targetSeatIndex);
        }
    }

    public LobbyPlayer? AddBotToLobby(IClient requester, int lobbyId, int seatIndex, string difficulty)
    {
        lock (lobbies)
        {
            Lobby? lobby = lobbies.FirstOrDefault(l => l.Id == lobbyId);
            if (lobby is null || requester.User is null) return null;
            if (!lobby.IsHost(requester.User.Id)) return null;
            if (lobby.IsFull()) return null;

            int resolvedSeat = seatIndex >= 0 && seatIndex < lobby.MaxNumberOfPlayer && lobby.GetPlayerAtSeat(seatIndex) is null
                ? seatIndex
                : lobby.FindFirstOpenSeat();
            if (resolvedSeat < 0) return null;

            long nextBotId = Interlocked.Decrement(ref botIdCounter);
            HashSet<string> existingNames = lobby.PlayersInLobby.Select(p => p.Login).ToHashSet(StringComparer.OrdinalIgnoreCase);
            string botName = BotNames.FirstOrDefault(n => !existingNames.Contains(n)) ?? $"Bot {Math.Abs(nextBotId)}";

            LobbyPlayer bot = LobbyPlayer.CreateBot(nextBotId, botName, resolvedSeat, difficulty);
            lobby.PlayersInLobby.Add(bot);
            return bot;
        }
    }

    public bool RemoveBotFromLobby(IClient requester, int lobbyId, int seatIndex)
    {
        lock (lobbies)
        {
            Lobby? lobby = lobbies.FirstOrDefault(l => l.Id == lobbyId);
            if (lobby is null || requester.User is null) return false;
            if (!lobby.IsHost(requester.User.Id)) return false;

            LobbyPlayer? bot = lobby.GetPlayerAtSeat(seatIndex);
            if (bot is null || !bot.IsBot) return false;

            lobby.PlayersInLobby.Remove(bot);
            return true;
        }
    }

    public Lobby? GetLobbyForClient(IClient client)
    {
        if (client.User is null) return null;
        lock (lobbies)
        {
            return lobbies.FirstOrDefault(l => l.PlayersInLobby.Any(p => !p.IsBot && p.Id == client.User.Id));
        }
    }

    public Lobby? GetLobbyById(int lobbyId)
    {
        lock (lobbies)
        {
            return lobbies.FirstOrDefault(l => l.Id == lobbyId);
        }
    }

    public Lobby? GetARankedLobby()
    {
        lock (lobbies)
        {
            return lobbies.FirstOrDefault(l => l.Ranked && !l.IsFull());
        }
    }

    public List<Lobby> GetAllPublicLobbies()
    {
        if (MaintainHouseTable)
        {
            bool needsHouse;
            lock (lobbies)
            {
                needsHouse = !lobbies.Any(l => l.IsPublic);
            }
            if (needsHouse)
            {
                EnsureHouseTable();
            }
        }

        lock (lobbies)
        {
            return [.. lobbies.Where(l => l.IsPublic)];
        }
    }

    public List<Lobby> GetPublicLobbiesWithName(string name)
    {
        lock (lobbies)
        {
            IEnumerable<Lobby> resultLobbies = lobbies.Where(l => l.IsPublic);
            if (!string.IsNullOrEmpty(name))
            {
                resultLobbies = resultLobbies.Where(l => l.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
            }
            return resultLobbies.ToList();
        }
    }

    public List<Lobby> GetLobbiesWithTimerStarted()
    {
        lock (lobbies)
        {
            return lobbies.Where(l => l.Timer.HasStarted).ToList();
        }
    }

    public bool PlayerHasAlreadyALobby(IClient player)
    {
        if (player.User is null) return false;
        lock (lobbies)
        {
            return lobbies.Any(l => !l.IsHouseTable && l.Creator.Id == player.User.Id);
        }
    }

    public bool PlayerIsAlreadyInALobby(IClient player)
    {
        if (player.User is null) return false;
        lock (lobbies)
        {
            return lobbies.Any(l => l.PlayersInLobby.Any(p => !p.IsBot && p.Id == player.User.Id));
        }
    }

    public bool PlayerIsInLobby(IClient player, int lobbyId)
    {
        if (player.User is null) return false;
        lock (lobbies)
        {
            Lobby? lobby = lobbies.FirstOrDefault(l => l.Id == lobbyId);
            return lobby is not null && lobby.PlayersInLobby.Any(p => !p.IsBot && p.Id == player.User.Id);
        }
    }

    public bool MaxNumberOfPlayerReached(int lobbyId)
    {
        lock (lobbies)
        {
            Lobby lobby = lobbies.First(l => l.Id == lobbyId);
            return lobby.PlayersInLobby.Count >= lobby.MaxNumberOfPlayer;
        }
    }

    public void UpdateTimerInLobby(int lobbyId, int newDuration)
    {
        lock (lobbies)
        {
            lobbies.First(l => l.Id == lobbyId).Timer.Duration = newDuration;
        }
    }

    public void DeleteLobby(int lobbyId)
    {
        Lobby? lobby;
        bool shouldRespawnHouse = false;
        lock (lobbies)
        {
            lobby = lobbies.FirstOrDefault(l => l.Id == lobbyId);
            if (lobby is null) return;
            lobbies.Remove(lobby);
            shouldRespawnHouse = MaintainHouseTable && (!lobbies.Any(l => l.IsPublic) || lobby.IsHouseTable);
        }
        codeGenerator.FreeLobbyCode(lobby.Code);
        chatManager.DeleteChat(lobby.ChatId);

        if (shouldRespawnHouse)
        {
            EnsureHouseTable();
        }
    }

    public bool ExistById(int lobbyId)
    {
        lock (lobbies)
        {
            return lobbies.Any(l => l.Id == lobbyId);
        }
    }

    public Lobby? GetLobbyByCode(string code)
    {
        lock (lobbies)
        {
            return lobbies.FirstOrDefault(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        }
    }
}
