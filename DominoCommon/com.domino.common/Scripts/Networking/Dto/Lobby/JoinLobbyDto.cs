using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby
{
    public class JoinLobbyDto
    {
        [JsonProperty(PropertyName = "i")]
        public int LobbyIdToJoin { get; set; }
    }
}
