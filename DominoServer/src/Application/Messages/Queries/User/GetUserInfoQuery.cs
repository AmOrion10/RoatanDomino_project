using Application.Common;
using Application.Interfaces;
using Application.Interfaces.Repositories;

namespace Application.Messages.Queries.User;

using Domain;

public class GetUserInfoQuery(IClient client) : IQuery<User>
{
    public IClient Client { get; set; } = client;
}

public class GetUserInfoQueryHandler(IUserRepository userRepository) : IQueryHandler<GetUserInfoQuery, User>
{
    public async Task<User> Handle(GetUserInfoQuery request, CancellationToken cancellationToken)
    {
        User user = await userRepository.GetById(request.Client.User!.Id);
        user.Statistics = await userRepository.GetUserStats(request.Client.User!.Id);
        return user;
    }
}
