using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto
{
    public class PlayerDto
    {
        [JsonProperty(PropertyName = "i")]
        public long Id { get; set; }

        [JsonProperty(PropertyName = "n")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "e")]
        public int Elo { get; set; }

        [JsonProperty(PropertyName = "b", NullValueHandling = NullValueHandling.Ignore)]
        public bool IsBot { get; set; }

        [JsonProperty(PropertyName = "bd", NullValueHandling = NullValueHandling.Ignore)]
        public string BotDifficulty { get; set; }

        [JsonProperty(PropertyName = "si", NullValueHandling = NullValueHandling.Ignore)]
        public int SeatIndex { get; set; }

        [JsonProperty(PropertyName = "au", NullValueHandling = NullValueHandling.Ignore)]
        public string AvatarUrl { get; set; }

        [JsonProperty(PropertyName = "ab", NullValueHandling = NullValueHandling.Ignore)]
        public string AvatarBase64 { get; set; }
    }
}
