using Application.Common;
using Application.Interfaces.Managers;

namespace Application.Messages.Commands.Game;

using Application.Exceptions.Game;

using Domain.Game;

public class GamePlaceFirstDominoCommand(int gameId) : ICommand<Game>
{
    public int GameId { get; set; } = gameId;
}

public class GamePlaceFirstDominoCommandHandler(IGameManager gameManager) : ICommandHandler<GamePlaceFirstDominoCommand, Game>
{
    public Task<Game> Handle(GamePlaceFirstDominoCommand request, CancellationToken cancellationToken)
    {
        Game game = gameManager.GetById(request.GameId) ?? throw new GameNotFoundException($"Game with id {request.GameId} not found"); ;
        game.PlaceFirstDomino();
        game.NextTurn();
        return Task.FromResult(game);
    }
}
