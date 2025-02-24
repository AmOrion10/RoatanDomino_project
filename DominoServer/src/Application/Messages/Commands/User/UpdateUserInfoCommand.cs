using Application.Common;
using Application.Interfaces;
using Application.Interfaces.Repositories;

using MediatR;

namespace Application.Messages.Commands.User;

using Domain;
using Application.Exceptions.Connection;
using Application.Exceptions.User;
using DominoCommon.com.domino.common.Scripts;

public class UpdateUserInfoCommand : ICommand<Unit>
{
    public required string Login { get; set; }
    public required string Birthdate { get; set; }
    public required string? Nationality { get; set; }
    public required IClient Client { get; set; }
}

public class UpdateUserInfoCommandHandler(IUserRepository userRepository) : ICommandHandler<UpdateUserInfoCommand, Unit>
{
    public async Task<Unit> Handle(UpdateUserInfoCommand request, CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParseExact(request.Birthdate, Constants.ShortFormatDate, out DateOnly birthdate))
        {
            throw new InvalidDateFormatException("The birthdate has an invalid format");
        }

        User updatedUser = request.Client.User!;
        if (!updatedUser.Login.Equals(request.Login) && await userRepository.ExistsByLogin(request.Login))
        {
            throw new UserAlreadyExistException("User with this login already exists");
        }

        updatedUser.Login = request.Login;
        updatedUser.Birthdate = birthdate;
        updatedUser.Nationality = request.Nationality;

        await userRepository.UpdateProfile(updatedUser);
        return Unit.Value;
    }
}
