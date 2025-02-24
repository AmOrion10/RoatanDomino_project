namespace Infrastructure.Database.Entities;

public class UserTournamentEntity
{
    public long TournamentId { get; set; }

    public long UserId { get; set; }

    public virtual TournamentEntity Tournament { get; set; } = null!;

    public virtual UserEntity User { get; set; } = null!;
}
