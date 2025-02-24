using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby
{
    public class LobbyCodeDto
    {
        [JsonProperty(PropertyName = "c")]
        public string Code { get; set; }
    }
}