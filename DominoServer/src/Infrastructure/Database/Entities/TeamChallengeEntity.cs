using DominoCommon.com.domino.common.Scripts.Enums;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Teams;

namespace Infrastructure.Database.Entities;

public class TeamChallengeEntity
{
    public long Id { get; set; }

    public long ChallengerTeamId { get; set; }

    public long TargetTeamId { get; set; }

    public MatchFormat GameMode { get; set; } = MatchFormat.SIX_LOV;

    public TableStyle Variant { get; set; } = TableStyle.DRAW;

    public int TableId { get; set; }

    public string TableCode { get; set; } = string.Empty;

    public TeamChallengeStatus Status { get; set; } = TeamChallengeStatus.Pending;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public virtual TeamEntity ChallengerTeam { get; set; } = null!;

    public virtual TeamEntity TargetTeam { get; set; } = null!;
}
