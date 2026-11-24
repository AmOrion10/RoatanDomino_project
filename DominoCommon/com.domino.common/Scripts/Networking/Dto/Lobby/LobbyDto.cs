using System.Collections.Generic;

using DominoCommon.com.domino.common.Scripts.Enums;

using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby
{
    public class LobbyDto
    {
        [JsonProperty(PropertyName = "i")]
        public int Id { get; set; }

        [JsonProperty(PropertyName = "c")]
        public PlayerDto Creator { get; set; }

        [JsonProperty(PropertyName = "p")]
        public List<PlayerDto> Players { get; set; } = new List<PlayerDto>();

        [JsonProperty(PropertyName = "n")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "m")]
        public int MaxNumberOfPlayer { get; set; }

        [JsonProperty(PropertyName = "ip")]
        public bool IsPublic { get; set; }

        [JsonProperty(PropertyName = "r")]
        public bool Ranked { get; set; }

        [JsonProperty(PropertyName = "co")]
        public string Code { get; set; } // Can be null

        [JsonProperty(PropertyName = "ts", NullValueHandling = NullValueHandling.Ignore)]
        public TableStyle? TableStyle { get; set; }

        [JsonProperty(PropertyName = "mf", NullValueHandling = NullValueHandling.Ignore)]
        public MatchFormat? MatchFormat { get; set; }

        [JsonProperty(PropertyName = "oop", NullValueHandling = NullValueHandling.Ignore)]
        public bool? OneOnePlayoff { get; set; }
    }
}
