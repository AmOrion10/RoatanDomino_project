using System.Collections.Generic;

using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Game
{
    public class EndGameDto
    {
        [JsonProperty(PropertyName = "n")]
        public bool NormalFinish { get; set; }

        [JsonProperty(PropertyName = "r")]
        public List<PlayerRankingsDto> Rankings { get; set; }
    }
}
