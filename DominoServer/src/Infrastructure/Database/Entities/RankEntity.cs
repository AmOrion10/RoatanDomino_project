namespace Infrastructure.Database.Entities;

public class RankEntity
{
    public long Id { get; set; }

    public string Name { get; set; }

    public int MinPoints { get; set; }

    public int MaxPoints { get; set; }

    public virtual ICollection<UserEntity> Users { get; set; }
}
