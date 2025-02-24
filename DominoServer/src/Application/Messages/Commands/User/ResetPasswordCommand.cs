using Application.Common;
using Application.Exceptions.Connection;
using Application.Exceptions.User;
using Application.Interfaces.Repositories;

using MediatR;

namespace Application.Messages.Commands.User;

using Domain;

public class ResetPasswordCommand(string code, string password, string email) : ICommand
{
    public string Code { get; set; } = code;
    public string Password { get; set; } = password;
    public string Email { get; set; } = email;
}

public class ResetPasswordCommandHandler(IResetPasswordCodeRepository resetPasswordCodeRepository, IUserRepository userRepository) : ICommandHandler<ResetPasswordCommand, Unit>
{
    public async Task<Unit> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        string? code = await resetPasswordCodeRepository.GetLastForEmail(request.Email);
        if (code is null || !code.Equals(request.Code))
        {
            throw new InvalidCodeException("Reset code was not the correct one");
        }

        User user = await userRepository.GetByEmail(request.Email) ?? throw new UserNotExistingException($"User with email {request.Email} not found");
        await userRepository.ChangePassword(user.Id, request.Password);

        return Unit.Value;
    }
}
