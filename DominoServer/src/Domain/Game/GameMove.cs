using DominoCommon.com.domino.common.Scripts.Networking;

namespace Domain.Game;

public class GameMove(Domino domino, int row, int col, Orientation orientation, long playerId)
{
    public Domino Domino { get; } = domino;
    public int Row { get; } = row;
    public int Col { get; } = col;
    public Orientation Orientation { get; } = orientation;
    public long PlayerId { get; } = playerId;

    public override string ToString() // Temp: print in the console
    {
        return $"Place {Domino.Side1}-{Domino.Side2} at ({Row}, {Col}) {Orientation}";
    }
}
