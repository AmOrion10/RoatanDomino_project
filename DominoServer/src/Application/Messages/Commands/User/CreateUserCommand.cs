using Application.Common;
using Application.Exceptions.Connection;
using Application.Interfaces.Repositories;

using MediatR;

namespace Application.Messages.Commands.User;

using Application.Exceptions.User;
using Domain;
using DominoCommon.com.domino.common.Scripts;

public class CreateUserCommand : ICommand
{
    public required string Email { get; set; }
    public required string Password { get; set; }
    public required string Login { get; set; }
    public required string Birthdate { get; set; }
}

public class CreateUserCommandHandler(IUserRepository userRepository) : ICommandHandler<CreateUserCommand, Unit>
{
    public async Task<Unit> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParseExact(request.Birthdate, Constants.ShortFormatDate, out DateOnly birthdate))
        {
            throw new InvalidDateFormatException("The birthdate has an invalid format");
        }

        if (await userRepository.ExistsByEmail(request.Email))
        {
            throw new UserAlreadyExistException("User with this email already exists");
        }

        if (await userRepository.ExistsByLogin(request.Login))
        {
            throw new UserAlreadyExistException("User with this login already exists");
        }

        User user = new(request.Email, request.Login);
        await userRepository.Create(user, request.Password, birthdate);

        return Unit.Value;
    }
}