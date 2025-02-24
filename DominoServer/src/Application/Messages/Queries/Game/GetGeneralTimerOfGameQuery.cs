using Application.Common;
using Application.Interfaces.Managers;

namespace Application.Messages.Queries.Game;

using Domain;

public class GetGeneralTimerOfGameQuery(int gameId) : IQuery<Timer>
{
    public int GameId { get; set; } = gameId;
}

public class GetGeneralTimerOfGameQueryHandler(IGameManager gameManager) : IQueryHandler<GetGeneralTimerOfGameQuery, Timer>
{
    public Task<Timer> Handle(GetGeneralTimerOfGameQuery request, CancellationToken cancellationToken)
    {
        if (gameManager.AllPlayersReadyForGame(request.GameId))
        {
            gameManager.StartTimerGeneralGame(request.GameId);
        }
        return Task.FromResult(gameManager.GetGeneralTimerForGame(request.GameId));
    }
}
