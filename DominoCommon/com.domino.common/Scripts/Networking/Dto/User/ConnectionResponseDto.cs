using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.User
{
    public class ConnectionResponseDto
    {
        [JsonProperty(PropertyName = "u")]
        public UserDto User { get; set; }

        [JsonProperty(PropertyName = "t")]
        public string Token { get; set; }
    }
}
