using Application.Common;
using Application.Exceptions.Chat;
using Application.Interfaces;
using Application.Interfaces.Managers;
using Application.Messages.Commands.Game;

namespace Application.Messages.Commands.User;

using Domain;
using Domain.Game;

public class LogoutUserCommand(IClient client) : ICommand<DisconnectionResult>
{
    public IClient Client { get; set; } = client;
}

public class LogoutUserCommandHandler(IMessageDispatcher dispatcher, ITokenManager tokenManager, ILobbyManager lobbyManager, IChatManager chatManager, IGameManager gameManager) : ICommandHandler<LogoutUserCommand, DisconnectionResult>
{
    public async Task<DisconnectionResult> Handle(LogoutUserCommand request, CancellationToken cancellationToken)
    {
        DisconnectionResult result = new(request.Client.User);

        if (!request.Client.IsLoggedIn)
        {
            return result;
        }

        Lobby? lobbyClient = lobbyManager.GetLobbyForClient(request.Client);
        if (lobbyClient is not null)
        {
            lobbyManager.RemovePlayerFromLobby(request.Client, lobbyClient.Id);
            if (lobbyManager.ExistById(lobbyClient.Id))
            {
                result.LobbyId = lobbyClient.Id;
            }
        }

        List<Chat> chatsClient = chatManager.GetChatsForClient(request.Client);
        foreach (Chat chat in chatsClient)
        {
            chatManager.ClientLeaveChat(request.Client, chat.Id);
        }

        Game? game = gameManager.GetForClient(request.Client);
        if (game is not null)
        {
            result.LeaveGameResult = await dispatcher.Dispatch(new LeaveGameCommand(request.Client));
        }

        result.GlobalChat = chatManager.GetById(chatManager.GlobalChatId) ?? throw new ChatNotFoundException($"Chat with id {chatManager.GlobalChatId} not found");

        tokenManager.RemoveTokenForClientIfPresent(request.Client);
        request.Client.IsLoggedIn = false;
        //request.Client.User = null; // Is it worth it to set it to null ?
        return result;
    }
}