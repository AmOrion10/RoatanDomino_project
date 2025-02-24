using Application.Common;
using Application.Interfaces.Repositories;

namespace Application.Messages.Queries.User;

using Application.Interfaces;
using Domain.Game;

public class GetAllHistoryGamesQuery(IClient client) : IQuery<List<HistoryGame>>
{
    public IClient Client { get; set; } = client;
}

public class GetAllHistoryGamesQueryHandler(IUserRepository userRepository) : IQueryHandler<GetAllHistoryGamesQuery, List<HistoryGame>>
{
    public async Task<List<HistoryGame>> Handle(GetAllHistoryGamesQuery request, CancellationToken cancellationToken)
    {
        return await userRepository.GetAllHistoryGames(request.Client.User!.Id);
    }
}
