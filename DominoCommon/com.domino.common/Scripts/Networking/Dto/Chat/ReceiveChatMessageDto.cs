using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Chat
{
    public class ReceiveChatMessageDto
    {
        [JsonProperty(PropertyName = "i")]
        public int ChatId { get; set; }

        [JsonProperty(PropertyName = "s")]
        public string Sender { get; set; }

        [JsonProperty(PropertyName = "m")]
        public string Message { get; set; }
    }
}