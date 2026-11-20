using System;
using System.Collections.Generic;
using System.Linq;

using DominoCommon.com.domino.common.Scripts.Networking;
using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;

public class OfflineGameEngine
{
    public const int DominoesPerPlayer = 7;
    public const int NumberOfRows = 60;
    public const int NumberOfCols = 60;

    public class OfflinePlayer
    {
        public PlayerDto Info { get; set; }
        public bool IsBot { get; set; }
        public BotDifficulty? BotDifficulty { get; set; }
        public List<DominoDto> Hand { get; } = new List<DominoDto>();

        public int HandPipSum()
        {
            int sum = 0;
            foreach (DominoDto domino in Hand)
            {
                sum += domino.Side1 + domino.Side2;
            }
            return sum;
        }
    }

    public class EndPosition
    {
        public int Row { get; set; }
        public int Col { get; set; }
        public DominoDto Domino { get; set; }
        public int OpenPipValue { get; set; }
    }

    public List<OfflinePlayer> Players { get; } = new List<OfflinePlayer>();
    public OfflinePlayer CurrentPlayer => Players[currentPlayerIndex];
    public int BoneyardCount => stack.Count;
    public MoveDto LastMove { get; private set; }
    public List<DominoDto> PlayedDominoes { get; } = new List<DominoDto>();

    private readonly GameGridDto gridDto = new GameGridDto(NumberOfRows, NumberOfCols);
    private readonly List<DominoDto> stack = new List<DominoDto>();
    private readonly List<EndPosition> endPositions = new List<EndPosition>();
    private readonly Random rng;
    private int currentPlayerIndex;

    public OfflineGameEngine(List<OfflinePlayer> players, int? seed = null)
    {
        rng = seed.HasValue ? new Random(seed.Value) : new Random();
        Players.AddRange(players);
        BuildAndShuffleStack();
    }

    private void BuildAndShuffleStack()
    {
        stack.Clear();
        for (int i = 0; i <= 6; i++)
        {
            for (int j = i; j <= 6; j++)
            {
                stack.Add(new DominoDto { Side1 = i, Side2 = j });
            }
        }

        for (int i = stack.Count - 1; i > 0; i--)
        {
            int swapIndex = rng.Next(i + 1);
            DominoDto temp = stack[i];
            stack[i] = stack[swapIndex];
            stack[swapIndex] = temp;
        }
    }

    public void DistributeDominoes()
    {
        foreach (OfflinePlayer player in Players)
        {
            player.Hand.Clear();
            for (int i = 0; i < DominoesPerPlayer; i++)
            {
                DominoDto drawn = PopStack();
                if (drawn == null) break;
                player.Hand.Add(drawn);
            }
        }
    }

    public MoveDto PlaceFirstDomino()
    {
        OfflinePlayer starter;
        DominoDto firstDomino;
        FindPlayerWithBiggestDomino(focusOnDouble: true, out starter, out firstDomino);

        Orientation orientation;
        if (starter != null && firstDomino != null)
        {
            orientation = Orientation.PerpendicularVerticalFromLeft;
        }
        else
        {
            FindPlayerWithBiggestDomino(focusOnDouble: false, out starter, out firstDomino);
            orientation = Orientation.HorizontalLeft;
        }

        MoveDto move = new MoveDto
        {
            Domino = CloneDomino(firstDomino),
            Row = NumberOfRows / 2,
            Col = NumberOfCols / 2,
            Orientation = orientation,
            PlayerId = starter.Info.Id
        };

        AddFirstDominoToGrid(move);
        RemoveDominoFromHand(starter, firstDomino);
        PlayedDominoes.Add(CloneDomino(firstDomino));
        LastMove = move;
        currentPlayerIndex = Players.FindIndex(p => p.Info.Id == starter.Info.Id);
        return move;
    }

    public bool PlaceDomino(MoveDto move)
    {
        if (move == null || move.Domino == null) return false;
        if (move.PlayerId != CurrentPlayer.Info.Id) return false;
        if (!CanPlaceDomino(move)) return false;

        DominoDto inHand = FindDominoInHand(CurrentPlayer, move.Domino);
        if (inHand == null) return false;

        // Keep exact hand Side1/Side2 ordering for UI matching
        MoveDto canonicalMove = new MoveDto
        {
            Domino = CloneDomino(inHand),
            Row = move.Row,
            Col = move.Col,
            Orientation = move.Orientation,
            PlayerId = move.PlayerId
        };

        AddDominoToGrid(canonicalMove);
        CurrentPlayer.Hand.Remove(inHand);
        PlayedDominoes.Add(CloneDomino(inHand));
        LastMove = canonicalMove;
        return true;
    }

