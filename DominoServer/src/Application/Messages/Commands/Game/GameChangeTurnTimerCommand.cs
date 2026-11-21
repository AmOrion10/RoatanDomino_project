using Application.Common;
using Application.Exceptions.Game;
using Application.Interfaces.Managers;

namespace Application.Messages.Commands.Game;

using Domain.Game;

public class GameChangeTurnTimerCommand(int gameId, bool timerExpired) : ICommand<Game>
{
    public int GameId { get; set; } = gameId;
    public bool TimerExpired { get; set; } = timerExpired;
}

public class GameChangeTurnTimerCommandHandler(IGameManager gameManager) : ICommandHandler<GameChangeTurnTimerCommand, Game>
{
    public Task<Game> Handle(GameChangeTurnTimerCommand request, CancellationToken cancellationToken)
    {
        Game game = gameManager.GetById(request.GameId) ?? throw new GameNotFoundException($"Game with id {request.GameId} not found");
        game.PlayerTurnTimer.HasStarted = false;
        if (request.TimerExpired)
        {
            game.NextTurn();
        }
        game.PlayerTurnTimer.Duration = game.IsLegacyDrawAdvance
            ? IGameManager.GameTimerPerPlayer
            : game.GetActiveTurnDurationSeconds();
        return Task.FromResult(game);
    }
}
