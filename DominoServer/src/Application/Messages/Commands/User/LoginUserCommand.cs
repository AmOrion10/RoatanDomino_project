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

        bool existsByLogin = await userRepository.ExistsByLogin(request.Login);
        bool existsByEmail = !existsByLogin && await userRepository.ExistsByEmail(request.Login);
        if (!existsByLogin && !existsByEmail)
        {
            throw new UserNotExistingException("No account found with that username or email.");
        }

        User user = await userRepository.Login(request.Login, request.Password)
            ?? throw new WrongPasswordException("Incorrect password. Please try again.");

        if (AnotherClientIsLoggedInWithTheSameUser(request.Client.Id, user.Login))
        {
            throw new ClientAlreadyLoggedInException("This account is already signed in on another device.");
        }

        Guid guid = tokenManager.GenerateTokenForClient(request.Client);
        request.Client.User = user;
        request.Client.IsLoggedIn = true;

        chatManager.ClientJoinChat(request.Client, chatManager.GlobalChatId);

        return new Tuple<User, Guid>(user, guid);
    }

    private bool AnotherClientIsLoggedInWithTheSameUser(int currentClientId, string login)
    {
        List<IClient> clients = clientManager.GetAllClients();
        foreach (IClient client in clients)
        {
            if (client.Id == currentClientId || !client.IsLoggedIn || client.User is null || !client.User.Login.Equals(login, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return true;
        }

        return false;
    }
}