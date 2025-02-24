using Application.Common;
using Application.Interfaces;
using Application.Interfaces.Managers;

namespace Application.Messages.Queries.Chat;

public class GetAllClientsForChatQuery(int chatId) : IQuery<List<IClient>>
{
    public int ChatId { get; set; } = chatId;
}

public class GetAllClientsForChatQueryHandler(IChatManager chatManager, IClientManager clientManager) : IQueryHandler<GetAllClientsForChatQuery, List<IClient>>
{
    public Task<List<IClient>> Handle(GetAllClientsForChatQuery request, CancellationToken cancellationToken)
    {
        List<IClient> clients = clientManager.GetAllLoggedInClients();
        List<IClient> clientInChat = [];
        foreach (IClient client in clients)
        {
            if (chatManager.ClientIsInChat(client, request.ChatId))
            {
                clientInChat.Add(client);
            }
        }
        return Task.FromResult(clientInChat);
    }
}