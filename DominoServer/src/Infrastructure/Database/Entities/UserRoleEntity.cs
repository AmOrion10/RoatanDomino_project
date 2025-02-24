namespace Infrastructure.Database.Entities;

public class UserRoleEntity
{
    public long UserId { get; set; }

    public long RoleId { get; set; }

    public virtual RoleEntity Role { get; set; } = null!;

    public virtual UserEntity User { get; set; } = null!;
}
