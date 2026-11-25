using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.User
{
    public class PlayerPresenceDto
    {
        [JsonProperty(PropertyName = "i")]
        public long UserId { get; set; }

        [JsonProperty(PropertyName = "u")]
        public string Username { get; set; }

        [JsonProperty(PropertyName = "e")]
        public int Elo { get; set; }

        [JsonProperty(PropertyName = "on")]
        public bool IsOnline { get; set; }

        [JsonProperty(PropertyName = "st")]
        public string Status { get; set; } // "Available", "At Table", "In Match", "Offline"

        [JsonProperty(PropertyName = "ti", NullValueHandling = NullValueHandling.Ignore)]
        public int? TableId { get; set; }

        [JsonProperty(PropertyName = "tn", NullValueHandling = NullValueHandling.Ignore)]
        public string TableName { get; set; }

        [JsonProperty(PropertyName = "au", NullValueHandling = NullValueHandling.Ignore)]
        public string AvatarUrl { get; set; }

        [JsonProperty(PropertyName = "ab", NullValueHandling = NullValueHandling.Ignore)]
        public string AvatarBase64 { get; set; }
    }
}
