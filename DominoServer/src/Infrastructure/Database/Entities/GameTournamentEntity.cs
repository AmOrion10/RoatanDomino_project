namespace Infrastructure.Database.Entities;

public class GameTournamentEntity
{
    public long TournamentId { get; set; }

    public long GameId { get; set; }

    public virtual TournamentEntity Tournament { get; set; } = null!;

    public virtual GameEntity Game { get; set; } = null!;
}
