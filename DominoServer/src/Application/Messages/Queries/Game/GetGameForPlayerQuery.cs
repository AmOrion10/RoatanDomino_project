using Application.Common;
using Application.Interfaces.Managers;

namespace Application.Messages.Queries.Game;

using Application.Exceptions.Game;
using Application.Interfaces;

using Domain.Game;

public class GetGameForPlayerQuery(IClient client) : IQuery<Game>
{
    public IClient Client { get; set; } = client;
}

public class GetGameForPlayerQueryHandler(IGameManager gameManager) : IQueryHandler<GetGameForPlayerQuery, Game>
{
    public Task<Game> Handle(GetGameForPlayerQuery request, CancellationToken cancellationToken)
    {
        Game game = gameManager.GetForClient(request.Client) ?? throw new GameNotFoundForPlayerException("Player isn't in a game");
        return Task.FromResult(game);
    }
}
