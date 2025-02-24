using Domain;

namespace Application.Interfaces.Repositories;

public interface IEmailSenderRepository
{
    Task<EmailSended?> GetLastByEmail(string email, EmailType type);
    Task Create(string email, EmailType type);
}