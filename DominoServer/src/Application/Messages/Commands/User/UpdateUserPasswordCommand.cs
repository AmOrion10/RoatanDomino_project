using Application.Common;
using Application.Interfaces;
using Application.Interfaces.Repositories;

using MediatR;

namespace Application.Messages.Commands.User;

using Domain;

public class UpdateUserPasswordCommand : ICommand<Unit>
{
    public required string ActualPassword { get; set; }
    public required string NewPassword { get; set; }
    public required IClient Client { get; set; }
}

public class UpdateUserPasswordCommandHandler(IUserRepository userRepository) : ICommandHandler<UpdateUserPasswordCommand, Unit>
{
    public async Task<Unit> Handle(UpdateUserPasswordCommand request, CancellationToken cancellationToken)
    {
        await userRepository.UpdatePassword(request.Client.User!, request.ActualPassword, request.NewPassword);
        return Unit.Value;
    }
}
