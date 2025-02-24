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
    }
}
