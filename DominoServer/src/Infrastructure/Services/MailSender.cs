using Application.Exceptions.User;
using Application.Interfaces;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace Infrastructure.Services;

public class MailSender : IMailSender
{
    private const string SmtpServer = "smtp.gmail.com";
    private const int SmtpPort = 587;
    private const string FromEmailAddress = "tiletitansincsup@gmail.com";
    private const string FromName = "Tile Titans Support";
    private const string SubjectResetPassword = "Reset password requested";

    private readonly SmtpClient? smtpClient;
    private readonly MailAddress? from;
    private readonly ILogger logger;

    public MailSender(ILogger<MailSender> logger)
    {
        this.logger = logger;
        string? emailPassword = Environment.GetEnvironmentVariable("EmailPassword");
        if (string.IsNullOrWhiteSpace(emailPassword))
        {
            logger.LogWarning("Can't setup mail sender because missing EmailPassword env var");
            return;
        }

        smtpClient = new SmtpClient(SmtpServer, SmtpPort)
        {
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            EnableSsl = true,
            Credentials = new NetworkCredential(FromEmailAddress, emailPassword)
        };
        from = new MailAddress(FromEmailAddress, FromName);
    }

    public async Task SendResetPasswordMail(string code, string toEmailAddress, string login)
    {
        if (smtpClient is null)
        {
            throw new EmailCouldNotBeSentException("Smtp client is not set");
        }

        try
        {
            MailAddress to = new(toEmailAddress, login);
            MailMessage message = new(from!, to)
            {
                IsBodyHtml = true,
                Subject = SubjectResetPassword,
                Body = GetBodyForResetPasswordMail(code, login)
            };
            await smtpClient.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            logger.LogError("Could not send Reset password mail {ex}", ex);
            throw new EmailCouldNotBeSentException("Error while trying to send email");
        }
    }

    private static string GetBodyForResetPasswordMail(string code, string login)
    {
        StringBuilder sb = new("""<!DOCTYPE html><html lang="en"><body>""");
        sb.Append($"<h1>Hello {login},</h1>");
        sb.Append("<h3>You have requested to reset your password</h3>");
        sb.Append($"<p id=\"p\">If you want to reset your password, please fill in the in-game form with the code <strong><span id=\"code\">{code}</span></strong> and the new password.</p>");
        sb.Append("<style>#p{ font-size:18px } #code{ background-color:rgb(213,244,243); padding:5px 1em 5px 1em;}</style>");
        sb.Append("</body></html>");
        return sb.ToString();
    }
}