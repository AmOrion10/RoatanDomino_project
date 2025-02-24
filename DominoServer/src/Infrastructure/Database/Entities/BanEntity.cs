namespace Infrastructure.Database.Entities;

public class BanEntity
{
    public long Id { get; set; }
    
    public long UserId { get; set; }

    public DateTime StartBan { get; set; }

    public DateTime EndBan { get; set; }

    public string? Reason { get; set; }

    public virtual UserEntity BannedUser { get; set; } = null!;
}
