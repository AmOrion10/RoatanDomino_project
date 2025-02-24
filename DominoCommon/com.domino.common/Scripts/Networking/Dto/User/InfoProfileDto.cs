using Newtonsoft.Json;
using System;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.User
{
    public class InfoProfileDto
    {
        [JsonProperty(PropertyName = "u")]
        public UserDto User { get; set; }

        [JsonProperty(PropertyName = "s")]
        public StatisticDto Statistic { get; set; }
    }
}
