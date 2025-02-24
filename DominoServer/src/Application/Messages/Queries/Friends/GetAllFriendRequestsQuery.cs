using Application.Common;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Domain;

namespace Application.Messages.Queries.Friends;

public class GetAllFriendRequestsQuery(IClient client) : IQuery<List<FriendRequest>>
{
    public IClient Client { get; set; } = client;
}

public class GetAllFriendRequestsQueryHandler(IFriendRequestRepository friendRequestRepository) : IQueryHandler<GetAllFriendRequestsQuery, List<FriendRequest>>
{
    public async Task<List<FriendRequest>> Handle(GetAllFriendRequestsQuery request, CancellationToken cancellationToken)
    {
        return await friendRequestRepository.GetAllForUserAsked(request.Client.User!.Id);
    }
}
