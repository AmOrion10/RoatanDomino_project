using Application.Common;
using Application.Interfaces.Repositories;

namespace Application.Messages.Queries.User;

using Domain.Game;

public class GetHistoryGameQuery(long id) : IQuery<HistoryGame>
{
    public long Id { get; set; } = id;
}

public class GetHistoryGameQueryHandler(IUserRepository userRepository) : IQueryHandler<GetHistoryGameQuery, HistoryGame>
{
    public async Task<HistoryGame> Handle(GetHistoryGameQuery request, CancellationToken cancellationToken)
    {
        return await userRepository.GetHistoryGame(request.Id);
    }
}
