namespace Infrastructure.Database.Entities;

public class TeamInviteEntity
{
    public long Id { get; set; }

    public long TeamId { get; set; }

    public long InvitedUserId { get; set; }

    public long InvitedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public virtual TeamEntity Team { get; set; } = null!;

    public virtual UserEntity InvitedUser { get; set; } = null!;

    public virtual UserEntity InvitedByUser { get; set; } = null!;
}
