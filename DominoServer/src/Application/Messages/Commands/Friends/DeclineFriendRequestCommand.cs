using Application.Common;
using Application.Exceptions.Connection;
using Application.Exceptions.Friends;
using Application.Interfaces;
using Application.Interfaces.Repositories;

using MediatR;

namespace Application.Messages.Commands.Friends;

using Domain;

public class DeclineFriendRequestCommand(string userLogin, IClient client) : ICommand
{
    public string UserLogin { get; set; } = userLogin;
    public IClient Client { get; set; } = client;
}

public class DeclineFriendRequestCommandHandler(IUserRepository userRepository, IFriendRequestRepository friendRequestRepository) : ICommandHandler<DeclineFriendRequestCommand, Unit>
{
    public async Task<Unit> Handle(DeclineFriendRequestCommand request, CancellationToken cancellationToken)
    {
        if (!await userRepository.ExistsByLogin(request.UserLogin))
        {
            throw new UserNotExistingException("User not found");
        }

        User user = await userRepository.GetByLogin(request.UserLogin);

        if (!await friendRequestRepository.Exist(userIdApplicant: user.Id, userIdAsked: request.Client.User!.Id))
        {
            throw new FriendRequestNotFoundException("Friend request not found");
        }

        await friendRequestRepository.DeleteRequest(userIdApplicant: user.Id, userIdAsked: request.Client.User!.Id);

        return Unit.Value;
    }
}