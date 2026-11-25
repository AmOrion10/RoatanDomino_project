using DominoCommon.com.domino.common.Scripts.Networking.Dto.Chat;

namespace Infrastructure.Database.Entities;

public class SocialChatMessageEntity
{
    public long Id { get; set; }

    public SocialChatChannelType ChannelType { get; set; } = SocialChatChannelType.Team;

    public long? TeamId { get; set; }

    public long SenderUserId { get; set; }

    public long? RecipientUserId { get; set; }

    public string Body { get; set; } = string.Empty;

    public DateTime SentUtc { get; set; } = DateTime.UtcNow;

    public virtual UserEntity SenderUser { get; set; } = null!;

    public virtual UserEntity? RecipientUser { get; set; }
}
