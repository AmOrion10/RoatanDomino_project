using System;

using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Friends
{
    public class FriendDto
    {
        [JsonProperty(PropertyName = "l")]
        public string Login { get; set; }

        [JsonProperty(PropertyName = "s")]
        public DateTime FriendSince { get; set; }
    }
}
