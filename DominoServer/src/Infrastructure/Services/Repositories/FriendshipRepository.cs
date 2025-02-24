using Application.Interfaces.Repositories;

using Domain;

using Infrastructure.Database;
using Infrastructure.Database.Entities;

using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services.Repositories;

public class FriendshipRepository(ApplicationDbContext dbContext) : IFriendshipRepository
{
    public async Task CreateFriendship(long userId1, long userId2)
    {
        await dbContext.Friendships.AddRangeAsync([
            new FriendshipEntity
            {
                UserId = userId1,
                FriendId = userId2
            },
            new FriendshipEntity
            {
                UserId = userId2,
                FriendId = userId1
            }
        ]);
        await dbContext.SaveChangesAsync();
    }

    public async Task<bool> Exist(long userId1, long userId2)
    {
        return await dbContext.Friendships.AnyAsync(f => f.UserId == userId1 && f.FriendId == userId2);
    }

    public async Task RemoveFriendship(long userId1, long userId2)
    {
        List<FriendshipEntity> friendships =
        [
            await dbContext.Friendships.FirstAsync(f => f.UserId == userId1 && f.FriendId == userId2),
            await dbContext.Friendships.FirstAsync(f => f.UserId == userId2 && f.FriendId == userId1)
        ];
        dbContext.Friendships.RemoveRange(friendships);
        await dbContext.SaveChangesAsync();
    }

    public async Task<List<Friend>> GetAllForUser(long userId)
    {
        return (await dbContext.Friendships
            .Where(f => f.UserId == userId)
            .Include(f => f.Friend)
            .ToListAsync())
            .Select(f => f.ToDomainEntity())
            .ToList();
    }
}
