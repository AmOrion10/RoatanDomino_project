using Application.Common;
using Application.Interfaces.Managers;

namespace Application.Messages.Commands.Game;

using Domain.Game;

public class UpdatePlayerTurnGameTimerCommand(int timeElapsed) : ICommand<List<int>>
{
    public int TimeElapsed { get; set; } = timeElapsed;
}

public class UpdatePlayerTurnGameTimerCommandHandler(IGameManager gameManager) : ICommandHandler<UpdatePlayerTurnGameTimerCommand, List<int>>
{
    public Task<List<int>> Handle(UpdatePlayerTurnGameTimerCommand request, CancellationToken cancellationToken)
    {
        List<int> gameIdWithTimerExpired = [];
        List<Game> games = gameManager.GetGamesWithPlayerTurnTimerStarted();

        foreach (Game game in games)
        {
            int newTimerDuration = game.PlayerTurnTimer.Duration - request.TimeElapsed;
            if (newTimerDuration <= 0)
            {
                gameIdWithTimerExpired.Add(game.Id);
            }
            gameManager.UpdatePlayerTurnTimer(game.Id, Math.Max(newTimerDuration, 0));
        }

        return Task.FromResult(gameIdWithTimerExpired);
    }
}
