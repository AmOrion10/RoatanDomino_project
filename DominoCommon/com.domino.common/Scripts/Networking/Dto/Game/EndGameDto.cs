using System.Collections.Generic;

using DominoCommon.com.domino.common.Scripts.Enums;

using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Game
{
    public class EndGameDto
    {
        [JsonProperty(PropertyName = "n")]
        public bool NormalFinish { get; set; }

        [JsonProperty(PropertyName = "r")]
        public List<PlayerRankingsDto> Rankings { get; set; }

        [JsonProperty(PropertyName = "brk", NullValueHandling = NullValueHandling.Ignore)]
        public bool? BreakTriggered { get; set; }

        [JsonProperty(PropertyName = "mo", NullValueHandling = NullValueHandling.Ignore)]
        public bool? MatchOver { get; set; }

        [JsonProperty(PropertyName = "act", NullValueHandling = NullValueHandling.Ignore)]
        public List<PostHandAction> AvailableActions { get; set; }

        [JsonProperty(PropertyName = "key", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsKeyWin { get; set; }

        [JsonProperty(PropertyName = "pts", NullValueHandling = NullValueHandling.Ignore)]
        public int? PointsAwarded { get; set; }

        [JsonProperty(PropertyName = "elo", NullValueHandling = NullValueHandling.Ignore)]
        public MatchEloBreakdownDto EloBreakdown { get; set; }
    }
}
