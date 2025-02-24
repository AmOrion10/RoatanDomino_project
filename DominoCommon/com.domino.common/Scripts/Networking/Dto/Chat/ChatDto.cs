using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Chat
{
    public class ChatDto
    {
        [JsonProperty(PropertyName = "i")]
        public int Id { get; set; }

        [JsonProperty(PropertyName = "n")]
        public string Name { get; set; }
    }
}
