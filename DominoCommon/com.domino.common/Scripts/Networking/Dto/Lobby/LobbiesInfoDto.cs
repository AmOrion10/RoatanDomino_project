using System.Collections.Generic;

using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby
{
    public class LobbiesInfoDto
    {
        [JsonProperty(PropertyName = "l")]
        public List<LobbyInfoDto> Lobbies { get; set; } = new List<LobbyInfoDto>();
    }
}