    public DominoDto Draw(long playerId)
    {
        if (playerId != CurrentPlayer.Info.Id) return null;
        DominoDto domino = PopStack();
        if (domino != null)
        {
            CurrentPlayer.Hand.Add(domino);
        }
        return domino;
    }

    public void NextTurn()
    {
        currentPlayerIndex = (currentPlayerIndex + 1) % Players.Count;
    }

    public OfflinePlayer PeekNextPlayer()
    {
        return Players[(currentPlayerIndex + 1) % Players.Count];
    }

    public List<MoveDto> GetPlayerPossibleMoves(OfflinePlayer player)
    {
        List<MoveDto> result = new List<MoveDto>();
        foreach (DominoDto domino in player.Hand)
        {
            result.AddRange(GetDominoPossibleMoves(domino, player.Info.Id));
        }
        return result;
    }

    public List<MoveDto> GetDominoPossibleMoves(DominoDto domino, long playerId)
    {
        List<MoveDto> possibleMoves = new List<MoveDto>();

        foreach (EndPosition endPos in endPositions)
        {
            if (domino.Side1 != endPos.Domino.Side1 && domino.Side1 != endPos.Domino.Side2 &&
                domino.Side2 != endPos.Domino.Side1 && domino.Side2 != endPos.Domino.Side2)
            {
                continue;
            }

            if (domino.IsDouble())
            {
                Orientation[] perpendicularOrientations =
                {
                    Orientation.PerpendicularHorizontalFromBottom,
                    Orientation.PerpendicularHorizontalFromTop,
                    Orientation.PerpendicularVerticalFromLeft,
                    Orientation.PerpendicularVerticalFromRight
                };

                foreach (Orientation orientation in perpendicularOrientations)
                {
                    MoveDto candidate = new MoveDto
                    {
                        Domino = CloneDomino(domino),
                        Row = endPos.Row,
                        Col = endPos.Col,
                        Orientation = orientation,
                        PlayerId = playerId
                    };

                    if (CanPlaceDomino(candidate))
                    {
                        possibleMoves.Add(candidate);
                    }
                }
                continue;
            }

            Orientation[] normalOrientations =
            {
                Orientation.HorizontalLeft,
                Orientation.HorizontalRight,
                Orientation.VerticalDown,
                Orientation.VerticalUp
            };

            foreach (Orientation orientation in normalOrientations)
            {
                MoveDto candidate = new MoveDto
                {
                    Domino = CloneDomino(domino),
                    Row = endPos.Row,
                    Col = endPos.Col,
                    Orientation = orientation,
                    PlayerId = playerId
                };

                if (CanPlaceDomino(candidate))
                {
                    possibleMoves.Add(candidate);
                }
            }
        }

        return possibleMoves;
    }

    public List<int> GetOpenEndPipValues()
    {
        List<int> values = new List<int>();
        foreach (EndPosition endPos in endPositions)
        {
            values.Add(endPos.OpenPipValue);
        }
        return values;
    }

    public int GetExposedPipAfterMove(MoveDto move)
    {
        int matchedPip = GetMatchedPipForMove(move);
        if (move.Domino.IsDouble())
        {
            return move.Domino.Side1;
        }
        return move.Domino.Side1 == matchedPip ? move.Domino.Side2 : move.Domino.Side1;
    }

    public List<int> GetOpenEndsAfterMove(MoveDto move)
    {
        List<int> nextEnds = new List<int>();
        foreach (EndPosition endPos in endPositions)
        {
            if (endPos.Row == move.Row && endPos.Col == move.Col)
            {
                continue;
            }
            nextEnds.Add(endPos.OpenPipValue);
        }
        nextEnds.Add(GetExposedPipAfterMove(move));
        return nextEnds;
    }

    public Dictionary<long, int> GetNumberOfDominoPerPlayer()
    {
        Dictionary<long, int> counts = new Dictionary<long, int>();
        foreach (OfflinePlayer player in Players)
        {
            counts[player.Info.Id] = player.Hand.Count;
        }
        return counts;
    }

