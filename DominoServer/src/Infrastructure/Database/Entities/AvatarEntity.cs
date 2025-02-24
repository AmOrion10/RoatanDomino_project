namespace Infrastructure.Database.Entities;

public class AvatarEntity
{
    public long Id { get; set; }

    public byte[] Image { get; set; }

    public virtual UserEntity User { get; set; } = null!;
}
