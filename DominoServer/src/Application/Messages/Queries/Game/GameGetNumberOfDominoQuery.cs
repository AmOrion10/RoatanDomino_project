using Application.Common;
using Application.Exceptions.Game;
using Application.Interfaces.Managers;

namespace Application.Messages.Queries.Game;

using Domain.Game;

public class GameGetNumberOfDominoQuery(int gameId) : IQuery<Dictionary<long, int>>
{
    public int GameId { get; set; } = gameId;
}

public class GameGetNumberOfDominoQueryHandler(IGameManager gameManager) : IQueryHandler<GameGetNumberOfDominoQuery, Dictionary<long, int>>
{
    public Task<Dictionary<long, int>> Handle(GameGetNumberOfDominoQuery request, CancellationToken cancellationToken)
    {
        Game game = gameManager.GetById(request.GameId) ?? throw new GameNotFoundException($"Game with id {request.GameId} not found");

        return Task.FromResult(game.GetNumberOfDominoAllPlayer());
    }
}
