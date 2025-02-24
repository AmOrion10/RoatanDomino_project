using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby
{
    public class LobbyNameDto
    {
        [JsonProperty(PropertyName = "n")]
        public string Name { get; set; }
    }
}