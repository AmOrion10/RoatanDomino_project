using Application.Common;
using Application.Interfaces.Managers;

namespace Application.Messages.Queries.Game;

using Domain;

public class GetPlayerTurnTimerQuery(int gameId) : IQuery<Timer>
{
    public int GameId { get; set; } = gameId;
}

public class GetPlayerTurnTimerQueryHandler(IGameManager gameManager) : IQueryHandler<GetPlayerTurnTimerQuery, Timer>
{
    public Task<Timer> Handle(GetPlayerTurnTimerQuery request, CancellationToken cancellationToken)
    {
        gameManager.StartTimerPlayerTurn(request.GameId);
        return Task.FromResult(gameManager.GetPlayerTurnTimer(request.GameId));
    }
}
