using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby
{
    public class PlayerReadyDto
    {
        [JsonProperty(PropertyName = "i")]
        public long PlayerId { get; set; }

        [JsonProperty(PropertyName = "r")]
        public bool IsReady { get; set; }
    }
}
