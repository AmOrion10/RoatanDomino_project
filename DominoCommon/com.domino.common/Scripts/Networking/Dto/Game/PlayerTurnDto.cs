using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Game
{
    public class PlayerTurnDto
    {
        [JsonProperty(PropertyName = "i")]
        public long IdUserPlaying { get; set; }

        [JsonProperty(PropertyName = "t")]
        public TimerDto Timer { get; set; }
    }
}
