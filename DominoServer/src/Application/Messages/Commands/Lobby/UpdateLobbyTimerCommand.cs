using Application.Common;
using Application.Interfaces.Managers;

namespace Application.Messages.Commands.Lobby;

public class UpdateLobbyTimerCommand(int timeElapsed) : ICommand<List<int>>
{
    public int TimeElapsed { get; set; } = timeElapsed;
}

public class UpdateLobbyTimerCommandHandler(ILobbyManager lobbyManager) : ICommandHandler<UpdateLobbyTimerCommand, List<int>>
{
    public Task<List<int>> Handle(UpdateLobbyTimerCommand request, CancellationToken cancellationToken)
    {
        List<int> lobbyIdWithTimerExpired = [];
        List<Domain.Lobby> lobbies = lobbyManager.GetLobbiesWithTimerStarted();

        foreach (Domain.Lobby lobby in lobbies)
        {
            int newTimerDuration = lobby.Timer.Duration - request.TimeElapsed;
            if (newTimerDuration <= 0)
            {
                lobbyIdWithTimerExpired.Add(lobby.Id);
            }
            lobbyManager.UpdateTimerInLobby(lobby.Id, Math.Max(newTimerDuration, 0));
        }

        return Task.FromResult(lobbyIdWithTimerExpired);
    }
}