using DominoCommon.com.domino.common.Scripts.Enums;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Teams;

namespace Infrastructure.Database.Entities;

public class TeamChallengeEntity
{
    public long Id { get; set; }

    public long ChallengerTeamId { get; set; }

    public long? TargetTeamId { get; set; }

    public MatchFormat GameMode { get; set; } = MatchFormat.SIX_LOV;

    public TableStyle Variant { get; set; } = TableStyle.DRAW;

    public int TableId { get; set; }

    public string TableCode { get; set; } = string.Empty;

    public TeamChallengeStatus Status { get; set; } = TeamChallengeStatus.Pending;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public bool IsOpenBoardChallenge { get; set; }

    public int MinOpponentTeamElo { get; set; } = RoatanRankTierHelper.MinElo;

    public bool RequireFullHumanSquad { get; set; }

    public DateTime? ExpiresUtc { get; set; }

    public DateTime? PlayDeadlineUtc { get; set; }

    public bool TeamACheckedIn { get; set; }

    public bool TeamBCheckedIn { get; set; }

    public virtual TeamEntity ChallengerTeam { get; set; } = null!;

    public virtual TeamEntity? TargetTeam { get; set; }

    public virtual ICollection<TeamChallengeEndorsementEntity> Endorsements { get; set; } = [];
}

public class TeamChallengeEndorsementEntity
{
    public long Id { get; set; }

    public long ChallengeId { get; set; }

    public long TeamId { get; set; }

    public long UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public virtual TeamChallengeEntity Challenge { get; set; } = null!;
}