    public bool IsGameOver()
    {
        if (Players.Any(p => p.Hand.Count == 0))
        {
            return true;
        }

        if (stack.Count > 0)
        {
            return false;
        }

        foreach (OfflinePlayer player in Players)
        {
            if (GetPlayerPossibleMoves(player).Count > 0)
            {
                return false;
            }
        }
        return true;
    }

    public List<OfflinePlayer> GetRankings()
    {
        return Players
            .OrderBy(p => p.HandPipSum())
            .ThenBy(p => p.Hand.Count)
            .ToList();
    }

    public EndGameDto BuildEndGameDto()
    {
        List<OfflinePlayer> ranked = GetRankings();
        List<PlayerRankingsDto> rankingDtos = new List<PlayerRankingsDto>();
        for (int i = 0; i < ranked.Count; i++)
        {
            rankingDtos.Add(new PlayerRankingsDto
            {
                Id = ranked[i].Info.Id,
                Rank = i + 1,
                Gain = i == 0 ? 25 : 0
            });
        }

        return new EndGameDto
        {
            NormalFinish = true,
            Rankings = rankingDtos
        };
    }

    private DominoDto PopStack()
    {
        if (stack.Count == 0) return null;
        int lastIndex = stack.Count - 1;
        DominoDto domino = stack[lastIndex];
        stack.RemoveAt(lastIndex);
        return domino;
    }

    private void FindPlayerWithBiggestDomino(bool focusOnDouble, out OfflinePlayer bestPlayer, out DominoDto bestDomino)
    {
        bestPlayer = null;
        bestDomino = null;
        int bestSum = -1;

        foreach (OfflinePlayer player in Players)
        {
            foreach (DominoDto domino in player.Hand)
            {
                if (focusOnDouble && !domino.IsDouble()) continue;
                int sum = domino.Side1 + domino.Side2;
                if (sum <= bestSum) continue;
                bestSum = sum;
                bestDomino = domino;
                bestPlayer = player;
            }
        }
    }

    private static DominoDto FindDominoInHand(OfflinePlayer player, DominoDto target)
    {
        foreach (DominoDto d in player.Hand)
        {
            if ((d.Side1 == target.Side1 && d.Side2 == target.Side2) ||
                (d.Side1 == target.Side2 && d.Side2 == target.Side1))
            {
                return d;
            }
        }
        return null;
    }

    private static void RemoveDominoFromHand(OfflinePlayer player, DominoDto target)
    {
        DominoDto found = FindDominoInHand(player, target);
        if (found != null)
        {
            player.Hand.Remove(found);
        }
    }

    private static DominoDto CloneDomino(DominoDto d)
    {
        return new DominoDto { Side1 = d.Side1, Side2 = d.Side2 };
    }

    private int GetMatchedPipForMove(MoveDto move)
    {
        EndPosition end = endPositions.FirstOrDefault(e => e.Row == move.Row && e.Col == move.Col);
        if (end != null)
        {
            return end.OpenPipValue;
        }
        return move.Domino.Side1;
    }

    private void AddFirstDominoToGrid(MoveDto move)
    {
        switch (move.Orientation)
        {
            case Orientation.HorizontalLeft:
                bool normalOrientation =
                    gridDto.Grid[move.Row][move.Col + 1] == move.Domino.Side1 ||
                    gridDto.Grid[move.Row - 1][move.Col] == move.Domino.Side1 ||
                    gridDto.Grid[move.Row + 1][move.Col] == move.Domino.Side1;
                int rightPip = normalOrientation ? move.Domino.Side1 : move.Domino.Side2;
                int leftPip = normalOrientation ? move.Domino.Side2 : move.Domino.Side1;
                gridDto.Grid[move.Row][move.Col] = rightPip;
                gridDto.Grid[move.Row][move.Col - 1] = leftPip;
                endPositions.Add(new EndPosition
                {
                    Row = move.Row,
                    Col = move.Col + 1,
                    Domino = move.Domino,
                    OpenPipValue = rightPip
                });
                UpdatePlacementPositions(move, leftPip);
                break;

            case Orientation.PerpendicularVerticalFromLeft:
                gridDto.Grid[move.Row - 1][move.Col] = move.Domino.Side1;
                gridDto.Grid[move.Row][move.Col] = move.Domino.Side1;
                gridDto.Grid[move.Row + 1][move.Col] = move.Domino.Side1;
                endPositions.Add(new EndPosition
                {
                    Row = move.Row,
                    Col = move.Col - 1,
                    Domino = move.Domino,
                    OpenPipValue = move.Domino.Side1
                });
                UpdatePlacementPositions(move, move.Domino.Side1);
                break;
        }
    }

