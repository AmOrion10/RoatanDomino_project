using System;
using System.Collections.Generic;
using System.Linq;

using DominoCommon.com.domino.common.Scripts.Enums;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;

public class RoatanMatchEngine
{
    public class MatchPlayer
    {
        public int Id { get; set; }
        public int TeamId { get; set; }
        public string Name { get; set; }
        public bool IsBot { get; set; }
        public BotDifficulty? Difficulty { get; set; }
        public int MatchScore { get; set; }
        public List<DominoDto> Hand { get; } = new List<DominoDto>();

        public int HandPipSum()
        {
            int sum = 0;
            foreach (DominoDto d in Hand)
            {
                sum += d.Side1 + d.Side2;
            }
            return sum;
        }
    }

    public class PlacedTile
    {
        public DominoDto Domino { get; set; }
        public int InnerPip { get; set; }
        public int OuterPip { get; set; }
        public BoardEnd EndPlaced { get; set; }
        public int PlayerId { get; set; }
        public bool IsDouble => Domino.Side1 == Domino.Side2;
    }

    public class LegalMove
    {
        public DominoDto Domino { get; set; }
        public BoardEnd End { get; set; }
        public int MatchedPip { get; set; }
        public int ExposedPip { get; set; }
    }

    public class RoundResult
    {
        public MatchPlayer Winner { get; set; }
        public int WinningTeamId { get; set; } = -1;
        public bool WasBlocked { get; set; }
        public bool IsDraw { get; set; }
        public bool BreakTriggered { get; set; }
        public int PointsAwarded { get; set; }
        public int NextPoserIndex { get; set; } = -1;
        public List<MatchPlayer> Standings { get; set; }
    }

    public RoatanGameMode Mode { get; }
    public TableStyle TableStyle { get; }
    public MatchFormat MatchFormat { get; }
    public TurnDirection TurnDirection { get; set; }
    public bool IsTeam2v2 { get; }
    public BotDifficulty Difficulty { get; }
    public List<MatchPlayer> Players { get; } = new List<MatchPlayer>();
    public MatchPlayer CurrentPlayer => Players[currentPlayerIndex];
    public int CurrentPlayerIndex => currentPlayerIndex;
    public int BoneyardCount => boneyard.Count;
    public int LockedPileCount => lockedPile.Count;
    public int RoundNumber { get; private set; }
    public int TargetMatchScore { get; } = 100;
    public int ConsecutivePasses => consecutivePasses;
    public bool IsBreakResetPending { get; private set; } = true;
    public int CurrentHandPoserIndex { get; private set; } = -1;
    public int LastHandPoserIndex { get; private set; } = -1;
    public bool LastHandWasDraw { get; private set; }

    public bool HasBoardStarted => leftChain.Count > 0 || centerTile != null;
    public int LeftEndPip { get; private set; } = -1;
    public int RightEndPip { get; private set; } = -1;

    public PlacedTile CenterTile => centerTile;
    public IReadOnlyList<PlacedTile> LeftChain => leftChain;
    public IReadOnlyList<PlacedTile> RightChain => rightChain;
    public IReadOnlyList<DominoDto> PlayedDominoes => playedDominoes;

    private readonly Random rng;
    private readonly List<DominoDto> boneyard = new List<DominoDto>();
    private readonly List<DominoDto> lockedPile = new List<DominoDto>();
    private readonly List<PlacedTile> leftChain = new List<PlacedTile>();
    private readonly List<PlacedTile> rightChain = new List<PlacedTile>();
    private readonly List<DominoDto> playedDominoes = new List<DominoDto>();

    private PlacedTile centerTile;
    private int currentPlayerIndex;
    private int consecutivePasses;
    private int lastRoundWinnerIndex = -1;

    private static readonly string[] IslandBotNames = { "Anthony", "Patrik", "Astor" };

