using Application;
using Application.Interfaces;
using Application.Interfaces.Repositories;

using Domain;

namespace Infrastructure.Services.Managers;

public class ResetPasswordManager(IMailSender mailSender, IEmailSenderRepository emailSenderRepository)
{
    public async Task Process(AskResetPasswordResult result)
    {
        if (result.Code is not null)
        {
            await mailSender.SendResetPasswordMail(result.Code, result.Email, result.Login!);
            await emailSenderRepository.Create(result.Email, EmailType.ResetPassword);
        }
    }
}