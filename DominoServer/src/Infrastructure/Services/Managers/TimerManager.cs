using Application.Interfaces.Managers;

using Domain;

namespace Infrastructure.Services.Managers;

public class TimerManager : ITimerManager
{
    public void UpdateTimerInLobby(Lobby lobby)
    {
        int numberOfPlayerReady = lobby.PlayersInLobby.Count(p => p.IsReady);
        if (lobby.Timer.HasStarted)
        {
            if (numberOfPlayerReady == lobby.MaxNumberOfPlayer && lobby.Timer.Duration > ITimerManager.LobbyTimerAllReady)
            {
                lobby.Timer.Duration = ITimerManager.LobbyTimerAllReady;
            }
            else if (numberOfPlayerReady == 0 || (numberOfPlayerReady == 1 && lobby.PlayersInLobby.Count == 1))
            {
                lobby.Timer.Duration = ITimerManager.LobbyTimerCanceled;
                lobby.Timer.HasStarted = false;
            }
        }
        else
        {
            if (numberOfPlayerReady >= 1 && numberOfPlayerReady < lobby.MaxNumberOfPlayer && lobby.PlayersInLobby.Count > 1)
            {
                lobby.Timer.Duration = ITimerManager.LobbyTimerAtLeastOneReady;
                lobby.Timer.HasStarted = true;
            }
        }
    }
}