    private void AddDominoToGrid(MoveDto move)
    {
        bool normalOrientation;
        int outerPip = move.Domino.Side1;

        switch (move.Orientation)
        {
            case Orientation.HorizontalLeft:
                normalOrientation =
                    gridDto.Grid[move.Row][move.Col + 1] == move.Domino.Side1 ||
                    gridDto.Grid[move.Row - 1][move.Col] == move.Domino.Side1 ||
                    gridDto.Grid[move.Row + 1][move.Col] == move.Domino.Side1;
                gridDto.Grid[move.Row][move.Col] = normalOrientation ? move.Domino.Side1 : move.Domino.Side2;
                outerPip = normalOrientation ? move.Domino.Side2 : move.Domino.Side1;
                gridDto.Grid[move.Row][move.Col - 1] = outerPip;
                break;

            case Orientation.HorizontalRight:
                normalOrientation =
                    gridDto.Grid[move.Row][move.Col - 1] == move.Domino.Side1 ||
                    gridDto.Grid[move.Row - 1][move.Col] == move.Domino.Side1 ||
                    gridDto.Grid[move.Row + 1][move.Col] == move.Domino.Side1;
                gridDto.Grid[move.Row][move.Col] = normalOrientation ? move.Domino.Side1 : move.Domino.Side2;
                outerPip = normalOrientation ? move.Domino.Side2 : move.Domino.Side1;
                gridDto.Grid[move.Row][move.Col + 1] = outerPip;
                break;

            case Orientation.VerticalUp:
                normalOrientation =
                    gridDto.Grid[move.Row + 1][move.Col] == move.Domino.Side1 ||
                    gridDto.Grid[move.Row][move.Col - 1] == move.Domino.Side1 ||
                    gridDto.Grid[move.Row][move.Col + 1] == move.Domino.Side1;
                gridDto.Grid[move.Row][move.Col] = normalOrientation ? move.Domino.Side1 : move.Domino.Side2;
                outerPip = normalOrientation ? move.Domino.Side2 : move.Domino.Side1;
                gridDto.Grid[move.Row - 1][move.Col] = outerPip;
                break;

            case Orientation.VerticalDown:
                normalOrientation =
                    gridDto.Grid[move.Row - 1][move.Col] == move.Domino.Side1 ||
                    gridDto.Grid[move.Row][move.Col - 1] == move.Domino.Side1 ||
                    gridDto.Grid[move.Row][move.Col + 1] == move.Domino.Side1;
                gridDto.Grid[move.Row][move.Col] = normalOrientation ? move.Domino.Side1 : move.Domino.Side2;
                outerPip = normalOrientation ? move.Domino.Side2 : move.Domino.Side1;
                gridDto.Grid[move.Row + 1][move.Col] = outerPip;
                break;

            case Orientation.PerpendicularHorizontalFromBottom:
            case Orientation.PerpendicularHorizontalFromTop:
                gridDto.Grid[move.Row][move.Col - 1] = move.Domino.Side1;
                gridDto.Grid[move.Row][move.Col] = move.Domino.Side1;
                gridDto.Grid[move.Row][move.Col + 1] = move.Domino.Side1;
                outerPip = move.Domino.Side1;
                break;

            case Orientation.PerpendicularVerticalFromLeft:
            case Orientation.PerpendicularVerticalFromRight:
                gridDto.Grid[move.Row - 1][move.Col] = move.Domino.Side1;
                gridDto.Grid[move.Row][move.Col] = move.Domino.Side1;
                gridDto.Grid[move.Row + 1][move.Col] = move.Domino.Side1;
                outerPip = move.Domino.Side1;
                break;
        }

        UpdatePlacementPositions(move, outerPip);
    }

