using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.User
{
    public class StatisticDto
    {
        [JsonProperty(PropertyName = "p")]
        public int NumberOfGamePlayed { get; set; }

        [JsonProperty(PropertyName = "w")]
        public int NumberOfGameWon { get; set; }

        [JsonProperty(PropertyName = "l")]
        public int NumberOfGameLost { get; set; }
    }
}
