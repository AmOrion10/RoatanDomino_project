using Domain;

namespace Infrastructure.Database.Entities;

public class FriendRequestEntity
{
    public long UserIdApplicant { get; set; } // The one who is the applicant
    public long UserIdAsked { get; set; } // The one who is asked

    public virtual UserEntity UserApplicant { get; set; } = null!;
    public virtual UserEntity UserAsked { get; set; } = null!;

    public FriendRequest ToDomainEntity()
    {
        return new FriendRequest
        {
            UserIdApplicant = UserIdApplicant,
            UserIdAsked = UserIdAsked,
            UserApplicantName = UserApplicant.Login,
            UserAskedName = UserAsked.Login
        };
    }
}
