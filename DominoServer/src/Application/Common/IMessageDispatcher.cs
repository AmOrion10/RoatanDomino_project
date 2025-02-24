namespace Application.Common;

public interface IMessageDispatcher : ICommandDispatcher, IQueryDispatcher
{

}

public interface IQueryDispatcher
{
    Task<TResponse> Dispatch<TResponse>(IQuery<TResponse> query);
}

public interface ICommandDispatcher
{
    Task<TResponse> Dispatch<TResponse>(ICommand<TResponse> command);
}