    private void UpdatePlacementPositions(MoveDto move, int outerPip)
    {
        List<EndPosition> nextEnds = endPositions
            .Where(ep => gridDto.Grid[ep.Row][ep.Col] == GameGridDto.EmptySpotValue)
            .ToList();

        switch (move.Orientation)
        {
            case Orientation.HorizontalLeft:
                if (move.Col - 2 > 0)
                    nextEnds.Add(new EndPosition { Row = move.Row, Col = move.Col - 2, Domino = move.Domino, OpenPipValue = outerPip });
                break;
            case Orientation.HorizontalRight:
                if (move.Col + 2 < gridDto.Row - 1)
                    nextEnds.Add(new EndPosition { Row = move.Row, Col = move.Col + 2, Domino = move.Domino, OpenPipValue = outerPip });
                break;
            case Orientation.VerticalUp:
                if (move.Row - 2 > 0)
                    nextEnds.Add(new EndPosition { Row = move.Row - 2, Col = move.Col, Domino = move.Domino, OpenPipValue = outerPip });
                break;
            case Orientation.VerticalDown:
                if (move.Row + 2 < gridDto.Col - 1)
                    nextEnds.Add(new EndPosition { Row = move.Row + 2, Col = move.Col, Domino = move.Domino, OpenPipValue = outerPip });
                break;
            case Orientation.PerpendicularHorizontalFromBottom:
                if (move.Row - 1 > 0)
                    nextEnds.Add(new EndPosition { Row = move.Row - 1, Col = move.Col, Domino = move.Domino, OpenPipValue = outerPip });
                break;
            case Orientation.PerpendicularHorizontalFromTop:
                if (move.Row + 1 < gridDto.Row)
                    nextEnds.Add(new EndPosition { Row = move.Row + 1, Col = move.Col, Domino = move.Domino, OpenPipValue = outerPip });
                break;
            case Orientation.PerpendicularVerticalFromLeft:
                if (move.Col + 1 < gridDto.Col - 1)
                    nextEnds.Add(new EndPosition { Row = move.Row, Col = move.Col + 1, Domino = move.Domino, OpenPipValue = outerPip });
                break;
            case Orientation.PerpendicularVerticalFromRight:
                if (move.Col - 1 > 0)
                    nextEnds.Add(new EndPosition { Row = move.Row, Col = move.Col - 1, Domino = move.Domino, OpenPipValue = outerPip });
                break;
        }

        endPositions.Clear();
        endPositions.AddRange(nextEnds);
    }

    private bool CanPlaceDomino(MoveDto move)
    {
        if (!endPositions.Any(e => e.Row == move.Row && e.Col == move.Col)) return false;
        if (move.Row < 0 || move.Col < 0 || move.Row > gridDto.Row - 1 || move.Col > gridDto.Col - 1) return false;

        switch (move.Orientation)
        {
            case Orientation.HorizontalLeft:
                return CanPlaceHorizontal(move.Domino, move.Row, move.Col, previousDominoOnLeft: false);
            case Orientation.HorizontalRight:
                return CanPlaceHorizontal(move.Domino, move.Row, move.Col, previousDominoOnLeft: true);
            case Orientation.VerticalUp:
                return CanPlaceVertical(move.Domino, move.Row, move.Col, previousDominoAtTop: false);
            case Orientation.VerticalDown:
                return CanPlaceVertical(move.Domino, move.Row, move.Col, previousDominoAtTop: true);
            case Orientation.PerpendicularHorizontalFromBottom:
                return CanPlaceDoubleHorizontal(move.Domino, move.Row, move.Col, previousDominoUp: false);
            case Orientation.PerpendicularHorizontalFromTop:
                return CanPlaceDoubleHorizontal(move.Domino, move.Row, move.Col, previousDominoUp: true);
            case Orientation.PerpendicularVerticalFromLeft:
                return CanPlaceDoubleVertical(move.Domino, move.Row, move.Col, previousDominoLeft: true);
            case Orientation.PerpendicularVerticalFromRight:
                return CanPlaceDoubleVertical(move.Domino, move.Row, move.Col, previousDominoLeft: false);
            default:
                return false;
        }
    }

