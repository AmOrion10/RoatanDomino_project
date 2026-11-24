using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Game
{
    public class RejoinSessionRequestDto
    {
        [JsonProperty(PropertyName = "mid")]
        public int MatchId { get; set; }

        [JsonProperty(PropertyName = "lid")]
        public int LobbyId { get; set; }

        [JsonProperty(PropertyName = "pid")]
        public long PlayerId { get; set; }

        [JsonProperty(PropertyName = "tok")]
        public string ReconnectToken { get; set; }
    }
}
