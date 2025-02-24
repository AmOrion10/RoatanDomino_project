using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.User
{
    public class GetHistoryGameDto
    {
        [JsonProperty(PropertyName = "i")]
        public long Id { get; set; }
    }
}