    private bool CanPlaceHorizontal(DominoDto domino, int row, int col, bool previousDominoOnLeft)
    {
        if (row <= 1 || row >= gridDto.Row - 2 || col <= 1 || col >= gridDto.Col - 2) return false;

        if ((gridDto.Grid[row - 1][col - 1] != GameGridDto.EmptySpotValue && gridDto.Grid[row - 1][col + 1] != GameGridDto.EmptySpotValue) ||
            (gridDto.Grid[row + 1][col - 1] != GameGridDto.EmptySpotValue && gridDto.Grid[row + 1][col + 1] != GameGridDto.EmptySpotValue))
        {
            return false;
        }

        if (previousDominoOnLeft)
        {
            if (col + 3 > gridDto.Col - 1) return false;
            if (gridDto.Grid[row][col] != GameGridDto.EmptySpotValue || gridDto.Grid[row][col + 1] != GameGridDto.EmptySpotValue) return false;
            if (gridDto.Grid[row - 1][col + 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row + 1][col + 1] != GameGridDto.EmptySpotValue ||
                gridDto.Grid[row - 1][col + 2] != GameGridDto.EmptySpotValue || gridDto.Grid[row + 1][col + 2] != GameGridDto.EmptySpotValue)
            {
                return false;
            }
            if ((gridDto.Grid[row][col - 1] != domino.Side1 && gridDto.Grid[row][col - 1] != domino.Side2) &&
                (gridDto.Grid[row - 1][col] != domino.Side2 && gridDto.Grid[row - 1][col] != domino.Side1) &&
                (gridDto.Grid[row + 1][col] != domino.Side2 && gridDto.Grid[row + 1][col] != domino.Side1))
            {
                return false;
            }
            if (gridDto.Grid[row][col + 2] != GameGridDto.EmptySpotValue || gridDto.Grid[row][col + 3] != GameGridDto.EmptySpotValue) return false;
        }
        else
        {
            if (col - 3 <= 0) return false;
            if (gridDto.Grid[row][col] != GameGridDto.EmptySpotValue || gridDto.Grid[row][col - 1] != GameGridDto.EmptySpotValue) return false;
            if (gridDto.Grid[row - 1][col - 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row + 1][col - 1] != GameGridDto.EmptySpotValue ||
                gridDto.Grid[row - 1][col - 2] != GameGridDto.EmptySpotValue || gridDto.Grid[row + 1][col - 2] != GameGridDto.EmptySpotValue)
            {
                return false;
            }
            if ((gridDto.Grid[row][col + 1] != domino.Side1 && gridDto.Grid[row][col + 1] != domino.Side2) &&
                (gridDto.Grid[row - 1][col] != domino.Side2 && gridDto.Grid[row - 1][col] != domino.Side1) &&
                (gridDto.Grid[row + 1][col] != domino.Side2 && gridDto.Grid[row + 1][col] != domino.Side1))
            {
                return false;
            }
            if (gridDto.Grid[row][col - 2] != GameGridDto.EmptySpotValue || gridDto.Grid[row][col - 3] != GameGridDto.EmptySpotValue) return false;
        }

        return true;
    }

    private bool CanPlaceVertical(DominoDto domino, int row, int col, bool previousDominoAtTop)
    {
        if (row <= 1 || row >= gridDto.Row - 2 || col <= 1 || col >= gridDto.Col - 2) return false;

        if ((gridDto.Grid[row - 1][col - 1] != GameGridDto.EmptySpotValue && gridDto.Grid[row + 1][col - 1] != GameGridDto.EmptySpotValue) ||
            (gridDto.Grid[row - 1][col + 1] != GameGridDto.EmptySpotValue && gridDto.Grid[row + 1][col + 1] != GameGridDto.EmptySpotValue))
        {
            return false;
        }

        if (previousDominoAtTop)
        {
            if (row + 3 > gridDto.Row - 1) return false;
            if (gridDto.Grid[row][col] != GameGridDto.EmptySpotValue || gridDto.Grid[row + 1][col] != GameGridDto.EmptySpotValue) return false;
            if (gridDto.Grid[row + 1][col - 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row + 1][col + 1] != GameGridDto.EmptySpotValue ||
                gridDto.Grid[row + 2][col - 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row + 2][col + 1] != GameGridDto.EmptySpotValue)
            {
                return false;
            }
            if ((gridDto.Grid[row - 1][col] != domino.Side2 && gridDto.Grid[row - 1][col] != domino.Side1) &&
                (gridDto.Grid[row][col - 1] != domino.Side1 && gridDto.Grid[row][col - 1] != domino.Side2) &&
                (gridDto.Grid[row][col + 1] != domino.Side1 && gridDto.Grid[row][col + 1] != domino.Side2))
            {
                return false;
            }
            if (gridDto.Grid[row + 2][col] != GameGridDto.EmptySpotValue || gridDto.Grid[row + 3][col] != GameGridDto.EmptySpotValue) return false;
        }
        else
        {
            if (row - 3 <= 0) return false;
            if (gridDto.Grid[row][col] != GameGridDto.EmptySpotValue || gridDto.Grid[row - 1][col] != GameGridDto.EmptySpotValue) return false;
            if (gridDto.Grid[row - 1][col - 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row - 1][col + 1] != GameGridDto.EmptySpotValue ||
                gridDto.Grid[row - 2][col - 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row - 2][col + 1] != GameGridDto.EmptySpotValue)
            {
                return false;
            }
            if ((gridDto.Grid[row + 1][col] != domino.Side1 && gridDto.Grid[row + 1][col] != domino.Side2) &&
                (gridDto.Grid[row][col - 1] != domino.Side1 && gridDto.Grid[row][col - 1] != domino.Side2) &&
                (gridDto.Grid[row][col + 1] != domino.Side1 && gridDto.Grid[row][col + 1] != domino.Side2))
            {
                return false;
            }
            if (gridDto.Grid[row - 2][col] != GameGridDto.EmptySpotValue || gridDto.Grid[row - 3][col] != GameGridDto.EmptySpotValue) return false;
        }

        return true;
    }

