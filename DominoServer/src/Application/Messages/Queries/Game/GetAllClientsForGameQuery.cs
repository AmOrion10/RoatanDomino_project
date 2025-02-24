using Application.Common;
using Application.Exceptions.Game;
using Application.Interfaces;
using Application.Interfaces.Managers;

namespace Application.Messages.Queries.Game;

using Domain.Game;

public class GetAllClientsForGameQuery(int gameId) : IQuery<List<IClient>>
{
    public int GameId { get; set; } = gameId;
}

public class GetAllClientsForGameQueryHandler(IClientManager clientManager, IGameManager gameManager) : IQueryHandler<GetAllClientsForGameQuery, List<IClient>>
{
    public Task<List<IClient>> Handle(GetAllClientsForGameQuery request, CancellationToken cancellationToken)
    {
        Game game = gameManager.GetById(request.GameId) ?? throw new GameNotFoundException($"Game with id {request.GameId} not found");
        List<IClient> allClients = clientManager.GetAllLoggedInClients();
        List<IClient> clientsInGame = [];

        foreach (IClient client in allClients)
        {
            if (game.Players.Any(gp => gp.Id == client.User!.Id))
            {
                clientsInGame.Add(client);
            }
        }

        return Task.FromResult(clientsInGame);
    }
}
