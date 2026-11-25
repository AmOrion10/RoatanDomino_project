using Domain;
using DominoCommon.com.domino.common.Scripts.Enums;
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

    public string? AvatarUrl { get; set; }

    public string? AuthProvider { get; set; }

    public int Score { get; set; }

    public int Elo { get; set; } = RoatanRankTierHelper.DefaultElo;

    public int Wins { get; set; }

    public int Losses { get; set; }

    public int WinAssists { get; set; }

    public int TotalMoves { get; set; }

    public int OptimalMoves { get; set; }

    public double AverageTurnSeconds { get; set; }

    public int OnlineMatchesLast7Days { get; set; }

    public DateTime? LastOnlineMatchUtc { get; set; }

    public int AbandonedMatches { get; set; }

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
        int resolvedElo = Elo >= RoatanRankTierHelper.MinElo ? Elo : RoatanRankTierHelper.DefaultElo;
        return new User(Email, Login)
        {
            Id = Id,
            Score = Score,
            Elo = resolvedElo,
            Wins = Wins,
            Losses = Losses,
            WinAssists = WinAssists,
            TotalMoves = TotalMoves,
            OptimalMoves = OptimalMoves,
            AverageTurnSeconds = AverageTurnSeconds,
            OnlineMatchesLast7Days = OnlineMatchesLast7Days,
            LastOnlineMatchUtc = LastOnlineMatchUtc,
            AbandonedMatches = AbandonedMatches,
            DateSignup = DateSignup,
            Birthdate = Birthdate,
            Nationality = NationalityId is null ? null : Nationality?.Name,
            AvatarUrl = AvatarUrl,
            AvatarBase64 = Avatar is not null && Avatar.Image is { Length: > 0 } ? Convert.ToBase64String(Avatar.Image) : null,
            AuthProvider = string.IsNullOrWhiteSpace(AuthProvider) ? "Email" : AuthProvider
        };
    }
}
