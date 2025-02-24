namespace Infrastructure.Database.Entities;

public class RoleEntity
{
    public long Id { get; set; }

    public string Name { get; set; }

    public virtual ICollection<UserRoleEntity> UserRoles { get; set; } = [];
}
