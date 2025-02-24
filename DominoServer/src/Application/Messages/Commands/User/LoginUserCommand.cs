using Application.Common;
using Application.Exceptions.Connection;
using Application.Interfaces;
using Application.Interfaces.Repositories;

namespace Application.Messages.Commands.User;

using Application.Interfaces.Managers;

using Domain;

public class LoginUserCommand : ICommand<Tuple<User, Guid>>
{
    public required string Login { get; set; }
    public required string Password { get; set; }
    public required IClient Client { get; set; }
}

public class LoginUserCommandHandler(IUserRepository userRepository, IClientManager clientManager, ITokenManager tokenManager, IChatManager chatManager) : ICommandHandler<LoginUserCommand, Tuple<User, Guid>>
{
    public async Task<Tuple<User, Guid>> Handle(LoginUserCommand request, CancellationToken cancellationToken)
    {
        if (request.Client.IsLoggedIn)
        {
            throw new ClientAlreadyLoggedInException("Client is already logged in");
        }

        if (!await userRepository.ExistsByLogin(request.Login))
        {
            throw new UserNotExistingException("No user with this login exists");
        }

        if (AnotherClientIsLoggedInWithTheSameIdentifiers(request))
        {
            throw new ClientAlreadyLoggedInException("Another client is already logged in with the same identifiers");
        }

        User user = await userRepository.Login(request.Login, request.Password) ?? throw new WrongPasswordException("The password for the user is not correct");

        Guid guid = tokenManager.GenerateTokenForClient(request.Client);
        request.Client.User = user;
        request.Client.IsLoggedIn = true;

        chatManager.ClientJoinChat(request.Client, chatManager.GlobalChatId);

        return new Tuple<User, Guid>(user, guid);
    }

    private bool AnotherClientIsLoggedInWithTheSameIdentifiers(LoginUserCommand request)
    {
        List<IClient> clients = clientManager.GetAllClients();
        foreach (IClient client in clients)
        {
            if (client.Id == request.Client.Id || !client.IsLoggedIn || !client.User!.Login.Equals(request.Login))
            {
                continue;
            }

            return true;
        }

        return false;
    }
}