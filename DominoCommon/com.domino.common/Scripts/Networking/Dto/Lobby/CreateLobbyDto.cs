using DominoCommon.com.domino.common.Scripts.Enums;

using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby
{
    public class CreateLobbyDto
    {
        [JsonProperty(PropertyName = "n")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "m")]
        public int MaxNumberOfPlayer { get; set; }

        [JsonProperty(PropertyName = "p")]
        public bool Public { get; set; }

        [JsonProperty(PropertyName = "ts", NullValueHandling = NullValueHandling.Ignore)]
        public TableStyle? TableStyle { get; set; }

        [JsonProperty(PropertyName = "mf", NullValueHandling = NullValueHandling.Ignore)]
        public MatchFormat? MatchFormat { get; set; }

        [JsonProperty(PropertyName = "oop", NullValueHandling = NullValueHandling.Ignore)]
        public bool? OneOnePlayoff { get; set; }
    }
}
