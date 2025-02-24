namespace Infrastructure.Database.Entities;

public class TournamentEntity
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public virtual ICollection<GameTournamentEntity> GameTournaments { get; set; } = [];

    public virtual ICollection<UserTournamentEntity> UserTournaments { get; set; } = [];
}
