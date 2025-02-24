using Domain.Game;

namespace Infrastructure.Database.Entities;

public class DominoEntity
{
    public short Id { get; set; }

    public short Side1 { get; set; }

    public short Side2 { get; set; }

    public Domino ToDomainEntity()
    {
        return new Domino((int)Side1, (int)Side2);
    }
}
