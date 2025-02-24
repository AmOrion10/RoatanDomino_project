using Application.Common;
using Application.Interfaces.Managers;

namespace Application.Messages.Queries.Game;

using Application.Exceptions.Game;

using Domain.Game;

public class GetDominoFromStackQuery(int gameId) : IQuery<Domino?>
{
    public int GameId { get; set; } = gameId;
}

public class GetDominoFromStackQueryHandler(IGameManager gameManager) : IQueryHandler<GetDominoFromStackQuery, Domino?>
{
    public Task<Domino?> Handle(GetDominoFromStackQuery request, CancellationToken cancellationToken)
    {
        Game game = gameManager.GetById(request.GameId) ?? throw new GameNotFoundException($"Game with id {request.GameId} not found");
        if (!game.Stack.IsEmpty())
        {
            return Task.FromResult(game.Draw(game.PlayerPlaying.Id));
        }
        return Task.FromResult((Domino?)null);
    }
}
