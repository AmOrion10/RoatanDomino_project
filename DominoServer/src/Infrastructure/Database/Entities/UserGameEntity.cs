namespace Infrastructure.Database.Entities;

public class UserGameEntity
{
    public long GameId { get; set; }

    public long UserId { get; set; }

    public int ScoreUpdate { get; set; }

    public virtual GameEntity Game { get; set; } = null!;

    public virtual UserEntity User { get; set; } = null!;
}
