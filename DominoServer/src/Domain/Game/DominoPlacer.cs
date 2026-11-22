using DominoCommon.com.domino.common.Scripts.Networking;

namespace Domain.Game;

public class DominoPlacer(GameGridDto gridDto)
{
    public MoveRecorder MoveHistory = new();
    private readonly MoveValidator validator = new(gridDto);
    private List<DominoEndPosition> endDominoes = [];
    private int openEndA = -1;
    private int openEndB = -1;

    public (int End1, int End2)? GetOpenEnds()
    {
        if (openEndA < 0 || openEndB < 0) return null;
        return (openEndA, openEndB);
    }

    public void PlaceFirstDomino(GamePlayer currentPlayer, GameMove move)
    {
        AddFirstDomino(move);
        currentPlayer.Hand.Remove(move.Domino);
    }

    public bool PlaceDomino(GamePlayer currentPlayer, GameMove move)
    {
        if (CanPlaceDomino(move) && currentPlayer.Hand.Contains(move.Domino))
        {
            AddDomino(move);
            currentPlayer.Hand.Remove(move.Domino);
            return true;
        }
        return false;
    }

    private void AddFirstDomino(GameMove move)
    {
        switch (move.Orientation)
        {
            case Orientation.HorizontalLeft:
                bool normalOrientation = gridDto.Grid[move.Row][move.Col + 1] == move.Domino.Side1 ||
                    gridDto.Grid[move.Row - 1][move.Col] == move.Domino.Side1 ||
                    gridDto.Grid[move.Row + 1][move.Col] == move.Domino.Side1;
                gridDto.Grid[move.Row][move.Col] = normalOrientation ? move.Domino.Side1 : move.Domino.Side2;
                gridDto.Grid[move.Row][move.Col - 1] = normalOrientation ? move.Domino.Side2 : move.Domino.Side1;
                endDominoes.Add(new DominoEndPosition(new GridCell(move.Row, move.Col + 1), move.Domino));
                break;
            case Orientation.PerpendicularVerticalFromLeft:
                gridDto.Grid[move.Row - 1][move.Col] = move.Domino.Side1;
                gridDto.Grid[move.Row][move.Col] = move.Domino.Side1;
                gridDto.Grid[move.Row + 1][move.Col] = move.Domino.Side1;
                endDominoes.Add(new DominoEndPosition(new GridCell(move.Row, move.Col - 1), move.Domino));
                break;
        }
        openEndA = move.Domino.Side1;
        openEndB = move.Domino.Side2;
        MoveHistory.AddToHistory(move);
        UpdatePlacementPositions(move);
    }

    private bool CanPlaceDomino(GameMove move)
    {
        if (!MovementOnAnEndPos(move)) return false;

        if (move.Row < 0 || move.Col < 0 || move.Row > gridDto.Row - 1 || move.Col > gridDto.Col - 1) return false;

        return move.Orientation switch
        {
            Orientation.HorizontalLeft => validator.CanPlaceHorizontal(move.Domino, move.Row, move.Col, previousDominoOnLeft: false),
            Orientation.HorizontalRight => validator.CanPlaceHorizontal(move.Domino, move.Row, move.Col, previousDominoOnLeft: true),
            Orientation.VerticalUp => validator.CanPlaceVertical(move.Domino, move.Row, move.Col, previousDominoAtTop: false),
            Orientation.VerticalDown => validator.CanPlaceVertical(move.Domino, move.Row, move.Col, previousDominoAtTop: true),
            Orientation.PerpendicularHorizontalFromBottom => validator.CanPlaceDoubleHorizontal(move.Domino, move.Row, move.Col, previousDominoUp: false),
            Orientation.PerpendicularHorizontalFromTop => validator.CanPlaceDoubleHorizontal(move.Domino, move.Row, move.Col, previousDominoUp: true),
            Orientation.PerpendicularVerticalFromLeft => validator.CanPlaceDoubleVertical(move.Domino, move.Row, move.Col, previousDominoLeft: true),
            Orientation.PerpendicularVerticalFromRight => validator.CanPlaceDoubleVertical(move.Domino, move.Row, move.Col, previousDominoLeft: false),
            _ => false
        };
    }

