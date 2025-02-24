using MediatR;

namespace Application.Common;

public interface ICommand : IRequest, ICommand<Unit>
{
    
}

public interface ICommand<out TResponse> : IRequest<TResponse>
{

}

public interface IQuery<out TResponse> : IRequest<TResponse>
{

}