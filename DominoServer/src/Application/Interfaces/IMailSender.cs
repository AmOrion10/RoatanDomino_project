namespace Application.Interfaces;

public interface IMailSender
{
    Task SendResetPasswordMail(string code, string toEmailAddress, string login);
}