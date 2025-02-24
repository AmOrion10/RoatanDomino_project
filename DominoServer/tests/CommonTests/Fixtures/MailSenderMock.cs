using Application.Interfaces;

namespace CommonTests.Fixtures;

public class MailSenderMock : IMailSender
{
    public List<string> MailsSended { get; } = [];

    public Task SendResetPasswordMail(string code, string toEmailAddress, string login)
    {
        MailsSended.Add(toEmailAddress);
        return Task.CompletedTask;
    }
}