namespace Infrastructure.Database.Entities;

public class TeamJoinRequestEntity
{
    public long Id { get; set; }

    public long TeamId { get; set; }

    public long UserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public virtual TeamEntity Team { get; set; } = null!;

    public virtual UserEntity User { get; set; } = null!;
}
