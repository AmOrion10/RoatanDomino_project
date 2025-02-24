using Application.Common;
using Application.Exceptions.Chat;
using Application.Interfaces;
using Application.Interfaces.Managers;
using Application.Messages.Queries.Game;

using Domain;

namespace Application.Messages.Commands.Game;

using Application.Exceptions.Game;

using Domain.Game;

public class ClientsInGameJoinChatCommand(int gameId) : ICommand<Chat>
{
    public int GameId { get; set; } = gameId;
}

public class ClientsInGameJoinChatCommandHandler(IGameManager gamaManager, IMessageDispatcher dispatcher, IChatManager chatManager) : ICommandHandler<ClientsInGameJoinChatCommand, Chat>
{
    public async Task<Chat> Handle(ClientsInGameJoinChatCommand request, CancellationToken cancellationToken)
    {
        Game game = gamaManager.GetById(request.GameId) ?? throw new GameNotFoundException($"Game with id {request.GameId} not found");
        List<IClient> clientsInGame = await dispatcher.Dispatch(new GetAllClientsForGameQuery(request.GameId));
        foreach (IClient client in clientsInGame)
        {
            chatManager.ClientJoinChat(client, game.ChatId);
        }
        return chatManager.GetById(game.ChatId) ?? throw new ChatNotFoundException($"Chat with id {game.ChatId} not found");
    }
}