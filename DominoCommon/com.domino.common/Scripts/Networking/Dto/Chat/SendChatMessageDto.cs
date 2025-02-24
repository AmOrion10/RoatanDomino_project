using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Chat
{
    public class SendChatMessageDto
    {
        [JsonProperty(PropertyName = "i")]
        public int ChatId { get; set; }

        [JsonProperty(PropertyName = "m")]
        public string Message { get; set; }
    }
}