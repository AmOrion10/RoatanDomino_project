using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto
{
    public class TimerDto
    {
        [JsonProperty(PropertyName = "d")]
        public int Duration { get; set; }

        [JsonProperty(PropertyName = "s")]
        public bool HasStarted { get; set; }
    }
}
