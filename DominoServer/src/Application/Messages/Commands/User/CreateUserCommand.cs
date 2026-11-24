using System.Globalization;

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
    private static readonly string[] AcceptedDateFormats =
    [
        "MM/dd/yyyy",
        "M/d/yyyy",
        Constants.ShortFormatDate,
        "d/M/yyyy",
        "yyyy-MM-dd"
    ];

    public async Task<Unit> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParseExact(request.Birthdate?.Trim(), AcceptedDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly birthdate))
        {
            throw new InvalidDateFormatException("Please enter a valid date of birth (MM/DD/YYYY).");
        }

        if (await userRepository.ExistsByEmail(request.Email))
        {
            throw new UserAlreadyExistException("An account with this email address already exists.");
        }

        if (await userRepository.ExistsByLogin(request.Login))
        {
            throw new UserAlreadyExistException("That username is already taken. Please choose another.");
        }

        User user = new(request.Email, request.Login);
        await userRepository.Create(user, request.Password, birthdate);

        return Unit.Value;
    }
}