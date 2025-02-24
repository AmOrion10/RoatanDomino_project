using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.User
{
    public class PasswordChangeDto
    {
        [JsonProperty(PropertyName = "a")]
        public string ActualPassword { get; set; }

        [JsonProperty(PropertyName = "n")]
        public string NewPassword { get; set; }
    }
}
