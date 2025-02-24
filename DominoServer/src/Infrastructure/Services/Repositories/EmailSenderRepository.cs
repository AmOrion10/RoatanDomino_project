using Application.Interfaces.Repositories;

using Domain;

using Infrastructure.Database;
using Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services.Repositories;

public class EmailSenderRepository(ApplicationDbContext dbContext) : IEmailSenderRepository
{
    public async Task<EmailSended?> GetLastByEmail(string email, EmailType type)
    {
        return (await dbContext.EmailSended
                .OrderByDescending(e => e.DateSended)
                .FirstOrDefaultAsync(e => e.EmailType == type && e.ToEmailAddress.Equals(email)))
                ?.ToDomainEntity();
    }

    public async Task Create(string email, EmailType type)
    {
        EmailSendedEntity entity = new()
        {
            DateSended = DateTime.Now,
            EmailType = type,
            ToEmailAddress = email
        };
        await dbContext.AddAsync(entity);
        await dbContext.SaveChangesAsync();
    }
}