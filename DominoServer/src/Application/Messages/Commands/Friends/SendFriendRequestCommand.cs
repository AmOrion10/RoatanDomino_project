using Application.Common;
using Application.Exceptions.Connection;
using Application.Exceptions.Friends;
using Application.Interfaces;
using Application.Interfaces.Repositories;

using MediatR;

namespace Application.Messages.Commands.Friends;

using Domain;

public class SendFriendRequestCommand(string userLogin, IClient client) : ICommand
{
    public string UserLogin { get; set; } = userLogin;
    public IClient Client { get; set; } = client;
}

public class SendFriendRequestCommandHandler(IUserRepository userRepository, IFriendRequestRepository friendRequestRepository, IFriendshipRepository friendshipRepository) : ICommandHandler<SendFriendRequestCommand, Unit>
{
    public async Task<Unit> Handle(SendFriendRequestCommand request, CancellationToken cancellationToken)
    {
        if (!await userRepository.ExistsByLogin(request.UserLogin))
        {
            throw new UserNotExistingException("User not found");
        }

        User user = await userRepository.GetByLogin(request.UserLogin);

        if (await friendshipRepository.Exist(request.Client.User!.Id, user.Id))
        {
            throw new AlreadyFriendException("Users are already friend");
        }

        if (await friendRequestRepository.Exist(userIdApplicant: user.Id, userIdAsked: request.Client.User!.Id))
        {
            await friendshipRepository.CreateFriendship(user.Id, request.Client.User!.Id);
            await friendRequestRepository.DeleteRequest(userIdApplicant: user.Id, userIdAsked: request.Client.User!.Id);
        }
        else
        {
            await friendRequestRepository.AddFriendRequest(userIdAsked: user.Id, userIdApplicant: request.Client.User!.Id);
        }
        return Unit.Value;
    }
}