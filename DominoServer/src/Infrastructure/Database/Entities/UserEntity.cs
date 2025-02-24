using Domain;
using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Database.Entities;

public class UserEntity
{
    public long Id { get; set; }

    public string Login { get; set; }

    public string Password { get; set; }

    public string Email { get; set; }

    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime DateSignup { get; set; }

    public DateTime? DateLastLogin { get; set; }

    public long? AvatarId { get; set; }

    public int Score { get; set; }

    public DateOnly Birthdate { get; set; }

    public long? NationalityId { get; set; }

    public long? RankId { get; set; }

    public virtual ICollection<BanEntity> Bans { get; set; } = [];
    public virtual ICollection<ReportEntity> Reports { get; set; } = [];

    public virtual ICollection<ChatEntity> ChatRecipients { get; set; } = [];
    public virtual ICollection<ChatEntity> ChatUsers { get; set; } = [];

    public virtual NationalityEntity? Nationality { get; set; }

    public virtual AvatarEntity? Avatar { get; set; }

    public virtual RankEntity? Rank { get; set; }

    public virtual ICollection<GameEntity> Games { get; set; } = [];

    public virtual ICollection<UserGameEntity> UserGames { get; set; } = [];

    public virtual ICollection<UserRoleEntity> UserRoles { get; set; } = [];

    public virtual ICollection<RoleEntity> Roles { get; set; } = [];

    public virtual ICollection<UserTournamentEntity> UserTournaments { get; set; } = [];

    public virtual ICollection<FriendshipEntity> FriendshipFriends { get; set; } = [];
    public virtual ICollection<FriendshipEntity> FriendshipUsers { get; set; } = [];
    public virtual ICollection<FriendRequestEntity> FriendRequestsAsked { get; set; } = [];
    public virtual ICollection<FriendRequestEntity> FriendRequestsApplicant { get; set; } = [];
    public virtual ICollection<GameEntity> GamesWon { get; set; } = [];

    public User ToDomainEntity()
    {
        return new User(Email, Login)
        {
            Id = Id,
            Score = Score,
            DateSignup = DateSignup,
            Birthdate = Birthdate,
            Nationality = NationalityId is null ? null : Nationality!.Name,
        };
    }
}
