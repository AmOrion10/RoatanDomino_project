using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Game
{
    public class PlayerRankingsDto
    {
        [JsonProperty(PropertyName = "i")]
        public long Id { get; set; }

        [JsonProperty(PropertyName = "r")]
        public int Rank { get; set; }

        [JsonProperty(PropertyName = "g")]
        public int Gain { get; set; }
    }
}
