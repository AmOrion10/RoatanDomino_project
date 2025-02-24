using DominoCommon.com.domino.common.Scripts.Networking;

namespace Domain.Game;

public class GameAction(GameActionType type, List<GameMove>? possibleMoves = null, Domino? domino = null)
{
    public GameActionType Type { get; set; } = type;
    public List<GameMove>? PossibleMoves { get; set; } = possibleMoves;
    public GameMove? PlacedDomino { get; set; }
    public Domino? DrawDomino { get; set; } = domino;
}
