using System.Collections.Generic;

using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.User
{
    public class PlayerRosterDto
    {
        [JsonProperty(PropertyName = "p")]
        public List<PlayerPresenceDto> Players { get; set; } = new List<PlayerPresenceDto>();
    }
}
