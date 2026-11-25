using DominoCommon.com.domino.common.Scripts.Enums;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Teams;

namespace Infrastructure.Database.Entities;

public class TeamEntity
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Tag { get; set; } = string.Empty;

    public int TeamElo { get; set; } = RoatanRankTierHelper.DefaultElo;

    public int TeamWins { get; set; }

    public int TeamLosses { get; set; }

    public int MaxMembers { get; set; } = TeamInfoDto.DefaultMaxMembers;

    public TeamJoinPolicy JoinPolicy { get; set; } = TeamJoinPolicy.Open;

    public int MinElo { get; set; } = RoatanRankTierHelper.MinElo;

    public TeamActivityRequirement ActivityRequirement { get; set; } = TeamActivityRequirement.Any;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public virtual ICollection<TeamMemberEntity> Members { get; set; } = [];

    public virtual ICollection<TeamJoinRequestEntity> JoinRequests { get; set; } = [];

    public virtual ICollection<TeamInviteEntity> Invites { get; set; } = [];
}
