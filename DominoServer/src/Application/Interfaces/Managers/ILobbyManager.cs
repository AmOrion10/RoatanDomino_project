using Domain;
using DominoCommon.com.domino.common.Scripts.Enums;

namespace Application.Interfaces.Managers;

public interface ILobbyManager
{
    Lobby CreateLobby(IClient creator, int maxNumberOfPlayer, bool lobbyPublic, string name, bool ranked = false, string? code = null, TableStyle? tableStyle = null, MatchFormat? matchFormat = null, bool? oneOnePlayoff = null);
    void AddPlayerToLobby(IClient player, int lobbyId);
    void RemovePlayerFromLobby(IClient player, int lobbyId);
    void ReadyPlayerStateChange(IClient player, int lobbyId);
    Lobby? GetLobbyForClient(IClient client);
    Lobby? GetLobbyById(int lobbyId);
    Lobby? GetARankedLobby();
    Lobby? GetLobbyByCode(string code);
    List<Lobby> GetAllPublicLobbies();
    List<Lobby> GetPublicLobbiesWithName(string name);
    List<Lobby> GetLobbiesWithTimerStarted();
    bool PlayerHasAlreadyALobby(IClient player);
    bool PlayerIsAlreadyInALobby(IClient player);
    bool PlayerIsInLobby(IClient player, int lobbyId);
    bool MaxNumberOfPlayerReached(int lobbyId);
    void UpdateTimerInLobby(int lobbyId, int newDuration);
    void DeleteLobby(int lobbyId);
    bool ExistById(int lobbyId);
}
