using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby
{
    public class SwitchTableSeatDto
    {
        [JsonProperty(PropertyName = "si")]
        public int TargetSeatIndex { get; set; }
    }
}
