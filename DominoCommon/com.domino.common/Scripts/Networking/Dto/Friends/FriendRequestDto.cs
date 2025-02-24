using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Friends
{
    public class FriendRequestDto
    {
        [JsonProperty(PropertyName = "l")]
        public string Login { get; set; }
    }
}
