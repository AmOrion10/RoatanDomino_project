using Application.Interfaces.Repositories;

using Infrastructure.Database;
using Infrastructure.Database.Entities;

using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services.Repositories;

public class ResetPasswordCodeRepository(ApplicationDbContext dbContext) : IResetPasswordCodeRepository
{
    public async Task Create(string code, string email)
    {
        ResetPasswordCodeEntity entity = new()
        {
            Code = code,
            EmailAddress = email,
            DateGenerated = DateTime.Now
        };
        await dbContext.ResetPasswordCode.AddAsync(entity);
        await dbContext.SaveChangesAsync();
    }

    public async Task<string?> GetLastForEmail(string email)
    {
        return (await dbContext.ResetPasswordCode
            .OrderByDescending(r => r.DateGenerated)
            .FirstOrDefaultAsync(r => r.EmailAddress.Equals(email)))
            ?.Code;
    }
}