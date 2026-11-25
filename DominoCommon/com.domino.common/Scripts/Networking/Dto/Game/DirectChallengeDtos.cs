using System;

using DominoCommon.com.domino.common.Scripts.Enums;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby;

using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Game
{
    public enum DirectChallengeStatus
    {
        Pending = 0,
        Accepted = 1,
        Ignored = 2
    }

    public class SendDirectChallengeDto
    {
        [JsonProperty(PropertyName = "tu")]
        public long TargetUserId { get; set; }

        [JsonProperty(PropertyName = "gm")]
        public MatchFormat GameMode { get; set; } = MatchFormat.TWO_BEST;

        [JsonProperty(PropertyName = "v")]
        public TableStyle Variant { get; set; } = TableStyle.DRAW;
    }

    public class RespondDirectChallengeDto
    {
        [JsonProperty(PropertyName = "ci")]
        public long ChallengeId { get; set; }

        [JsonProperty(PropertyName = "a")]
        public bool Accept { get; set; }
    }

    public class DirectChallengeDto
    {
        [JsonProperty(PropertyName = "ci")]
        public long ChallengeId { get; set; }

        [JsonProperty(PropertyName = "cu")]
        public long ChallengerUserId { get; set; }

        [JsonProperty(PropertyName = "cn")]
        public string ChallengerUsername { get; set; }

        [JsonProperty(PropertyName = "ce")]
        public int ChallengerElo { get; set; } = RoatanRankTierHelper.DefaultElo;

        [JsonProperty(PropertyName = "tu")]
        public long TargetUserId { get; set; }

        [JsonProperty(PropertyName = "tn")]
        public string TargetUsername { get; set; }

        [JsonProperty(PropertyName = "gm")]
        public MatchFormat GameMode { get; set; } = MatchFormat.TWO_BEST;

        [JsonProperty(PropertyName = "v")]
        public TableStyle Variant { get; set; } = TableStyle.DRAW;

        [JsonProperty(PropertyName = "tb", NullValueHandling = NullValueHandling.Ignore)]
        public int? TableId { get; set; }

        [JsonProperty(PropertyName = "tc", NullValueHandling = NullValueHandling.Ignore)]
        public string TableCode { get; set; }

        [JsonProperty(PropertyName = "s")]
        public DirectChallengeStatus Status { get; set; } = DirectChallengeStatus.Pending;

        [JsonProperty(PropertyName = "c")]
        public DateTime CreatedUtc { get; set; }

        [JsonProperty(PropertyName = "m", NullValueHandling = NullValueHandling.Ignore)]
        public string Message { get; set; }
    }

    public class DirectChallengeActionResponseDto
    {
        [JsonProperty(PropertyName = "ch")]
        public DirectChallengeDto Challenge { get; set; }

        [JsonProperty(PropertyName = "tb", NullValueHandling = NullValueHandling.Ignore)]
        public LobbyDto Table { get; set; }
    }
}
