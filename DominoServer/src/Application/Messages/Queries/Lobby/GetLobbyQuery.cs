using Application.Common;
using Application.Exceptions.Lobby;
using Application.Interfaces.Managers;

namespace Application.Messages.Queries.Lobby;

using Domain;

public class GetLobbyQuery(int lobbyId) : IQuery<Lobby>
{
    public int LobbyId { get; set; } = lobbyId;
}

public class GetLobbyQueryHandler(ILobbyManager lobbyManager) : IQueryHandler<GetLobbyQuery, Lobby>
{
    public Task<Lobby> Handle(GetLobbyQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(lobbyManager.GetLobbyById(request.LobbyId) ?? throw new LobbyNotExistingException($"Lobby with id {request.LobbyId} not found"));
    }
}