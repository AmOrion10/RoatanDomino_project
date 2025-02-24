namespace Domain.Game;

public class DominoEndPosition(GridCell gridCell, Domino domino)
{
    public GridCell GridCell { get; } = gridCell;
    public Domino Domino { get; } = domino;
}
