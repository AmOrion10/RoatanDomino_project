using Application.Common;
using Application.Exceptions.Connection;
using Application.Interfaces;
using Application.Interfaces.Repositories;

using MediatR;

namespace Application.Messages.Commands.Friends;

using Application.Exceptions.Friends;

using Domain;

public class RemoveFriendCommand(string userLogin, IClient client) : ICommand
{
    public string UserLogin { get; set; } = userLogin;
    public IClient Client { get; set; } = client;
}

public class RemoveFriendCommandHandler(IUserRepository userRepository, IFriendshipRepository friendshipRepository) : ICommandHandler<RemoveFriendCommand, Unit>
{
    public async Task<Unit> Handle(RemoveFriendCommand request, CancellationToken cancellationToken)
    {
        if (!await userRepository.ExistsByLogin(request.UserLogin))
        {
            throw new UserNotExistingException("User not found");
        }

        User user = await userRepository.GetByLogin(request.UserLogin);

        if (!await friendshipRepository.Exist(user.Id, request.Client.User!.Id))
        {
            throw new NotInFriendListException("Users are not friends");
        }

        await friendshipRepository.RemoveFriendship(user.Id, request.Client.User!.Id);
        
        return Unit.Value;
    }
}
