using Domain;

namespace Application.Interfaces.Repositories;

public interface IFriendshipRepository
{
    Task CreateFriendship(long userId1, long userId2);
    Task<bool> Exist(long userId1, long userId2);
    Task RemoveFriendship(long userId1, long userId2);
    Task<List<Friend>> GetAllForUser(long userId);
}
