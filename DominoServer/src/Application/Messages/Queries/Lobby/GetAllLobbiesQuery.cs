using Application.Common;
using Application.Interfaces.Managers;

namespace Application.Messages.Queries.Lobby;

using Domain;

public class GetAllLobbiesQuery : IQuery<List<Lobby>>
{
}

public class GetAllLobbiesQueryHandler(ILobbyManager lobbyManager) : IQueryHandler<GetAllLobbiesQuery, List<Lobby>>
{
    public Task<List<Lobby>> Handle(GetAllLobbiesQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(lobbyManager.GetAllPublicLobbies());
    }
}