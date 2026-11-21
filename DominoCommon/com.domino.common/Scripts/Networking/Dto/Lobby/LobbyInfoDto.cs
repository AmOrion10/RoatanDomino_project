using DominoCommon.com.domino.common.Scripts.Enums;

using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby
{
    public class LobbyInfoDto
    {
        [JsonProperty(PropertyName = "i")]
        public int Id { get; set; }

        [JsonProperty(PropertyName = "c")]
        public string Creator { get; set; } // Can be null

        [JsonProperty(PropertyName = "m")]
        public int MaxNumberOfPlayer { get; set; }

        [JsonProperty(PropertyName = "np")]
        public int NumberOfPlayer { get; set; }

        [JsonProperty(PropertyName = "n")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "p")]
        public bool IsPublic { get; set; }

        [JsonProperty(PropertyName = "ts", NullValueHandling = NullValueHandling.Ignore)]
        public TableStyle? TableStyle { get; set; }

        [JsonProperty(PropertyName = "mf", NullValueHandling = NullValueHandling.Ignore)]
        public MatchFormat? MatchFormat { get; set; }
    }
}
