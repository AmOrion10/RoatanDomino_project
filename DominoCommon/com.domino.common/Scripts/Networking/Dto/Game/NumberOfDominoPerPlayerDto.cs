using System.Collections.Generic;

using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Game
{
    public class NumberOfDominoPerPlayerDto
    {
        [JsonProperty(PropertyName = "n")]
        public Dictionary<long, int> NumberOfDomino { get; set; } = new Dictionary<long, int>();
    }
}
