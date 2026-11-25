using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.User
{
    public class SocialAuthDto
    {
        [JsonProperty(PropertyName = "pr")]
        public string Provider { get; set; } // "Google" or "Facebook"

        [JsonProperty(PropertyName = "tk", NullValueHandling = NullValueHandling.Ignore)]
        public string IdToken { get; set; }

        [JsonProperty(PropertyName = "em", NullValueHandling = NullValueHandling.Ignore)]
        public string Email { get; set; }

        [JsonProperty(PropertyName = "dn", NullValueHandling = NullValueHandling.Ignore)]
        public string DisplayName { get; set; }

        [JsonProperty(PropertyName = "pid", NullValueHandling = NullValueHandling.Ignore)]
        public string ProviderUserId { get; set; }

        [JsonProperty(PropertyName = "oc", NullValueHandling = NullValueHandling.Ignore)]
        public string OneTimeCode { get; set; }

        [JsonProperty(PropertyName = "au", NullValueHandling = NullValueHandling.Ignore)]
        public string AvatarUrl { get; set; }

        [JsonProperty(PropertyName = "ab", NullValueHandling = NullValueHandling.Ignore)]
        public string AvatarBase64 { get; set; }
    }
}
