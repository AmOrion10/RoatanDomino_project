using System.ComponentModel.DataAnnotations.Schema;

using Domain;

namespace Infrastructure.Database.Entities;

public class FriendshipEntity
{
    public long UserId { get; set; }

    public long FriendId { get; set; }

    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime DateStart { get; set; }

    public virtual UserEntity Friend { get; set; } = null!;

    public virtual UserEntity User { get; set; } = null!;

    public Friend ToDomainEntity()
    {
        return new Friend
        {
            Name = Friend.Login,
            FriendSince = DateStart
        };
    }
}
