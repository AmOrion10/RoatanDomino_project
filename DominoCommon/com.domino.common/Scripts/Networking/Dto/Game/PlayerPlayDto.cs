using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Game
{
    public class PlayerPlayDto
    {
        [JsonProperty(PropertyName = "a")]
        public PlayActionType PlayActionType { get; set; }

        [JsonProperty(PropertyName = "i")]
        public long IdUserPlayed { get; set; }

        [JsonProperty(PropertyName = "m")]
        public MoveDto Moves { get; set; }
    }
}
