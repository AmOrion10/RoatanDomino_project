using Application.Common;
using Application.Exceptions.Connection;
using Application.Exceptions.Friends;
using Application.Interfaces;
using Application.Interfaces.Repositories;

using MediatR;

namespace Application.Messages.Commands.Friends;

using Domain;

public class AcceptFriendRequestCommand(string userLogin, IClient client) : ICommand
{
    public string UserLogin { get; set; } = userLogin;
    public IClient Client { get; set; } = client;
}

public class AcceptFriendRequestCommandHandler(IUserRepository userRepository, IFriendRequestRepository friendRequestRepository, IFriendshipRepository friendshipRepository) : ICommandHandler<AcceptFriendRequestCommand, Unit>
{
    public async Task<Unit> Handle(AcceptFriendRequestCommand request, CancellationToken cancellationToken)
    {
        if (!await userRepository.ExistsByLogin(request.UserLogin))
        {
            throw new UserNotExistingException("User not found");
        }

        User user = await userRepository.GetByLogin(request.UserLogin);
        if (! await friendRequestRepository.Exist(userIdApplicant: user.Id, userIdAsked: request.Client.User!.Id))
        {
            throw new FriendRequestNotFoundException("Friend request was not found");
        }

        await friendshipRepository.CreateFriendship(user.Id, request.Client.User!.Id);
        await friendRequestRepository.DeleteRequest(userIdApplicant: user.Id, userIdAsked: request.Client.User!.Id);

        return Unit.Value;
    }
}