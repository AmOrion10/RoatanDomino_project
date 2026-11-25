using System;
using System.Collections.Generic;

using DominoCommon.com.domino.common.Scripts.Enums;

using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Teams
{
    public class TeamInfoDto
    {
        public const int DefaultMaxMembers = 12;

        [JsonProperty(PropertyName = "i")]
        public long Id { get; set; }

        [JsonProperty(PropertyName = "n")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "t")]
        public string Tag { get; set; }

        [JsonProperty(PropertyName = "e")]
        public int TeamElo { get; set; } = RoatanRankTierHelper.DefaultElo;

        [JsonProperty(PropertyName = "rt")]
        public RoatanRankTier TeamRankTier { get; set; } = RoatanRankTier.Sailor;

        [JsonProperty(PropertyName = "w")]
        public int TeamWins { get; set; }

        [JsonProperty(PropertyName = "lo")]
        public int TeamLosses { get; set; }

        [JsonProperty(PropertyName = "cu")]
        public string CaptainUsername { get; set; }

        [JsonProperty(PropertyName = "mc")]
        public int MemberCount { get; set; }

        [JsonProperty(PropertyName = "mm")]
        public int MaxMembers { get; set; } = DefaultMaxMembers;

        [JsonProperty(PropertyName = "jp")]
        public TeamJoinPolicy JoinPolicy { get; set; } = TeamJoinPolicy.Open;

        [JsonProperty(PropertyName = "me")]
        public int MinElo { get; set; } = RoatanRankTierHelper.MinElo;

        [JsonProperty(PropertyName = "ar")]
        public TeamActivityRequirement ActivityRequirement { get; set; } = TeamActivityRequirement.Any;

        [JsonProperty(PropertyName = "mr")]
        public bool MeetsRequirements { get; set; } = true;

        [JsonProperty(PropertyName = "dr", NullValueHandling = NullValueHandling.Ignore)]
        public string DisqualifyReason { get; set; }
    }

    public class TeamMemberDto
    {
        [JsonProperty(PropertyName = "i")]
        public long UserId { get; set; }

        [JsonProperty(PropertyName = "u")]
        public string Username { get; set; }

        [JsonProperty(PropertyName = "ab", NullValueHandling = NullValueHandling.Ignore)]
        public string AvatarBase64 { get; set; }

        [JsonProperty(PropertyName = "au", NullValueHandling = NullValueHandling.Ignore)]
        public string AvatarUrl { get; set; }

        [JsonProperty(PropertyName = "e")]
        public int Elo { get; set; } = RoatanRankTierHelper.DefaultElo;

        [JsonProperty(PropertyName = "rt")]
        public RoatanRankTier RankTier { get; set; } = RoatanRankTier.Sailor;

        [JsonProperty(PropertyName = "r")]
        public TeamRole Role { get; set; } = TeamRole.Teammate;

        [JsonProperty(PropertyName = "on")]
        public bool IsOnline { get; set; }

        [JsonProperty(PropertyName = "j")]
        public DateTime JoinedAtUtc { get; set; }
    }

    public class TeamJoinRequestDto
    {
        [JsonProperty(PropertyName = "i")]
        public long RequestId { get; set; }

        [JsonProperty(PropertyName = "ti")]
        public long TeamId { get; set; }

        [JsonProperty(PropertyName = "tn", NullValueHandling = NullValueHandling.Ignore)]
        public string TeamName { get; set; }

        [JsonProperty(PropertyName = "ui")]
        public long UserId { get; set; }

        [JsonProperty(PropertyName = "u")]
        public string Username { get; set; }

        [JsonProperty(PropertyName = "ab", NullValueHandling = NullValueHandling.Ignore)]
        public string AvatarBase64 { get; set; }

        [JsonProperty(PropertyName = "e")]
        public int Elo { get; set; } = RoatanRankTierHelper.DefaultElo;

        [JsonProperty(PropertyName = "rt")]
        public RoatanRankTier RankTier { get; set; } = RoatanRankTier.Sailor;

        [JsonProperty(PropertyName = "c")]
        public DateTime CreatedAtUtc { get; set; }
    }

    public class TeamInviteDto
    {
        [JsonProperty(PropertyName = "i")]
        public long InviteId { get; set; }

        [JsonProperty(PropertyName = "ti")]
        public long TeamId { get; set; }

        [JsonProperty(PropertyName = "tn")]
        public string TeamName { get; set; }

        [JsonProperty(PropertyName = "tt")]
        public string TeamTag { get; set; }

        [JsonProperty(PropertyName = "te")]
        public int TeamElo { get; set; } = RoatanRankTierHelper.DefaultElo;

        [JsonProperty(PropertyName = "rt")]
        public RoatanRankTier TeamRankTier { get; set; } = RoatanRankTier.Sailor;

        [JsonProperty(PropertyName = "ui")]
        public long InvitedUserId { get; set; }

        [JsonProperty(PropertyName = "uu", NullValueHandling = NullValueHandling.Ignore)]
        public string InvitedUsername { get; set; }

        [JsonProperty(PropertyName = "bi")]
        public long InvitedByUserId { get; set; }

        [JsonProperty(PropertyName = "bu")]
        public string InvitedByUsername { get; set; }

        [JsonProperty(PropertyName = "c")]
        public DateTime CreatedAtUtc { get; set; }
    }

    public class MyTeamDetailsDto
    {
        [JsonProperty(PropertyName = "t", NullValueHandling = NullValueHandling.Ignore)]
        public TeamInfoDto TeamInfo { get; set; }

        [JsonProperty(PropertyName = "mr", NullValueHandling = NullValueHandling.Ignore)]
        public TeamRole? MyRole { get; set; }

        [JsonProperty(PropertyName = "m")]
        public List<TeamMemberDto> Members { get; set; } = new List<TeamMemberDto>();

        [JsonProperty(PropertyName = "pr")]
        public List<TeamJoinRequestDto> PendingRequests { get; set; } = new List<TeamJoinRequestDto>();

        [JsonProperty(PropertyName = "pi")]
        public List<TeamInviteDto> PendingInvites { get; set; } = new List<TeamInviteDto>();

        [JsonProperty(PropertyName = "msg", NullValueHandling = NullValueHandling.Ignore)]
        public string StatusMessage { get; set; }
    }

    public class TeamsListDto
    {
        [JsonProperty(PropertyName = "t")]
        public List<TeamInfoDto> Teams { get; set; } = new List<TeamInfoDto>();

        [JsonProperty(PropertyName = "pi")]
        public List<TeamInviteDto> PendingInvites { get; set; } = new List<TeamInviteDto>();
    }

    public class GetTeamsRequestDto
    {
        [JsonProperty(PropertyName = "q", NullValueHandling = NullValueHandling.Ignore)]
        public string SearchQuery { get; set; }
    }

    public class CreateTeamDto
    {
        [JsonProperty(PropertyName = "n")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "t")]
        public string Tag { get; set; }

        [JsonProperty(PropertyName = "jp")]
        public TeamJoinPolicy JoinPolicy { get; set; } = TeamJoinPolicy.Open;

        [JsonProperty(PropertyName = "me")]
        public int MinElo { get; set; } = RoatanRankTierHelper.MinElo;

        [JsonProperty(PropertyName = "ar")]
        public TeamActivityRequirement ActivityRequirement { get; set; } = TeamActivityRequirement.Any;
    }

    public class UpdateTeamSettingsDto
    {
        [JsonProperty(PropertyName = "n")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "t")]
        public string Tag { get; set; }

        [JsonProperty(PropertyName = "jp")]
        public TeamJoinPolicy JoinPolicy { get; set; } = TeamJoinPolicy.Open;

        [JsonProperty(PropertyName = "me")]
        public int MinElo { get; set; } = RoatanRankTierHelper.MinElo;

        [JsonProperty(PropertyName = "ar")]
        public TeamActivityRequirement ActivityRequirement { get; set; } = TeamActivityRequirement.Any;
    }

    public class JoinOrRequestTeamDto
    {
        [JsonProperty(PropertyName = "ti")]
        public long TeamId { get; set; }
    }

    public class RespondTeamJoinRequestDto
    {
        [JsonProperty(PropertyName = "ri")]
        public long RequestId { get; set; }

        [JsonProperty(PropertyName = "a")]
        public bool Approve { get; set; }
    }

    public class InviteToTeamDto
    {
        [JsonProperty(PropertyName = "ui")]
        public long TargetUserId { get; set; }

        [JsonProperty(PropertyName = "u", NullValueHandling = NullValueHandling.Ignore)]
        public string TargetUsername { get; set; }
    }

    public class RespondTeamInviteDto
    {
        [JsonProperty(PropertyName = "ii")]
        public long InviteId { get; set; }

        [JsonProperty(PropertyName = "a")]
        public bool Accept { get; set; }
    }

    public class PromoteOrDemoteMemberDto
    {
        [JsonProperty(PropertyName = "ui")]
        public long TargetUserId { get; set; }

        [JsonProperty(PropertyName = "r")]
        public TeamRole NewRole { get; set; }
    }

    public class KickTeamMemberDto
    {
        [JsonProperty(PropertyName = "ui")]
        public long TargetUserId { get; set; }
    }
}
