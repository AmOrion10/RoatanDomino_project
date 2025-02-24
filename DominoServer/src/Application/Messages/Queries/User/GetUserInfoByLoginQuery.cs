using Application.Common;
using Application.Interfaces;
using Application.Interfaces.Repositories;

namespace Application.Messages.Queries.User;

using Application.Exceptions.Connection;
using Domain;

public class GetUserInfoByLoginQuery(string login) : IQuery<User>
{
    public string Login { get; set; } = login;
}

public class GetUserInfoByLoginQueryHandler(IUserRepository userRepository) : IQueryHandler<GetUserInfoByLoginQuery, User>
{
    public async Task<User> Handle(GetUserInfoByLoginQuery request, CancellationToken cancellationToken)
    {
        if (!await userRepository.ExistsByLogin(request.Login))
        {
            throw new UserNotExistingException("User not found");
        }
        User user = await userRepository.GetByLogin(request.Login);
        user.Statistics = await userRepository.GetUserStats(user.Id);
        return user;
    }
}
