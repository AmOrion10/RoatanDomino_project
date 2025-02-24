using Application.Common;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Domain;

namespace Application.Messages.Queries.Friends;

public class GetAllFriendsQuery(IClient client) : IQuery<List<Friend>>
{
    public IClient Client { get; set; } = client;
}

public class GetAllFriendsQueryHandler(IFriendshipRepository friendshipRepository) : IQueryHandler<GetAllFriendsQuery, List<Friend>>
{
    public async Task<List<Friend>> Handle(GetAllFriendsQuery request, CancellationToken cancellationToken)
    {
        return await friendshipRepository.GetAllForUser(request.Client.User!.Id);
    }
}
