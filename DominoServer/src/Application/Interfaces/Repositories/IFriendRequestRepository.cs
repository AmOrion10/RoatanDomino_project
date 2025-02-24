using Domain;

namespace Application.Interfaces.Repositories;

public interface IFriendRequestRepository
{
    Task AddFriendRequest(long userIdAsked, long userIdApplicant);
    Task<bool> Exist(long userIdApplicant, long userIdAsked);
    Task DeleteRequest(long userIdApplicant, long userIdAsked);
    Task<List<FriendRequest>> GetAllForUserAsked(long userIdAsked);
}
