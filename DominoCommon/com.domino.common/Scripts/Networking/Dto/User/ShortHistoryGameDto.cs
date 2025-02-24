using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.User
{
    public class ShortHistoryGameDto
    {
        [JsonProperty(PropertyName = "i")]
        public long Id { get; set; }

        [JsonProperty(PropertyName = "e")]
        public string EndTime { get; set; }

        [JsonProperty(PropertyName = "r")]
        public bool Ranked { get; set; }
    }
}