    private bool CanPlaceDoubleHorizontal(DominoDto domino, int row, int col, bool previousDominoUp)
    {
        if (row <= 1 || col <= 0 || row >= gridDto.Row - 2 || col >= gridDto.Col - 1) return false;
        if (gridDto.Grid[row][col] != GameGridDto.EmptySpotValue ||
            gridDto.Grid[row][col - 1] != GameGridDto.EmptySpotValue ||
            gridDto.Grid[row][col + 1] != GameGridDto.EmptySpotValue)
        {
            return false;
        }

        if (previousDominoUp)
        {
            if (gridDto.Grid[row - 1][col] != domino.Side1) return false;
            if (gridDto.Grid[row + 1][col - 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row + 1][col + 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row + 1][col] != GameGridDto.EmptySpotValue ||
                gridDto.Grid[row + 2][col - 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row + 2][col + 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row + 2][col] != GameGridDto.EmptySpotValue)
            {
                return false;
            }
        }
        else
        {
            if (gridDto.Grid[row + 1][col] != domino.Side1) return false;
            if (gridDto.Grid[row - 1][col - 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row - 1][col + 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row - 1][col] != GameGridDto.EmptySpotValue ||
                gridDto.Grid[row - 2][col - 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row - 2][col + 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row - 2][col] != GameGridDto.EmptySpotValue)
            {
                return false;
            }
        }

        return true;
    }

    private bool CanPlaceDoubleVertical(DominoDto domino, int row, int col, bool previousDominoLeft)
    {
        if (row <= 0 || col <= 1 || row >= gridDto.Row - 1 || col >= gridDto.Col - 2) return false;
        if (gridDto.Grid[row][col] != GameGridDto.EmptySpotValue ||
            gridDto.Grid[row - 1][col] != GameGridDto.EmptySpotValue ||
            gridDto.Grid[row + 1][col] != GameGridDto.EmptySpotValue)
        {
            return false;
        }

        if (previousDominoLeft)
        {
            if (gridDto.Grid[row][col - 1] != domino.Side1) return false;
            if (gridDto.Grid[row - 1][col + 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row + 1][col + 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row][col + 1] != GameGridDto.EmptySpotValue ||
                gridDto.Grid[row - 1][col + 2] != GameGridDto.EmptySpotValue || gridDto.Grid[row + 1][col + 2] != GameGridDto.EmptySpotValue || gridDto.Grid[row][col + 2] != GameGridDto.EmptySpotValue)
            {
                return false;
            }
        }
        else
        {
            if (gridDto.Grid[row][col + 1] != domino.Side1) return false;
            if (gridDto.Grid[row - 1][col - 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row + 1][col - 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row][col - 1] != GameGridDto.EmptySpotValue ||
                gridDto.Grid[row - 1][col - 2] != GameGridDto.EmptySpotValue || gridDto.Grid[row + 1][col - 2] != GameGridDto.EmptySpotValue || gridDto.Grid[row][col - 2] != GameGridDto.EmptySpotValue)
            {
                return false;
            }
        }

        return true;
    }
}