    /// <summary>
    /// Backward-compatible constructor mapping RoatanGameMode to decoupled TableStyle + MatchFormat.
    /// </summary>
    public RoatanMatchEngine(RoatanGameMode mode, BotDifficulty difficulty, int botCount, int? seed = null)
        : this(
            mode == RoatanGameMode.BlockedCerrado ? TableStyle.BLOCK : TableStyle.DRAW,
            MatchFormat.CUT_THROAT,
            difficulty,
            botCount,
            seed,
            isTeam2v2: false,
            turnDirection: TurnDirection.CounterClockwise)
    {
    }

    public RoatanMatchEngine(
        TableStyle tableStyle,
        MatchFormat matchFormat,
        BotDifficulty difficulty,
        int botCount,
        int? seed = null,
        bool isTeam2v2 = false,
        TurnDirection turnDirection = TurnDirection.CounterClockwise)
    {
        TableStyle = tableStyle;
        MatchFormat = matchFormat;
        Mode = tableStyle == TableStyle.BLOCK ? RoatanGameMode.BlockedCerrado : RoatanGameMode.ClassicDraw;
        TurnDirection = turnDirection;
        Difficulty = difficulty;
        rng = seed.HasValue ? new Random(seed.Value) : new Random();

        int clampedBots = Math.Max(1, Math.Min(3, botCount));
        int totalPlayers = 1 + clampedBots;
        IsTeam2v2 = isTeam2v2 && totalPlayers == 4;

        Players.Add(new MatchPlayer
        {
            Id = 0,
            TeamId = IsTeam2v2 ? 0 : 0,
            Name = "You",
            IsBot = false
        });

        for (int i = 0; i < clampedBots; i++)
        {
            int seatId = i + 1;
            Players.Add(new MatchPlayer
            {
                Id = seatId,
                TeamId = IsTeam2v2 ? (seatId % 2) : seatId,
                Name = IslandBotNames[i],
                IsBot = true,
                Difficulty = difficulty
            });
        }
    }

    public static int GetTilesPerPlayer(int playerCount, TableStyle tableStyle)
    {
        if (playerCount == 2) return 7;
        if (playerCount == 3) return tableStyle == TableStyle.BLOCK ? 9 : 7;
        if (playerCount == 4) return tableStyle == TableStyle.BLOCK ? 7 : 6;
        return 7;
    }

    public void TriggerBreakReset()
    {
        IsBreakResetPending = true;
    }

    public PlacedTile StartNewRound()
    {
        RoundNumber++;
        centerTile = null;
        leftChain.Clear();
        rightChain.Clear();
        playedDominoes.Clear();
        lockedPile.Clear();
        LeftEndPip = -1;
        RightEndPip = -1;
        consecutivePasses = 0;

        bool excludeDoubleBlank = Players.Count == 3 && TableStyle == TableStyle.BLOCK;
        BuildAndShuffleDeck(excludeDoubleBlank);

        int tilesPerPlayer = GetTilesPerPlayer(Players.Count, TableStyle);
        foreach (MatchPlayer player in Players)
        {
            player.Hand.Clear();
            for (int i = 0; i < tilesPerPlayer; i++)
            {
                DominoDto tile = PopBoneyard();
                if (tile != null)
                {
                    player.Hand.Add(tile);
                }
            }
        }

        // In BLOCK mode (e.g. 1v1 with 14 remaining tiles), place undealt tiles into a locked/inactive pile.
        if (TableStyle == TableStyle.BLOCK && boneyard.Count > 0)
        {
            lockedPile.AddRange(boneyard);
            boneyard.Clear();
        }

        ResolveOpeningPose(out int starterIdx, out DominoDto starterTile);
        return ApplyOpeningPose(starterIdx, starterTile);
    }

    public void ResolveOpeningPose(out int starterIdx, out DominoDto starterTile)
    {
        if (!IsBreakResetPending && LastHandWasDraw && LastHandPoserIndex >= 0 && LastHandPoserIndex < Players.Count)
        {
            starterIdx = LastHandPoserIndex;
            starterTile = FindBestOpeningTile(Players[starterIdx]);
            return;
        }

        if (!IsBreakResetPending && !LastHandWasDraw && lastRoundWinnerIndex >= 0 && lastRoundWinnerIndex < Players.Count)
        {
            starterIdx = lastRoundWinnerIndex;
            starterTile = FindBestOpeningTile(Players[starterIdx]);
            return;
        }

        FindGlobalOpeningTile(out starterIdx, out starterTile);
    }

