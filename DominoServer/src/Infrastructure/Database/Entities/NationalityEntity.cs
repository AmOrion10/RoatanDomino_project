namespace Infrastructure.Database.Entities;

public class NationalityEntity
{
    public long Id { get; set; }

    public string Name { get; set; }

    public virtual ICollection<UserEntity> Users { get; set; } = null!;
}
