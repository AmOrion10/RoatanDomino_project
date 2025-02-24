using System.Collections.Generic;

using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Game
{
    public class PossibleMovesDto
    {
        [JsonProperty(PropertyName = "m")]
        public List<MoveDto> Moves { get; set; } = new List<MoveDto>();
    }
}
