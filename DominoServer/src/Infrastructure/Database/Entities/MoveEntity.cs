using Domain.Game;
using DominoCommon.com.domino.common.Scripts.Networking;

namespace Infrastructure.Database.Entities;

public class MoveEntity
{
    public long Id { get; set; }

    public int Row { get; set; }

    public int Col { get; set; }

    public Orientation Orientation { get; set; }

    public int Order { get; set; }

    public long GameId { get; set; }

    public long? UserId { get; set; }

    public short DominoId { get; set; }

    public virtual DominoEntity Domino { get; set; } = null!;

    public virtual GameEntity Game { get; set; } = null!;

    public virtual UserEntity? User { get; set; } = null!;

    public GameMove ToDomainEntity()
    {
        return new GameMove(Domino.ToDomainEntity(), Row, Col, Orientation, UserId.Value);
    }
}
