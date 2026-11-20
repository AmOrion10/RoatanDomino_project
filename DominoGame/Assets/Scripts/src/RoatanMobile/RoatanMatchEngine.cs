using System;
using System.Collections.Generic;
using System.Linq;

using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;

public class RoatanMatchEngine
{
    public class MatchPlayer
    {
        public int Id { get; set; }
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
        public bool WasBlocked { get; set; }
        public int PointsAwarded { get; set; }
        public List<MatchPlayer> Standings { get; set; }
    }

    public RoatanGameMode Mode { get; }
    public BotDifficulty Difficulty { get; }
    public List<MatchPlayer> Players { get; } = new List<MatchPlayer>();
    public MatchPlayer CurrentPlayer => Players[currentPlayerIndex];
    public int CurrentPlayerIndex => currentPlayerIndex;
    public int BoneyardCount => boneyard.Count;
    public int RoundNumber { get; private set; }
    public int TargetMatchScore { get; } = 100;

    public bool HasBoardStarted => leftChain.Count > 0 || centerTile != null;
    public int LeftEndPip { get; private set; } = -1;
    public int RightEndPip { get; private set; } = -1;

    public PlacedTile CenterTile => centerTile;
    public IReadOnlyList<PlacedTile> LeftChain => leftChain;
    public IReadOnlyList<PlacedTile> RightChain => rightChain;
    public IReadOnlyList<DominoDto> PlayedDominoes => playedDominoes;

    private readonly Random rng;
    private readonly List<DominoDto> boneyard = new List<DominoDto>();
    private readonly List<PlacedTile> leftChain = new List<PlacedTile>();
    private readonly List<PlacedTile> rightChain = new List<PlacedTile>();
    private readonly List<DominoDto> playedDominoes = new List<DominoDto>();

    private PlacedTile centerTile;
    private int currentPlayerIndex;
    private int consecutivePasses;
    private int lastRoundWinnerIndex = -1;

    private static readonly string[] IslandBotNames = { "Mateo", "Sofia", "Carlos" };

    public RoatanMatchEngine(RoatanGameMode mode, BotDifficulty difficulty, int botCount, int? seed = null)
    {
        Mode = mode;
        Difficulty = difficulty;
        rng = seed.HasValue ? new Random(seed.Value) : new Random();

        int clampedBots = mode == RoatanGameMode.BlockedCerrado ? 3 : Math.Max(1, Math.Min(3, botCount));

        Players.Add(new MatchPlayer
        {
            Id = 0,
            Name = "You",
            IsBot = false
        });

        for (int i = 0; i < clampedBots; i++)
        {
            Players.Add(new MatchPlayer
            {
                Id = i + 1,
                Name = IslandBotNames[i],
                IsBot = true,
                Difficulty = difficulty
            });
        }
    }

    public PlacedTile StartNewRound()
    {
        RoundNumber++;
        centerTile = null;
        leftChain.Clear();
        rightChain.Clear();
        playedDominoes.Clear();
        LeftEndPip = -1;
        RightEndPip = -1;
        consecutivePasses = 0;

        BuildAndShuffleBoneyard();

        int tilesPerPlayer = (Mode == RoatanGameMode.ClassicDraw && Players.Count == 4) ? 6 : 7;
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

        // First round (or if no prior winner): highest double starts (fallback: highest pip sum).
        // Subsequent rounds: previous round winner leads with their best double or highest tile.
        int starterIdx;
        DominoDto starterTile;
        if (lastRoundWinnerIndex >= 0 && lastRoundWinnerIndex < Players.Count)
        {
            starterIdx = lastRoundWinnerIndex;
            starterTile = FindBestOpeningTile(Players[starterIdx]);
        }
        else
        {
            FindGlobalOpeningTile(out starterIdx, out starterTile);
        }

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

        currentPlayerIndex = (starterIdx + 1) % Players.Count;
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

            // If both ends have the same pip value AND it's the very first tile (just center),
            // still allow choosing either side unless user prefers simplicity — allowing both sides feels natural.
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

        currentPlayerIndex = (currentPlayerIndex + 1) % Players.Count;
        return placed;
    }

    public DominoDto DrawFromBoneyard()
    {
        if (Mode == RoatanGameMode.BlockedCerrado || boneyard.Count == 0)
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

    public void PassTurn()
    {
        consecutivePasses++;
        currentPlayerIndex = (currentPlayerIndex + 1) % Players.Count;
    }

    public bool CanCurrentPlayerDraw()
    {
        return Mode == RoatanGameMode.ClassicDraw && boneyard.Count > 0;
    }

    public MatchPlayer PeekNextPlayer()
    {
        return Players[(currentPlayerIndex + 1) % Players.Count];
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

        if (boneyard.Count == 0 && Players.All(p => GetLegalMoves(p).Count == 0))
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

        MatchPlayer winner = standings[0];
        int pointsWon = 0;
        foreach (MatchPlayer p in Players)
        {
            if (p.Id != winner.Id)
            {
                pointsWon += p.HandPipSum();
            }
        }

        winner.MatchScore += pointsWon;
        lastRoundWinnerIndex = Players.FindIndex(p => p.Id == winner.Id);

        return new RoundResult
        {
            Winner = winner,
            WasBlocked = wasBlocked,
            PointsAwarded = pointsWon,
            Standings = standings
        };
    }

    public bool HasMatchChampion(out MatchPlayer champion)
    {
        champion = Players.OrderByDescending(p => p.MatchScore).FirstOrDefault(p => p.MatchScore >= TargetMatchScore);
        return champion != null;
    }

    private void BuildAndShuffleBoneyard()
    {
        boneyard.Clear();
        for (int i = 0; i <= 6; i++)
        {
            for (int j = i; j <= 6; j++)
            {
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

        int bestSum = -1;
        for (int i = 0; i < Players.Count; i++)
        {
            foreach (DominoDto d in Players[i].Hand)
            {
                int sum = d.Side1 + d.Side2;
                if (sum > bestSum)
                {
                    bestSum = sum;
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
