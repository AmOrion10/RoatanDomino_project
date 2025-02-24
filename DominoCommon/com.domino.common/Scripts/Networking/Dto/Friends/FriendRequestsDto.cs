using System.Collections.Generic;

using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Friends
{
    public class FriendRequestsDto
    {
        [JsonProperty(PropertyName = "r")]
        public List<FriendRequestDto> FriendRequests = new List<FriendRequestDto>();
    }
}
