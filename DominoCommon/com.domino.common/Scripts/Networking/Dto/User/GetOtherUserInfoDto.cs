using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.User
{
    public class GetOtherUserInfoDto
    {
        [JsonProperty(PropertyName = "l")]
        public string Login { get; set; }
    }
}
