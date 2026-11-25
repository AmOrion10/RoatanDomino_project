using DominoCommon.com.domino.common.Scripts.Enums;

namespace Infrastructure.Database.Entities;

public class TeamMemberEntity
{
    public long TeamId { get; set; }

    public long UserId { get; set; }

    public TeamRole Role { get; set; } = TeamRole.Teammate;

    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;

    public virtual TeamEntity Team { get; set; } = null!;

    public virtual UserEntity User { get; set; } = null!;
}