    private PlacedTile ApplyOpeningPose(int starterIdx, DominoDto starterTile)
    {
        MatchPlayer starter = Players[starterIdx];
        RemoveFromHand(starter, starterTile);

        centerTile = new PlacedTile
        {
            Domino = Clone(starterTile),
            InnerPip = starterTile.Side1,
            OuterPip = starterTile.Side2,
            EndPlaced = BoardEnd.Center,
            PlayerId = starter.Id
        };

        LeftEndPip = starterTile.Side1;
        RightEndPip = starterTile.Side2;
        playedDominoes.Add(Clone(starterTile));

        CurrentHandPoserIndex = starterIdx;
        LastHandPoserIndex = starterIdx;
        IsBreakResetPending = false;
        currentPlayerIndex = StepTurnIndex(starterIdx);
        return centerTile;
    }

    public List<LegalMove> GetLegalMoves(MatchPlayer player)
    {
        List<LegalMove> moves = new List<LegalMove>();
        if (!HasBoardStarted) return moves;

        bool endsEqual = LeftEndPip == RightEndPip;

        foreach (DominoDto tile in player.Hand)
        {
            bool matchesLeft = tile.Side1 == LeftEndPip || tile.Side2 == LeftEndPip;
            bool matchesRight = tile.Side1 == RightEndPip || tile.Side2 == RightEndPip;

            if (matchesLeft)
            {
                int exposed = tile.Side1 == LeftEndPip ? tile.Side2 : tile.Side1;
                moves.Add(new LegalMove
                {
                    Domino = Clone(tile),
                    End = BoardEnd.Left,
                    MatchedPip = LeftEndPip,
                    ExposedPip = exposed
                });
            }

            if (matchesRight && !(endsEqual && leftChain.Count == 0 && rightChain.Count == 0))
            {
                int exposed = tile.Side1 == RightEndPip ? tile.Side2 : tile.Side1;
                moves.Add(new LegalMove
                {
                    Domino = Clone(tile),
                    End = BoardEnd.Right,
                    MatchedPip = RightEndPip,
                    ExposedPip = exposed
                });
            }
        }

        return moves;
    }

    public PlacedTile PlayMove(LegalMove move)
    {
        if (move == null || move.Domino == null) return null;
        MatchPlayer player = CurrentPlayer;
        DominoDto inHand = FindInHand(player, move.Domino);
        if (inHand == null) return null;

        int targetPip = move.End == BoardEnd.Left ? LeftEndPip : RightEndPip;
        if (inHand.Side1 != targetPip && inHand.Side2 != targetPip)
        {
            return null;
        }

        int exposedPip = inHand.Side1 == targetPip ? inHand.Side2 : inHand.Side1;

        PlacedTile placed = new PlacedTile
        {
            Domino = Clone(inHand),
            InnerPip = targetPip,
            OuterPip = exposedPip,
            EndPlaced = move.End,
            PlayerId = player.Id
        };

        if (move.End == BoardEnd.Left)
        {
            leftChain.Add(placed);
            LeftEndPip = exposedPip;
        }
        else
        {
            rightChain.Add(placed);
            RightEndPip = exposedPip;
        }

        player.Hand.Remove(inHand);
        playedDominoes.Add(Clone(inHand));
        consecutivePasses = 0;

        currentPlayerIndex = StepTurnIndex(currentPlayerIndex);
        return placed;
    }

    public DominoDto DrawFromBoneyard()
    {
        if (!CanCurrentPlayerDraw())
        {
            return null;
        }

        DominoDto tile = PopBoneyard();
        if (tile != null)
        {
            CurrentPlayer.Hand.Add(tile);
        }
        return tile;
    }

