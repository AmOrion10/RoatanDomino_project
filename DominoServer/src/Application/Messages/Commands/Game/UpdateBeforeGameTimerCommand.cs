using Application.Common;
using Application.Interfaces.Managers;

namespace Application.Messages.Commands.Game;

using Domain.Game;

public class UpdateBeforeGameTimerCommand(int timeElapsed) : ICommand<List<int>>
{
    public int TimeElapsed { get; set; } = timeElapsed;
}

public class UpdateBeforeGameTimerCommandHandler(IGameManager gameManager) : ICommandHandler<UpdateBeforeGameTimerCommand, List<int>>
{
    public Task<List<int>> Handle(UpdateBeforeGameTimerCommand request, CancellationToken cancellationToken)
    {
        List<int> gameIdWithTimerExpired = [];
        List<Game> games = gameManager.GetGamesWithTimerStarted();

        foreach (Game game in games)
        {
            int newTimerDuration = game.StartTimer.Duration - request.TimeElapsed;
            if (newTimerDuration <= 0)
            {
                gameIdWithTimerExpired.Add(game.Id);
            }
            gameManager.UpdateStartTimer(game.Id, Math.Max(newTimerDuration, 0));
        }

        return Task.FromResult(gameIdWithTimerExpired);
    }
}
