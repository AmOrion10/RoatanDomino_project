using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.User
{
    public class UpdateProfileAvatarDto
    {
        [JsonProperty(PropertyName = "ab", NullValueHandling = NullValueHandling.Ignore)]
        public string AvatarBase64 { get; set; }

        [JsonProperty(PropertyName = "au", NullValueHandling = NullValueHandling.Ignore)]
        public string AvatarUrl { get; set; }
    }
}