    public bool PassTurn()
    {
        if (!CanCurrentPlayerPass())
        {
            return false;
        }

        consecutivePasses++;
        currentPlayerIndex = StepTurnIndex(currentPlayerIndex);
        return true;
    }

    public bool CanCurrentPlayerDraw()
    {
        return TableStyle == TableStyle.DRAW &&
               boneyard.Count > 0 &&
               GetLegalMoves(CurrentPlayer).Count == 0;
    }

    public bool CanCurrentPlayerPass()
    {
        return GetLegalMoves(CurrentPlayer).Count == 0 && !CanCurrentPlayerDraw();
    }

    public MatchPlayer PeekNextPlayer()
    {
        return Players[StepTurnIndex(currentPlayerIndex)];
    }

    private int StepTurnIndex(int fromIndex)
    {
        if (TurnDirection == TurnDirection.Clockwise)
        {
            return (fromIndex + 1) % Players.Count;
        }

        return (fromIndex - 1 + Players.Count) % Players.Count;
    }

    public bool IsRoundOver(out bool wasBlocked)
    {
        if (Players.Any(p => p.Hand.Count == 0))
        {
            wasBlocked = false;
            return true;
        }

        if (consecutivePasses >= Players.Count && boneyard.Count == 0)
        {
            wasBlocked = true;
            return true;
        }

        wasBlocked = false;
        return false;
    }

    public RoundResult CompleteRound()
    {
        bool wasBlocked;
        IsRoundOver(out wasBlocked);

        List<MatchPlayer> standings = Players
            .OrderBy(p => p.HandPipSum())
            .ThenBy(p => p.Hand.Count)
            .ToList();

        // Domino Out: player emptied hand
        MatchPlayer dominoOutWinner = Players.FirstOrDefault(p => p.Hand.Count == 0);
        if (dominoOutWinner != null)
        {
            int winningTeam = dominoOutWinner.TeamId;
            int pointsWon = SumOpposingPips(winningTeam);
            AwardPoints(dominoOutWinner, winningTeam, pointsWon);

            LastHandWasDraw = false;
            lastRoundWinnerIndex = Players.FindIndex(p => p.Id == dominoOutWinner.Id);

            return new RoundResult
            {
                Winner = dominoOutWinner,
                WinningTeamId = winningTeam,
                WasBlocked = false,
                IsDraw = false,
                BreakTriggered = false,
                PointsAwarded = pointsWon,
                NextPoserIndex = lastRoundWinnerIndex,
                Standings = standings
            };
        }

        // Blocked Board (Lockdown): compare individual player hand pip sums (never combine partners' pips)
        int lowestIndividualPips = Players.Min(p => p.HandPipSum());
        List<MatchPlayer> lowestPlayers = Players
            .Where(p => p.HandPipSum() == lowestIndividualPips)
            .ToList();

        List<int> distinctLowestTeams = lowestPlayers
            .Select(p => p.TeamId)
            .Distinct()
            .ToList();

        // Opposing tie for lowest individual pip total -> Draw (0 pts, no Break, same Poser next hand)
        if (distinctLowestTeams.Count > 1)
        {
            LastHandWasDraw = true;
            return new RoundResult
            {
                Winner = null,
                WinningTeamId = -1,
                WasBlocked = true,
                IsDraw = true,
                BreakTriggered = false,
                PointsAwarded = 0,
                NextPoserIndex = CurrentHandPoserIndex,
                Standings = standings
            };
        }

        MatchPlayer blockWinner = lowestPlayers[0];
        int blockWinningTeam = distinctLowestTeams[0];
        int blockPoints = SumOpposingPips(blockWinningTeam);
        AwardPoints(blockWinner, blockWinningTeam, blockPoints);

        LastHandWasDraw = false;
        lastRoundWinnerIndex = Players.FindIndex(p => p.Id == blockWinner.Id);

        return new RoundResult
        {
            Winner = blockWinner,
            WinningTeamId = blockWinningTeam,
            WasBlocked = true,
            IsDraw = false,
            BreakTriggered = false,
            PointsAwarded = blockPoints,
            NextPoserIndex = lastRoundWinnerIndex,
            Standings = standings
        };
    }

