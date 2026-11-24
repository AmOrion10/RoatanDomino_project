using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby
{
    public class LobbySeatDto
    {
        [JsonProperty(PropertyName = "si")]
        public int SeatIndex { get; set; }

        [JsonProperty(PropertyName = "o")]
        public bool IsOccupied { get; set; }

        [JsonProperty(PropertyName = "b")]
        public bool IsBot { get; set; }

        [JsonProperty(PropertyName = "bd", NullValueHandling = NullValueHandling.Ignore)]
        public string BotDifficulty { get; set; }

        [JsonProperty(PropertyName = "p", NullValueHandling = NullValueHandling.Ignore)]
        public PlayerDto Player { get; set; }

        [JsonProperty(PropertyName = "r")]
        public bool IsReady { get; set; }
    }
}
