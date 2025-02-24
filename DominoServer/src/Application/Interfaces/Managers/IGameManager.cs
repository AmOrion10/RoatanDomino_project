using Domain;
using Domain.Game;

using Timer = Domain.Timer;

namespace Application.Interfaces.Managers;

public interface IGameManager
{
    public const int GameTimerStarting = 5;
    public const int GameTimerGeneral = 600;
    public const int GameTimerPerPlayer = 30;

    Game? GetById(int gameId);
    void CreateGameFromLobby(Lobby lobby, int seed);
    Game? GetForClient(IClient client);
    bool AllPlayersReadyForGame(int gameId);
    void StartTimerForGame(int gameId);
    Timer GetStartTimerForGame(int gameId);
    List<Game> GetGamesWithTimerStarted();
    void UpdateStartTimer(int gameId, int newDuration);
    void StartTimerGeneralGame(int gameId);
    Timer GetGeneralTimerForGame(int gameId);
    List<Game> GetGamesWithGeneralTimerStarted();
    void UpdateGeneralTimer(int gameId, int newDuration);
    void StartTimerPlayerTurn(int gameId);
    Timer GetPlayerTurnTimer(int gameId);
    List<Game> GetGamesWithPlayerTurnTimerStarted();
    void UpdatePlayerTurnTimer(int gameId, int newDuration);
    void DeleteGame(int gameId);
    bool PlayerLeave(int gameId, IClient playerLeaving);
}
