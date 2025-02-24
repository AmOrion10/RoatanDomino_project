using System;
using System.Collections.Generic;

using DominoCommon.com.domino.common.Scripts.Networking;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;

public class NearestDomino
{
    public int Col { get; set; }
    public int Row { get; set; }
    public DominoBoard Domino { get; set; }
    public DominoRelativePosition RelativePosition { get; set; }

    public NearestDomino(DominoBoard domino, DominoRelativePosition relativePosition)
    {
        Domino = domino;
        RelativePosition = relativePosition;
    }

    public NearestDomino(DominoBoard domino, DominoRelativePosition relativePosition, int row, int col) : this(domino, relativePosition)
    {
        Row = row;
        Col = col;
    }

    public static NearestDomino GetNearestDomino(MoveDto move, GameGridDto gridIndex, List<DominoBoard> dominoes)
    {
        switch (move.Orientation)
        {
            case Orientation.HorizontalLeft:
                if (gridIndex[move.Row][move.Col + 1] != GameGridDto.EmptySpotValue)
                    return new NearestDomino(dominoes[gridIndex[move.Row][move.Col + 1]], DominoRelativePosition.Left, move.Row, move.Col + 1);
                if (gridIndex[move.Row + 1][move.Col] != GameGridDto.EmptySpotValue)
                    return new NearestDomino(dominoes[gridIndex[move.Row + 1][move.Col]], DominoRelativePosition.Top, move.Row + 1, move.Col);
                return new NearestDomino(dominoes[gridIndex[move.Row - 1][move.Col]], DominoRelativePosition.Bottom, move.Row - 1, move.Col);

            case Orientation.HorizontalRight:
                if (gridIndex[move.Row][move.Col - 1] != GameGridDto.EmptySpotValue)
                    return new NearestDomino(dominoes[gridIndex[move.Row][move.Col - 1]], DominoRelativePosition.Right, move.Row, move.Col - 1);
                if (gridIndex[move.Row + 1][move.Col] != GameGridDto.EmptySpotValue)
                    return new NearestDomino(dominoes[gridIndex[move.Row + 1][move.Col]], DominoRelativePosition.Top, move.Row + 1, move.Col);
                return new NearestDomino(dominoes[gridIndex[move.Row - 1][move.Col]], DominoRelativePosition.Bottom, move.Row - 1, move.Col);

            case Orientation.VerticalUp:
                if (gridIndex[move.Row][move.Col + 1] != GameGridDto.EmptySpotValue)
                    return new NearestDomino(dominoes[gridIndex[move.Row][move.Col + 1]], DominoRelativePosition.Left, move.Row, move.Col + 1);
                if (gridIndex[move.Row + 1][move.Col] != GameGridDto.EmptySpotValue)
                    return new NearestDomino(dominoes[gridIndex[move.Row + 1][move.Col]], DominoRelativePosition.Top, move.Row + 1, move.Col);
                return new NearestDomino(dominoes[gridIndex[move.Row][move.Col - 1]], DominoRelativePosition.Right, move.Row, move.Col - 1);

            case Orientation.VerticalDown:
                if (gridIndex[move.Row][move.Col + 1] != GameGridDto.EmptySpotValue)
                    return new NearestDomino(dominoes[gridIndex[move.Row][move.Col + 1]], DominoRelativePosition.Left, move.Row, move.Col + 1);
                if (gridIndex[move.Row - 1][move.Col] != GameGridDto.EmptySpotValue)
                    return new NearestDomino(dominoes[gridIndex[move.Row - 1][move.Col]], DominoRelativePosition.Bottom, move.Row - 1, move.Col);
                return new NearestDomino(dominoes[gridIndex[move.Row][move.Col - 1]], DominoRelativePosition.Right, move.Row, move.Col - 1);

            case Orientation.PerpendicularHorizontalFromTop:
                return new NearestDomino(dominoes[gridIndex[move.Row - 1][move.Col]], DominoRelativePosition.Bottom);

            case Orientation.PerpendicularHorizontalFromBottom:
                return new NearestDomino(dominoes[gridIndex[move.Row + 1][move.Col]], DominoRelativePosition.Top);

            case Orientation.PerpendicularVerticalFromLeft:
                return new NearestDomino(dominoes[gridIndex[move.Row][move.Col - 1]], DominoRelativePosition.Right);

            case Orientation.PerpendicularVerticalFromRight:
                return new NearestDomino(dominoes[gridIndex[move.Row][move.Col + 1]], DominoRelativePosition.Left);

            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}