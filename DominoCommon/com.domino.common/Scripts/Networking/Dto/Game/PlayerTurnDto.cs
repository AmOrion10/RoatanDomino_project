using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Game
{
    public class PlayerTurnDto
    {
        [JsonProperty(PropertyName = "i")]
        public long IdUserPlaying { get; set; }

        [JsonProperty(PropertyName = "t")]
        public TimerDto Timer { get; set; }

        [JsonProperty(PropertyName = "ap", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsAutoPassTurn { get; set; }

        [JsonProperty(PropertyName = "bc", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsBotControlled { get; set; }

        [JsonProperty(PropertyName = "cap", NullValueHandling = NullValueHandling.Ignore)]
        public int? ConsecutiveAutoPlays { get; set; }
    }
}
