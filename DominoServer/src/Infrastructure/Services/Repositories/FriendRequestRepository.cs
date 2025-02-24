using Application.Interfaces.Repositories;
using Domain;
using Infrastructure.Database;
using Infrastructure.Database.Entities;

using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services.Repositories;

public class FriendRequestRepository(ApplicationDbContext dbContext) : IFriendRequestRepository
{
    public async Task AddFriendRequest(long userIdAsked, long userIdApplicant)
    {
        await dbContext.FriendRequests.AddAsync(new FriendRequestEntity()
        {
            UserIdAsked = userIdAsked,
            UserIdApplicant = userIdApplicant
        });
        await dbContext.SaveChangesAsync();
    }

    public async Task<bool> Exist(long userIdApplicant, long userIdAsked)
    {
        return await dbContext.FriendRequests.AnyAsync(f => f.UserIdAsked == userIdAsked && f.UserIdApplicant == userIdApplicant);
    }

    public async Task DeleteRequest(long userIdApplicant, long userIdAsked)
    {
        FriendRequestEntity entity = await dbContext.FriendRequests.FirstAsync(f => f.UserIdAsked == userIdAsked && f.UserIdApplicant == userIdApplicant);
        dbContext.FriendRequests.Remove(entity);
        await dbContext.SaveChangesAsync();
    }

    public async Task<List<FriendRequest>> GetAllForUserAsked(long userIdAsked)
    {
        return (await dbContext.FriendRequests
            .Where(f => f.UserIdAsked == userIdAsked)
            .Include(f => f.UserApplicant)
            .Include(f => f.UserAsked)
            .ToListAsync())
            .Select(fe => fe.ToDomainEntity())
            .ToList();
    }
}
