using Domain;
using DominoCommon.com.domino.common.Scripts.Enums;

namespace Application.Interfaces.Managers;

public interface ILobbyManager
{
    bool MaintainHouseTable { get; set; }
    Lobby EnsureHouseTable();
    Lobby CreateLobby(IClient creator, int maxNumberOfPlayer, bool lobbyPublic, string name, bool ranked = false, string? code = null, TableStyle? tableStyle = null, MatchFormat? matchFormat = null, bool? oneOnePlayoff = null);
    Lobby CreateTeamChallengeLobby(IClient challenger, long challengeId, long teamAId, string teamAName, string teamATag, long teamBId, string teamBName, string teamBTag, MatchFormat matchFormat, TableStyle tableStyle, bool requireFullHumanSquad = false, DateTime? playDeadlineUtc = null, bool seatCreatorOnTeamB = false);
    void AddPlayerToLobby(IClient player, int lobbyId);
    int AddPlayerToTeamChallengeLobby(IClient player, int lobbyId, long? playerTeamId);
    void RemovePlayerFromLobby(IClient player, int lobbyId);
    void ReadyPlayerStateChange(IClient player, int lobbyId);
    bool SwitchSeat(IClient player, int lobbyId, int targetSeatIndex);
    LobbyPlayer? AddBotToLobby(IClient requester, int lobbyId, int seatIndex, string difficulty);
    bool RemoveBotFromLobby(IClient requester, int lobbyId, int seatIndex);
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
