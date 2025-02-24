using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Database.Entities;

public class ChatEntity
{
    public long Id { get; set; }

    public long SenderUserId { get; set; }

    public long ReceiverUserId { get; set; }

    public string Content { get; set; }

    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime MessageTime { get; set; }

    public string Channel { get; set; }

    public virtual UserEntity SenderUser { get; set; } = null!;

    public virtual UserEntity ReceiverUser { get; set; } = null!;
}
