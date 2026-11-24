using Application.Interfaces;
using Application.Interfaces.Managers;

using Domain;
using DominoCommon.com.domino.common.Scripts.Enums;

namespace Infrastructure.Services.Managers;

public class LobbyManager(ITimerManager timerManager, ICodeGenerator codeGenerator, IChatManager chatManager) : ILobbyManager
{
    private int lobbyIdCounter = 1;
    private readonly List<Lobby> lobbies = [];

    public Lobby CreateLobby(IClient creator, int maxNumberOfPlayer, bool lobbyPublic, string name, bool ranked = false, string? code = null, TableStyle? tableStyle = null, MatchFormat? matchFormat = null, bool? oneOnePlayoff = null)
    {
        code ??= codeGenerator.GenerateCodeForLobby();
        LobbyPlayer lobbyCreator = new(creator.User!);
        Lobby lobby = new(lobbyIdCounter++, lobbyCreator, maxNumberOfPlayer, lobbyPublic, name, ranked, code, tableStyle, matchFormat, oneOnePlayoff)
        {
            PlayersInLobby = [lobbyCreator],
            ChatId = chatManager.CreateChat("Lobby")
        };
        lock (lobbies)
        {
            lobbies.Add(lobby);
        }
        chatManager.ClientJoinChat(creator, lobby.ChatId);
        return lobby;
    }

    public void AddPlayerToLobby(IClient player, int lobbyId)
    {
        Lobby lobby;
        lock (lobbies)
        {
            lobby = lobbies.First(l => l.Id == lobbyId);
        }
        lobby.PlayersInLobby.Add(new LobbyPlayer(player.User!));
        timerManager.UpdateTimerInLobby(lobby);
        chatManager.ClientJoinChat(player, lobby.ChatId);
    }

    public void RemovePlayerFromLobby(IClient player, int lobbyId)
    {
        Lobby lobby;
        lock (lobbies)
        {
            lobby = lobbies.First(l => l.Id == lobbyId);
            lobby.PlayersInLobby.Remove(lobby.PlayersInLobby.First(p => p.Id == player.User!.Id));
        }

        if (lobby.IsEmpty())
        {
            DeleteLobby(lobby.Id);
        }
        else
        {
            lobby.Creator = lobby.PlayersInLobby[0];
            timerManager.UpdateTimerInLobby(lobby);
            chatManager.ClientLeaveChat(player, lobby.ChatId);
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

    public Lobby? GetLobbyForClient(IClient client)
    {
        lock (lobbies)
        {
            return lobbies.FirstOrDefault(l => l.PlayersInLobby.Any(p => p.Id == client.User!.Id));
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
            return lobbies.FirstOrDefault(l =>  l.Ranked && !l.IsFull());
        }
    }

    public List<Lobby> GetAllPublicLobbies()
    {
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
        lock (lobbies)
        {
            return lobbies.Any(l => l.Creator.Id == player.User!.Id);
        }
    }

    public bool PlayerIsAlreadyInALobby(IClient player)
    {
        lock (lobbies)
        {
            return lobbies.Any(l => l.PlayersInLobby.Any(p => p.Id == player.User!.Id));
        }
    }

    public bool PlayerIsInLobby(IClient player, int lobbyId)
    {
        lock (lobbies)
        {
            return lobbies.First(l => l.Id == lobbyId).PlayersInLobby.Any(p => p.Id == player.User!.Id);
        }
    }

    public bool MaxNumberOfPlayerReached(int lobbyId)
    {
        lock (lobbies)
        {
            Lobby lobby = lobbies.First(l => l.Id == lobbyId);
            return lobby.PlayersInLobby.Count == lobby.MaxNumberOfPlayer;
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
        Lobby lobby;
        lock (lobbies)
        {
            lobby = lobbies.First(l => l.Id == lobbyId);
            lobbies.Remove(lobby);
        }
        codeGenerator.FreeLobbyCode(lobby.Code);
        chatManager.DeleteChat(lobby.ChatId);
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
            return lobbies.FirstOrDefault(l => l.Code.Equals(code));
        }
    }
}
