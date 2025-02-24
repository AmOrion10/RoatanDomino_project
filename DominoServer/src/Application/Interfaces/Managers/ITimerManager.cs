using Domain;

namespace Application.Interfaces.Managers;

public interface ITimerManager
{
    public const int LobbyTimerCanceled = -1;
    public const int LobbyTimerAtLeastOneReady = 60;
    public const int LobbyTimerAllReady = 10;

    void UpdateTimerInLobby(Lobby lobby);
}