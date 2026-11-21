using System.Collections.Generic;

using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Game
{
    public class HandDto
    {
        [JsonProperty(PropertyName = "d")]
        public List<DominoDto> Dominoes { get; set; } = new List<DominoDto>();

        [JsonProperty(PropertyName = "mid", NullValueHandling = NullValueHandling.Ignore)]
        public int? MatchId { get; set; }

        [JsonProperty(PropertyName = "lid", NullValueHandling = NullValueHandling.Ignore)]
        public int? LobbyId { get; set; }

        [JsonProperty(PropertyName = "rtok", NullValueHandling = NullValueHandling.Ignore)]
        public string ReconnectToken { get; set; }
    }
}
