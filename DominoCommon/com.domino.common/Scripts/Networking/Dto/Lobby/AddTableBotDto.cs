using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby
{
    public class AddTableBotDto
    {
        [JsonProperty(PropertyName = "si")]
        public int SeatIndex { get; set; }

        [JsonProperty(PropertyName = "bd")]
        public string Difficulty { get; set; } = "Pro";
    }
}