    private int SumOpposingPips(int winningTeamId)
    {
        int sum = 0;
        foreach (MatchPlayer p in Players)
        {
            if (p.TeamId != winningTeamId)
            {
                sum += p.HandPipSum();
            }
        }
        return sum;
    }

    private void AwardPoints(MatchPlayer winner, int winningTeamId, int points)
    {
        if (IsTeam2v2)
        {
            foreach (MatchPlayer p in Players)
            {
                if (p.TeamId == winningTeamId)
                {
                    p.MatchScore += points;
                }
            }
        }
        else
        {
            winner.MatchScore += points;
        }
    }

    public bool HasMatchChampion(out MatchPlayer champion)
    {
        champion = Players.OrderByDescending(p => p.MatchScore).FirstOrDefault(p => p.MatchScore >= TargetMatchScore);
        return champion != null;
    }

    private void BuildAndShuffleDeck(bool excludeDoubleBlank)
    {
        boneyard.Clear();
        for (int i = 0; i <= 6; i++)
        {
            for (int j = i; j <= 6; j++)
            {
                if (excludeDoubleBlank && i == 0 && j == 0)
                {
                    continue;
                }
                boneyard.Add(new DominoDto { Side1 = i, Side2 = j });
            }
        }

        for (int i = boneyard.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            DominoDto temp = boneyard[i];
            boneyard[i] = boneyard[j];
            boneyard[j] = temp;
        }
    }

    private DominoDto PopBoneyard()
    {
        if (boneyard.Count == 0) return null;
        int last = boneyard.Count - 1;
        DominoDto tile = boneyard[last];
        boneyard.RemoveAt(last);
        return tile;
    }

    private void FindGlobalOpeningTile(out int bestPlayerIdx, out DominoDto bestTile)
    {
        bestPlayerIdx = 0;
        bestTile = Players[0].Hand[0];
        int bestDouble = -1;

        // First priority: [6|6] down to [0|0]
        for (int i = 0; i < Players.Count; i++)
        {
            foreach (DominoDto d in Players[i].Hand)
            {
                if (d.Side1 == d.Side2 && d.Side1 > bestDouble)
                {
                    bestDouble = d.Side1;
                    bestPlayerIdx = i;
                    bestTile = d;
                }
            }
        }

        if (bestDouble >= 0) return;

        // Fallback if no player holds a double: highest total pip sum (tie-breaker: highest single end)
        int bestSum = -1;
        int bestMaxEnd = -1;
        for (int i = 0; i < Players.Count; i++)
        {
            foreach (DominoDto d in Players[i].Hand)
            {
                int sum = d.Side1 + d.Side2;
                int maxEnd = Math.Max(d.Side1, d.Side2);
                if (sum > bestSum || (sum == bestSum && maxEnd > bestMaxEnd))
                {
                    bestSum = sum;
                    bestMaxEnd = maxEnd;
                    bestPlayerIdx = i;
                    bestTile = d;
                }
            }
        }
    }

    private static DominoDto FindBestOpeningTile(MatchPlayer player)
    {
        DominoDto bestDouble = player.Hand
            .Where(d => d.Side1 == d.Side2)
            .OrderByDescending(d => d.Side1)
            .FirstOrDefault();

        if (bestDouble != null) return bestDouble;

        return player.Hand
            .OrderByDescending(d => d.Side1 + d.Side2)
            .ThenByDescending(d => Math.Max(d.Side1, d.Side2))
            .First();
    }

    private static DominoDto FindInHand(MatchPlayer player, DominoDto target)
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

    private static void RemoveFromHand(MatchPlayer player, DominoDto target)
    {
        DominoDto found = FindInHand(player, target);
        if (found != null)
        {
            player.Hand.Remove(found);
        }
    }

    private static DominoDto Clone(DominoDto d)
    {
        return new DominoDto { Side1 = d.Side1, Side2 = d.Side2 };
    }
}