    public List<GameMove> GetDominoPossibleMoves(Domino domino, long playerId)
    {
        List<GameMove> possibleMoves = [];

        foreach (DominoEndPosition endPos in endDominoes)
        {
            if (domino.Side1 != endPos.Domino.Side1 && domino.Side1 != endPos.Domino.Side2 && domino.Side2 != endPos.Domino.Side1 && domino.Side2 != endPos.Domino.Side2) continue;
            if(domino.IsDouble())
            {
                Orientation[] perpendicularOrientations = [Orientation.PerpendicularHorizontalFromBottom, Orientation.PerpendicularHorizontalFromTop, Orientation.PerpendicularVerticalFromLeft, Orientation.PerpendicularVerticalFromRight];
                foreach (Orientation orientation in perpendicularOrientations)
                {
                    if (CanPlaceDomino(new GameMove(domino, endPos.GridCell.Row, endPos.GridCell.Col, orientation, playerId)))
                    {
                        possibleMoves.Add(new GameMove(domino, endPos.GridCell.Row, endPos.GridCell.Col, orientation, playerId));
                    }
                }
                continue;
            }

            Orientation[] normalOrientations = [Orientation.HorizontalLeft, Orientation.HorizontalRight, Orientation.VerticalDown, Orientation.VerticalUp];
            foreach (Orientation orientation in normalOrientations)
            {
                if (CanPlaceDomino(new GameMove(domino, endPos.GridCell.Row, endPos.GridCell.Col, orientation, playerId)))
                {
                    possibleMoves.Add(new GameMove(domino, endPos.GridCell.Row, endPos.GridCell.Col, orientation, playerId));
                }
            }
        }
        return possibleMoves;
    }

    public List<GameMove> GetPlayerPossibleMoves(GamePlayer player)
    {
        return player.Hand
            .SelectMany(domino => GetDominoPossibleMoves(domino, player.Id))
            .ToList();
    }

    private void AddDomino(GameMove move)
    {
        bool normalOrientation;

        switch (move.Orientation)
        {
            case Orientation.HorizontalLeft:
                normalOrientation = (gridDto.Grid[move.Row][move.Col + 1] == move.Domino.Side1) || (gridDto.Grid[move.Row - 1][move.Col] == move.Domino.Side1) || (gridDto.Grid[move.Row + 1][move.Col] == move.Domino.Side1);
                gridDto.Grid[move.Row][move.Col] = normalOrientation ? move.Domino.Side1 : move.Domino.Side2;
                gridDto.Grid[move.Row][move.Col - 1] = normalOrientation ? move.Domino.Side2 : move.Domino.Side1;
                break;
            case Orientation.HorizontalRight:
                normalOrientation = (gridDto.Grid[move.Row][move.Col - 1] == move.Domino.Side1) || (gridDto.Grid[move.Row - 1][move.Col] == move.Domino.Side1) || (gridDto.Grid[move.Row + 1][move.Col] == move.Domino.Side1);
                gridDto.Grid[move.Row][move.Col] = normalOrientation ? move.Domino.Side1 : move.Domino.Side2;
                gridDto.Grid[move.Row][move.Col + 1] = normalOrientation ? move.Domino.Side2 : move.Domino.Side1;
                break;
            case Orientation.VerticalUp:
                normalOrientation = (gridDto.Grid[move.Row + 1][move.Col] == move.Domino.Side1) || (gridDto.Grid[move.Row][move.Col - 1] == move.Domino.Side1) || (gridDto.Grid[move.Row][move.Col + 1] == move.Domino.Side1);
                gridDto.Grid[move.Row][move.Col] = normalOrientation ? move.Domino.Side1 : move.Domino.Side2;
                gridDto.Grid[move.Row - 1][move.Col] = normalOrientation ? move.Domino.Side2 : move.Domino.Side1;
                break;
            case Orientation.VerticalDown:
                normalOrientation = (gridDto.Grid[move.Row - 1][move.Col] == move.Domino.Side1) || (gridDto.Grid[move.Row][move.Col - 1] == move.Domino.Side1) || (gridDto.Grid[move.Row][move.Col + 1] == move.Domino.Side1);
                gridDto.Grid[move.Row][move.Col] = normalOrientation ? move.Domino.Side1 : move.Domino.Side2;
                gridDto.Grid[move.Row + 1][move.Col] = normalOrientation ? move.Domino.Side2 : move.Domino.Side1;
                break;
            case Orientation.PerpendicularHorizontalFromBottom:
            case Orientation.PerpendicularHorizontalFromTop:
                gridDto.Grid[move.Row][move.Col - 1] = move.Domino.Side1;
                gridDto.Grid[move.Row][move.Col] = move.Domino.Side1;
                gridDto.Grid[move.Row][move.Col + 1] = move.Domino.Side1;
                break;
            case Orientation.PerpendicularVerticalFromLeft:
            case Orientation.PerpendicularVerticalFromRight:
                gridDto.Grid[move.Row - 1][move.Col] = move.Domino.Side1;
                gridDto.Grid[move.Row][move.Col] = move.Domino.Side1;
                gridDto.Grid[move.Row + 1][move.Col] = move.Domino.Side1;
                break;
        }
        int matchedPip = gridDto.Grid[move.Row][move.Col];
        int exposedPip = move.Domino.Side1 == matchedPip ? move.Domino.Side2 : move.Domino.Side1;
        if (openEndA == matchedPip)
        {
            openEndA = exposedPip;
        }
        else if (openEndB == matchedPip)
        {
            openEndB = exposedPip;
        }

        MoveHistory.AddToHistory(move);
        UpdatePlacementPositions(move);
    }

