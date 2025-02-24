using Application.Common;
using Application.Interfaces;
using Application.Interfaces.Managers;

namespace Application.Messages.Queries.Lobby;

public class GetAllClientsForLobbyQuery(int lobbyId) : IQuery<List<IClient>>
{
    public int LobbyId { get; set; } = lobbyId;
}

public class GetAllClientForLobbyQueryHandler(ILobbyManager lobbyManager, IClientManager clientManager) : IQueryHandler<GetAllClientsForLobbyQuery, List<IClient>>
{
    public Task<List<IClient>> Handle(GetAllClientsForLobbyQuery request, CancellationToken cancellationToken)
    {
        List<IClient> clientsInLobby = [];
        List<IClient> clients = clientManager.GetAllLoggedInClients();
        foreach (IClient client in clients)
        {
            if (lobbyManager.PlayerIsInLobby(client, request.LobbyId))
            {
                clientsInLobby.Add(client);
            }
        }
        return Task.FromResult(clientsInLobby);
    }
}