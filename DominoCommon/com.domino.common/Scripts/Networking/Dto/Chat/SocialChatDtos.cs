using System;
using System.Collections.Generic;

using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Chat
{
    public enum SocialChatChannelType
    {
        Team = 0,
        Direct = 1
    }

    public class ChatMessageDto
    {
        [JsonProperty(PropertyName = "id")]
        public long MessageId { get; set; }

        [JsonProperty(PropertyName = "ct")]
        public SocialChatChannelType ChannelType { get; set; } = SocialChatChannelType.Team;

        [JsonProperty(PropertyName = "ti", NullValueHandling = NullValueHandling.Ignore)]
        public long? TeamId { get; set; }

        [JsonProperty(PropertyName = "su")]
        public long SenderUserId { get; set; }

        [JsonProperty(PropertyName = "sn", NullValueHandling = NullValueHandling.Ignore)]
        public string SenderUsername { get; set; }

        [JsonProperty(PropertyName = "ru", NullValueHandling = NullValueHandling.Ignore)]
        public long? RecipientUserId { get; set; }

        [JsonProperty(PropertyName = "rn", NullValueHandling = NullValueHandling.Ignore)]
        public string RecipientUsername { get; set; }

        [JsonProperty(PropertyName = "b")]
        public string Body { get; set; }

        [JsonProperty(PropertyName = "st")]
        public DateTime SentUtc { get; set; }
    }

    public class GetChatHistoryDto
    {
        [JsonProperty(PropertyName = "ct")]
        public SocialChatChannelType ChannelType { get; set; } = SocialChatChannelType.Team;

        [JsonProperty(PropertyName = "ti", NullValueHandling = NullValueHandling.Ignore)]
        public long? TeamId { get; set; }

        [JsonProperty(PropertyName = "ou", NullValueHandling = NullValueHandling.Ignore)]
        public long? OtherUserId { get; set; }

        [JsonProperty(PropertyName = "l")]
        public int Limit { get; set; } = 50;
    }

    public class ChatHistoryDto
    {
        [JsonProperty(PropertyName = "ct")]
        public SocialChatChannelType ChannelType { get; set; } = SocialChatChannelType.Team;

        [JsonProperty(PropertyName = "ti", NullValueHandling = NullValueHandling.Ignore)]
        public long? TeamId { get; set; }

        [JsonProperty(PropertyName = "ou", NullValueHandling = NullValueHandling.Ignore)]
        public long? OtherUserId { get; set; }

        [JsonProperty(PropertyName = "ms")]
        public List<ChatMessageDto> Messages { get; set; } = new List<ChatMessageDto>();
    }
}
