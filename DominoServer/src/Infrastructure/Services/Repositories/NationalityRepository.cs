using Application.Interfaces.Repositories;

using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services.Repositories;

public class NationalityRepository(ApplicationDbContext dbContext) : INationalityRepository
{
    public async Task<long?> GetByName(string name)
    {
        return (await dbContext.Nationalities.FirstOrDefaultAsync(n => n.Name.Equals(name)))?.Id;
    }
}