    private void UpdatePlacementPositions(GameMove move)
    {
        List<DominoEndPosition> newEndDominoes = endDominoes.Where(endPosition =>
            gridDto.Grid[endPosition.GridCell.Row][endPosition.GridCell.Col] == GameGridDto.EmptySpotValue).ToList();

        switch (move.Orientation)
        {
            case Orientation.HorizontalLeft:
                if (move.Col - 2 > 0)
                    newEndDominoes.Add(new DominoEndPosition(new GridCell(move.Row, move.Col - 2), move.Domino)); // newEndPos.Add(new GridCell(row, col - 2));
                break;
            case Orientation.HorizontalRight:
                if (move.Col + 2 < gridDto.Row - 1)
                    newEndDominoes.Add(new DominoEndPosition(new GridCell(move.Row, move.Col + 2), move.Domino)); //newEndPos.Add(new GridCell(move.Row, move.Col + 2));
                break;
            case Orientation.VerticalUp:
                if (move.Row - 2 > 0)
                    newEndDominoes.Add(new DominoEndPosition(new GridCell(move.Row - 2, move.Col), move.Domino)); //newEndPos.Add(new GridCell(move.Row - 2, move.Col));
                break;
            case Orientation.VerticalDown:
                if (move.Row + 2 < gridDto.Col - 1)
                    newEndDominoes.Add(new DominoEndPosition(new GridCell(move.Row + 2, move.Col), move.Domino)); //newEndPos.Add(new GridCell(move.Row + 2, move.Col));
                break;
            case Orientation.PerpendicularHorizontalFromBottom:
                if (move.Row - 1 > 0)
                    newEndDominoes.Add(new DominoEndPosition(new GridCell(move.Row - 1, move.Col), move.Domino)); //newEndPos.Add(new GridCell(move.Row - 1, move.Col));
                break;
            case Orientation.PerpendicularHorizontalFromTop:
                if (move.Row + 1 < gridDto.Row)
                    newEndDominoes.Add(new DominoEndPosition(new GridCell(move.Row + 1, move.Col), move.Domino)); //newEndPos.Add(new GridCell(move.Row + 1, move.Col));
                break;
            case Orientation.PerpendicularVerticalFromLeft:
                if (move.Col + 1 < gridDto.Col - 1)
                    newEndDominoes.Add(new DominoEndPosition(new GridCell(move.Row, move.Col + 1), move.Domino)); //newEndPos.Add(new GridCell(move.Row, move.Col + 1));
                break;
            case Orientation.PerpendicularVerticalFromRight:
                if (move.Col - 1 > 0)
                    newEndDominoes.Add(new DominoEndPosition(new GridCell(move.Row, move.Col - 1), move.Domino)); //newEndPos.Add(new GridCell(move.Row, move.Col - 1));
                break;
        }

        endDominoes.Clear();
        endDominoes = newEndDominoes;
    }

    private bool MovementOnAnEndPos(GameMove move)
    {
        return endDominoes.Any(endPos => endPos.GridCell.Row == move.Row && endPos.GridCell.Col == move.Col);
    }
}
