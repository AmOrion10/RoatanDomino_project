using System.Collections.Generic;

using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Game
{
    public class HandDto
    {
        [JsonProperty(PropertyName = "d")]
        public List<DominoDto> Dominoes { get; set; } = new List<DominoDto>();
    }
}
