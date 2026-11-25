using Application.Interfaces.Managers;

using Domain;

namespace Infrastructure.Services.Managers;

public class TimerManager : ITimerManager
{
    public void UpdateTimerInLobby(Lobby lobby)
    {
        int humanCount = lobby.GetHumanPlayers().Count;
        int numberOfPlayerReady = lobby.PlayersInLobby.Count(p => !p.IsBot && p.IsReady);

        // Online tables require at least 2 real human players before any auto-start countdown can run
        if (humanCount < 2)
        {
            if (lobby.Timer.HasStarted)
            {
                lobby.Timer.Duration = ITimerManager.LobbyTimerCanceled;
                lobby.Timer.HasStarted = false;
            }
            return;
        }

        int totalReadyIncludingBots = lobby.PlayersInLobby.Count(p => p.IsReady);
        if (lobby.Timer.HasStarted)
        {
            if (totalReadyIncludingBots == lobby.MaxNumberOfPlayer && lobby.Timer.Duration > ITimerManager.LobbyTimerAllReady)
            {
                lobby.Timer.Duration = ITimerManager.LobbyTimerAllReady;
            }
            else if (numberOfPlayerReady == 0)
            {
                lobby.Timer.Duration = ITimerManager.LobbyTimerCanceled;
                lobby.Timer.HasStarted = false;
            }
        }
        else
        {
            if (numberOfPlayerReady >= 1 && totalReadyIncludingBots < lobby.MaxNumberOfPlayer && humanCount > 1)
            {
                lobby.Timer.Duration = ITimerManager.LobbyTimerAtLeastOneReady;
                lobby.Timer.HasStarted = true;
            }
        }
    }
}
