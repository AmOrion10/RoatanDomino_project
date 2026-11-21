using DominoCommon.com.domino.common.Scripts.Enums;

using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Game
{
    public class ActiveMatchCheckResponseDto
    {
        [JsonProperty(PropertyName = "act")]
        public bool HasActiveMatch { get; set; }

        [JsonProperty(PropertyName = "mid")]
        public int MatchId { get; set; }

        [JsonProperty(PropertyName = "lid")]
        public int LobbyId { get; set; }

        [JsonProperty(PropertyName = "pid")]
        public long PlayerId { get; set; }

        [JsonProperty(PropertyName = "rem")]
        public int RemainingGraceSeconds { get; set; }

        [JsonProperty(PropertyName = "mf")]
        public MatchFormat MatchFormat { get; set; }

        [JsonProperty(PropertyName = "ts")]
        public TableStyle TableStyle { get; set; }

        [JsonProperty(PropertyName = "ttl")]
        public string MatchTitle { get; set; }
    }
}
