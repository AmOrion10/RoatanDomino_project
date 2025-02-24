using Application.Common;
using Application.Interfaces.Managers;

namespace Application.Messages.Commands.Game;

using Domain.Game;

public class UpdateGeneralGameTimerCommand(int timeElapsed) : ICommand<List<int>>
{
    public int TimeElapsed { get; set; } = timeElapsed;
}

public class UpdateGeneralGameTimerCommandHandler(IGameManager gameManager) : ICommandHandler<UpdateGeneralGameTimerCommand, List<int>>
{
    public Task<List<int>> Handle(UpdateGeneralGameTimerCommand request, CancellationToken cancellationToken)
    {
        List<int> gameIdWithTimerExpired = [];
        List<Game> games = gameManager.GetGamesWithGeneralTimerStarted();

        foreach (Game game in games)
        {
            int newTimerDuration = game.GeneralTimer.Duration - request.TimeElapsed;
            if (newTimerDuration <= 0)
            {
                gameIdWithTimerExpired.Add(game.Id);
            }
            gameManager.UpdateGeneralTimer(game.Id, Math.Max(newTimerDuration, 0));
        }

        return Task.FromResult(gameIdWithTimerExpired);
    }
}
