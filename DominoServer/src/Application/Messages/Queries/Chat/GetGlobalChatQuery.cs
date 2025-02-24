using Application.Common;
using Application.Interfaces.Managers;

namespace Application.Messages.Queries.Chat;

using Domain;

public class GetGlobalChatQuery : IQuery<Chat>
{
}

public class GetGlobalChatQueryHandler(IChatManager chatManager) : IQueryHandler<GetGlobalChatQuery, Chat>
{
    public Task<Chat> Handle(GetGlobalChatQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(chatManager.GetById(chatManager.GlobalChatId)!);
    }
}