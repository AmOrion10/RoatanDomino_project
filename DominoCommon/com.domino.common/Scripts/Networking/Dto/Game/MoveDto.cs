using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Game
{
    public class MoveDto
    {
        [JsonProperty(PropertyName = "d")]
        public DominoDto Domino { get; set; }

        [JsonProperty(PropertyName = "r")]
        public int Row { get; set; }

        [JsonProperty(PropertyName = "c")]
        public int Col { get; set; }

        [JsonProperty(PropertyName = "o")]
        public Orientation Orientation { get; set; }

        [JsonProperty(PropertyName = "p")]
        public long PlayerId { get; set; }
    }
}
