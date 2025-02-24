using MediatR;

namespace Application.Common;

public class MessageDispatcher(IMediator mediator) : IMessageDispatcher
{
    public Task<TResponse> Dispatch<TResponse>(ICommand<TResponse> command)
    {
        return mediator.Send(command);
    }

    public Task<TResponse> Dispatch<TResponse>(IQuery<TResponse> query)
    {
        return mediator.Send(query);
    }
}
