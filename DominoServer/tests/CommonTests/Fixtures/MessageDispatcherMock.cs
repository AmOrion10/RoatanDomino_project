using Application.Common;

namespace CommonTests.Fixtures;

public class MessageDispatcherMock : IMessageDispatcher
{
    public List<Type> CommandsCalled { get; } = [];

    public Task<TResponse> Dispatch<TResponse>(ICommand<TResponse> command)
    {
        CommandsCalled.Add(command.GetType());
        return Task.FromResult(default(TResponse))!;
    }

    public Task<TResponse> Dispatch<TResponse>(IQuery<TResponse> query)
    {
        return Task.FromResult(default(TResponse))!;
    }
}
