using Application.Common;
using Application.Interfaces.Managers;

namespace Application.Messages.Queries.Lobby;

using Domain;

public class GetLobbiesWithNameQuery(string name) : IQuery<List<Lobby>>
{
    public string Name { get; set; } = name;
}

public class GetLobbiesWithNameQueryHandler(ILobbyManager lobbyManager) : IQueryHandler<GetLobbiesWithNameQuery, List<Lobby>>
{
    public Task<List<Lobby>> Handle(GetLobbiesWithNameQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(lobbyManager.GetPublicLobbiesWithName(request.Name));
    }
}