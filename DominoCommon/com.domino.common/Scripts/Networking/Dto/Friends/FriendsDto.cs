using System.Collections.Generic;

using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Friends
{
    public class FriendsDto
    {
        [JsonProperty(PropertyName = "f")]
        public List<FriendDto> Friends = new List<FriendDto>();
    }
}
