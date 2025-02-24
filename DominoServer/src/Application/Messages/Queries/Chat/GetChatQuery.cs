using Application.Common;
using Application.Exceptions.Chat;
using Application.Interfaces.Managers;

namespace Application.Messages.Queries.Chat;

using Domain;

public class GetChatQuery(int chatId) : IQuery<Chat>
{
    public int ChatId { get; set; } = chatId;
}

public class GetChatQueryHandler(IChatManager chatManager) : IQueryHandler<GetChatQuery, Chat>
{
    public Task<Chat> Handle(GetChatQuery request, CancellationToken cancellationToken)
    {
        Chat chat = chatManager.GetById(request.ChatId) ?? throw new ChatNotFoundException($"Chat with id {request.ChatId} not found");
        return Task.FromResult(chat);
    }
}