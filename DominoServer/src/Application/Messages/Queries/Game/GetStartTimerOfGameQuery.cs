using Application.Common;
using Application.Interfaces.Managers;

namespace Application.Messages.Queries.Game;

using Domain;

public class GetStartTimerOfGameQuery(int gameId) : IQuery<Timer>
{
    public int GameId { get; set; } = gameId;
}

public class GetStartTimerOfQueryQueryHandler(IGameManager gameManager) : IQueryHandler<GetStartTimerOfGameQuery, Timer>
{
    public Task<Timer> Handle(GetStartTimerOfGameQuery request, CancellationToken cancellationToken)
    {
        if (gameManager.AllPlayersReadyForGame(request.GameId))
        {
            gameManager.StartTimerForGame(request.GameId);
        }
        return Task.FromResult(gameManager.GetStartTimerForGame(request.GameId));
    }
}
