using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby
{
    public class RemoveTableBotDto
    {
        [JsonProperty(PropertyName = "si")]
        public int SeatIndex { get; set; }
    }
}
