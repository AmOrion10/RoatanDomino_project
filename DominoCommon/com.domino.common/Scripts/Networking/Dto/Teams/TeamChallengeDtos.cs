using System;
using System.Collections.Generic;

using DominoCommon.com.domino.common.Scripts.Enums;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby;

using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Teams
{
    public enum TeamChallengeStatus
    {
        Pending = 0,
        Accepted = 1,
        Declined = 2,
        Completed = 3
    }

    public class TeamChallengeDto
    {
        [JsonProperty(PropertyName = "i")]
        public long ChallengeId { get; set; }

        [JsonProperty(PropertyName = "ci")]
        public long ChallengerTeamId { get; set; }

        [JsonProperty(PropertyName = "cn")]
        public string ChallengerTeamName { get; set; }

        [JsonProperty(PropertyName = "ct")]
        public string ChallengerTeamTag { get; set; }

        [JsonProperty(PropertyName = "ce")]
        public int ChallengerTeamElo { get; set; } = RoatanRankTierHelper.DefaultElo;

        [JsonProperty(PropertyName = "ti")]
        public long TargetTeamId { get; set; }

        [JsonProperty(PropertyName = "tn")]
        public string TargetTeamName { get; set; }

        [JsonProperty(PropertyName = "tt")]
        public string TargetTeamTag { get; set; }

        [JsonProperty(PropertyName = "te")]
        public int TargetTeamElo { get; set; } = RoatanRankTierHelper.DefaultElo;

        [JsonProperty(PropertyName = "gm")]
        public MatchFormat GameMode { get; set; } = MatchFormat.SIX_LOV;

        [JsonProperty(PropertyName = "v")]
        public TableStyle Variant { get; set; } = TableStyle.DRAW;

        [JsonProperty(PropertyName = "tb")]
        public int TableId { get; set; }

        [JsonProperty(PropertyName = "tc", NullValueHandling = NullValueHandling.Ignore)]
        public string TableCode { get; set; }

        [JsonProperty(PropertyName = "s")]
        public TeamChallengeStatus Status { get; set; } = TeamChallengeStatus.Pending;

        [JsonProperty(PropertyName = "c")]
        public DateTime CreatedUtc { get; set; }

        [JsonProperty(PropertyName = "m", NullValueHandling = NullValueHandling.Ignore)]
        public string Message { get; set; }
    }

    public class SendTeamChallengeDto
    {
        [JsonProperty(PropertyName = "ti")]
        public long TargetTeamId { get; set; }

        [JsonProperty(PropertyName = "gm")]
        public MatchFormat GameMode { get; set; } = MatchFormat.SIX_LOV;

        [JsonProperty(PropertyName = "v")]
        public TableStyle Variant { get; set; } = TableStyle.DRAW;
    }

    public class RespondTeamChallengeDto
    {
        [JsonProperty(PropertyName = "ci")]
        public long ChallengeId { get; set; }

        [JsonProperty(PropertyName = "a")]
        public bool Accept { get; set; }
    }

    public class TeamChallengeActionResponseDto
    {
        [JsonProperty(PropertyName = "ch")]
        public TeamChallengeDto Challenge { get; set; }

        [JsonProperty(PropertyName = "tb", NullValueHandling = NullValueHandling.Ignore)]
        public LobbyDto Table { get; set; }
    }

    public class TeamLeaderboardEntryDto
    {
        [JsonProperty(PropertyName = "r")]
        public int Rank { get; set; }

        [JsonProperty(PropertyName = "i")]
        public long TeamId { get; set; }

        [JsonProperty(PropertyName = "n")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "t")]
        public string Tag { get; set; }

        [JsonProperty(PropertyName = "e")]
        public int TeamElo { get; set; } = RoatanRankTierHelper.DefaultElo;

        [JsonProperty(PropertyName = "rt")]
        public RoatanRankTier TeamRankTier { get; set; } = RoatanRankTier.Sailor;

        [JsonProperty(PropertyName = "tn")]
        public string TierName { get; set; }

        [JsonProperty(PropertyName = "tc")]
        public string TierColorHex { get; set; }

        [JsonProperty(PropertyName = "w")]
        public int TeamWins { get; set; }

        [JsonProperty(PropertyName = "lo")]
        public int TeamLosses { get; set; }

        [JsonProperty(PropertyName = "cu")]
        public string CaptainUsername { get; set; }

        [JsonProperty(PropertyName = "mc")]
        public int MemberCount { get; set; }

        [JsonProperty(PropertyName = "mm")]
        public int MaxMembers { get; set; } = TeamInfoDto.DefaultMaxMembers;
    }

    public class TeamLeaderboardDto
    {
        [JsonProperty(PropertyName = "e")]
        public List<TeamLeaderboardEntryDto> Entries { get; set; } = new List<TeamLeaderboardEntryDto>();

        [JsonProperty(PropertyName = "me", NullValueHandling = NullValueHandling.Ignore)]
        public TeamLeaderboardEntryDto MyTeamEntry { get; set; }

        [JsonProperty(PropertyName = "tot")]
        public int TotalTeams { get; set; }
    }
}
