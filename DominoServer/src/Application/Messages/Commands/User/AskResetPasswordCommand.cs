using Application.Common;
using Application.Exceptions.Connection;
using Application.Exceptions.User;
using Application.Interfaces;
using Application.Interfaces.Repositories;

namespace Application.Messages.Commands.User;

using Domain;

public class AskResetPasswordCommand(string email) : ICommand<AskResetPasswordResult>
{
    public string Email { get; set; } = email;
}

public class AskResetPasswordCommandHandler(IEmailSenderRepository emailSenderRepository, IResetPasswordCodeRepository resetPasswordCodeRepository, ICodeGenerator codeGenerator, IUserRepository userRepository) : ICommandHandler<AskResetPasswordCommand, AskResetPasswordResult>
{
    private const int SecondsBetweenEmails = 60;

    public async Task<AskResetPasswordResult> Handle(AskResetPasswordCommand request, CancellationToken cancellationToken)
    {
        AskResetPasswordResult result = new(request.Email);

        User user = await userRepository.GetByEmail(request.Email) ?? throw new UserNotExistingException($"User with email {request.Email} not found");
        result.Login = user.Login;

        EmailSended? email = await emailSenderRepository.GetLastByEmail(request.Email, EmailType.ResetPassword);
        if (email is null || email.DateSended < DateTime.Now.AddSeconds(-SecondsBetweenEmails))
        {
            string code = codeGenerator.GenerateCodeForResetPassword();
            result.Code = code;
            await resetPasswordCodeRepository.Create(code, request.Email);
        }
        else
        {
            throw new EmailAlreadySentException("Reset password email was sent a short time ago");
        }
        return result;
    }
}