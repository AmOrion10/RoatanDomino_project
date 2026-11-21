using DominoCommon.com.domino.common.Scripts.Enums;
using DominoCommon.com.domino.common.Scripts.Networking;

namespace Domain.Game;

public class Game
{
    private const int NumberOfRows = 60;
    private const int NumberOfCols = 60;

    private const double PercentageOfLevelBettedRanked = 0.01;
    private const int FlatBet = 10;

    public int Id { get; }
    public List<GamePlayer> Players { get; }
    public GamePlayer PlayerPlaying => Players[currentPlayerIndex];
    public Timer StartTimer { get; set; } = new();
    public Timer GeneralTimer { get; set; } = new();
    public Timer PlayerTurnTimer { get; set; } = new();
    public DominoStack Stack { get; }
    public GameMove LastGameMove => placer.MoveHistory.LastMove();
    public MoveRecorder MoveHistory => placer.MoveHistory;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public GameMode Mode { get; set; }
    public TableStyle TableStyle { get; }
    public MatchFormat MatchFormat { get; }
    public TurnDirection TurnDirection { get; set; }
    public bool IsTeam2v2 { get; }
    public bool IsLegacyDrawAdvance { get; }

    public int HandNumber { get; private set; }
    public int ConsecutivePasses { get; private set; }
    public bool IsBreakResetPending { get; private set; } = true;
    public long? CurrentHandPoserId { get; private set; }
    public long? LastHandPoserId { get; private set; }
    public bool LastHandWasDraw { get; private set; }
    public long? LastHandWinnerId { get; private set; }
    public HandResolution? LastHandResolution { get; private set; }

    public int TotalBet { get; set; }
    public int ChatId { get; set; }

    private readonly GameGridDto gridDto = new(NumberOfRows, NumberOfCols);
    private readonly DominoPlacer placer;
    private int currentPlayerIndex;
    public List<GamePlayer> Classement { get; } = [];

    /// <summary>
    /// Backward-compatible constructor for existing production lobbies and tests.
    /// Defaults to DRAW table style, CUT_THROAT match format, and counter-clockwise turns.
    /// </summary>
    public Game(int id, List<GamePlayer> players, GameMode mode, int? seed = null)
        : this(id, players, mode, TableStyle.DRAW, MatchFormat.CUT_THROAT, seed, isTeam2v2: false, turnDirection: TurnDirection.CounterClockwise, isLegacyDrawAdvance: true)
    {
    }

    public Game(
        int id,
        List<GamePlayer> players,
        GameMode mode,
        TableStyle tableStyle,
        MatchFormat matchFormat = MatchFormat.CUT_THROAT,
        int? seed = null,
        bool isTeam2v2 = false,
        TurnDirection turnDirection = TurnDirection.CounterClockwise,
        bool isLegacyDrawAdvance = false)
    {
        Stack = new DominoStack(seed);
        Id = id;
        Players = players;
        placer = new DominoPlacer(gridDto);
        Mode = mode;
        TableStyle = tableStyle;
        MatchFormat = matchFormat;
        TurnDirection = turnDirection;
        IsTeam2v2 = isTeam2v2 && players.Count == 4;
        IsLegacyDrawAdvance = isLegacyDrawAdvance;

        AssignDefaultTeams();
    }

    private void AssignDefaultTeams()
    {
        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].TeamId < 0)
            {
                Players[i].TeamId = IsTeam2v2 ? (i % 2) : i;
            }
        }
    }

    public static int GetTilesPerPlayer(int playerCount, TableStyle tableStyle)
    {
        return (playerCount, tableStyle) switch
        {
            (2, TableStyle.BLOCK) => 7,
            (2, TableStyle.DRAW) => 7,
            (3, TableStyle.BLOCK) => 9,
            (3, TableStyle.DRAW) => 7,
            (4, TableStyle.BLOCK) => 7,
            (4, TableStyle.DRAW) => 6,
            _ => 7
        };
    }

    public void TriggerBreakReset()
    {
        IsBreakResetPending = true;
    }

    public void DistributeDominoes()
    {
        StartTime = DateTime.Now;
        HandNumber++;
        ConsecutivePasses = 0;
        LastHandResolution = null;

        // 3-player BLOCK removes [0|0] -> 27 tiles total (9 dealt to each player, 0 boneyard)
        if (Players.Count == 3 && TableStyle == TableStyle.BLOCK)
        {
            Stack.Remove(0, 0);
        }

        int tilesPerPlayer = GetTilesPerPlayer(Players.Count, TableStyle);

        foreach (GamePlayer player in Players)
        {
            player.Hand.Clear();
            for (int i = 0; i < tilesPerPlayer; i++)
            {
                Domino? domino = Stack.Draw();
                if (domino is null) break;
                player.Hand.Add(domino);
            }
        }

        // 2-player BLOCK: remaining 14 tiles are placed face-down in a locked/inactive pile (no drawing allowed)
        if (TableStyle == TableStyle.BLOCK && Stack.TotalRemainingTiles() > 0)
        {
            Stack.LockRemainingPile();
        }
    }

    public (GamePlayer Poser, Domino OpeningDomino) ResolveOpeningPoser()
    {
        if (!IsBreakResetPending && LastHandWasDraw && LastHandPoserId.HasValue)
        {
            GamePlayer? samePoser = Players.FirstOrDefault(p => p.Id == LastHandPoserId.Value);
            if (samePoser is not null && samePoser.Hand.Count > 0)
            {
                return (samePoser, FindBestOpeningTileInHand(samePoser));
            }
        }

        if (!IsBreakResetPending && !LastHandWasDraw && LastHandWinnerId.HasValue)
        {
            GamePlayer? priorWinner = Players.FirstOrDefault(p => p.Id == LastHandWinnerId.Value);
            if (priorWinner is not null && priorWinner.Hand.Count > 0)
            {
                return (priorWinner, FindBestOpeningTileInHand(priorWinner));
            }
        }

        // Hand 1 or Break reset: [6|6] holder -> highest double [5|5]..[0|0] -> highest pip sum (tie-breaker: highest single end)
        (GamePlayer? playerWithDouble, Domino? highestDouble) = FindPlayerWithHighestDouble();
        if (playerWithDouble is not null && highestDouble is not null)
        {
            return (playerWithDouble, highestDouble);
        }

        (GamePlayer? playerWithHighTile, Domino? highestTile) = FindPlayerWithHighestPipTile();
        if (playerWithHighTile is not null && highestTile is not null)
        {
            return (playerWithHighTile, highestTile);
        }

        throw new InvalidOperationException("Cannot resolve opening poser when all hands are empty.");
    }

    public void PlaceFirstDomino()
    {
        (GamePlayer poser, Domino openingDomino) = ResolveOpeningPoser();

        Orientation orientation = openingDomino.Side1 == openingDomino.Side2
            ? Orientation.PerpendicularVerticalFromLeft
            : Orientation.HorizontalLeft;

        GameMove move = new(openingDomino, NumberOfRows / 2, NumberOfCols / 2, orientation, poser.Id);
        placer.PlaceFirstDomino(poser, move);

        currentPlayerIndex = Players.FindIndex(p => p.Id == poser.Id);
        CurrentHandPoserId = poser.Id;
        LastHandPoserId = poser.Id;
        IsBreakResetPending = false;
        ConsecutivePasses = 0;
    }

    public bool PlaceDomino(GameMove move)
    {
        if (move.PlayerId == PlayerPlaying.Id)
        {
            bool placed = placer.PlaceDomino(PlayerPlaying, new GameMove(move.Domino, move.Row, move.Col, move.Orientation, move.PlayerId));
            if (placed)
            {
                ConsecutivePasses = 0;
            }
            return placed;
        }
        return false;
    }

    public bool CanDraw(long idUser)
    {
        if (TableStyle != TableStyle.DRAW) return false;
        if (idUser != PlayerPlaying.Id) return false;
        if (Stack.IsEmpty()) return false;
        return placer.GetPlayerPossibleMoves(PlayerPlaying).Count == 0;
    }

    public bool CanPass(long idUser)
    {
        if (idUser != PlayerPlaying.Id) return false;
        if (placer.GetPlayerPossibleMoves(PlayerPlaying).Count > 0) return false;

        if (TableStyle == TableStyle.DRAW && !Stack.IsEmpty())
        {
            return false;
        }

        return true;
    }

    public Domino? Draw(long idUser)
    {
        if (TableStyle == TableStyle.BLOCK || Stack.IsLocked)
        {
            return null;
        }

        if (idUser == PlayerPlaying.Id)
        {
            Domino? domino = Stack.Draw();
            if (domino is not null)
            {
                PlayerPlaying.AddDominoToHand(domino);
                return domino;
            }
        }
        return null;
    }

    public bool PassTurn(long idUser)
    {
        if (!CanPass(idUser))
        {
            return false;
        }

        ConsecutivePasses++;
        NextTurn();
        return true;
    }

    private (GamePlayer? player, Domino? domino) FindPlayerWithHighestDouble()
    {
        GamePlayer? bestPlayer = null;
        Domino? bestDomino = null;
        int bestDoubleValue = -1;

        foreach (GamePlayer player in Players)
        {
            foreach (Domino domino in player.Hand)
            {
                if (domino.Side1 != domino.Side2) continue;

                if (domino.Side1 > bestDoubleValue)
                {
                    bestDoubleValue = domino.Side1;
                    bestDomino = domino;
                    bestPlayer = player;
                }
            }
        }

        return (bestPlayer, bestDomino);
    }

    private (GamePlayer? player, Domino? domino) FindPlayerWithHighestPipTile()
    {
        GamePlayer? bestPlayer = null;
        Domino? bestDomino = null;
        int bestSum = -1;
        int bestMaxEnd = -1;

        foreach (GamePlayer player in Players)
        {
            foreach (Domino domino in player.Hand)
            {
                int sum = domino.SumOfSides();
                int maxEnd = Math.Max(domino.Side1, domino.Side2);

                if (sum > bestSum || (sum == bestSum && maxEnd > bestMaxEnd))
                {
                    bestSum = sum;
                    bestMaxEnd = maxEnd;
                    bestDomino = domino;
                    bestPlayer = player;
                }
            }
        }

        return (bestPlayer, bestDomino);
    }

    private static Domino FindBestOpeningTileInHand(GamePlayer player)
    {
        Domino? bestDouble = player.Hand
            .Where(d => d.Side1 == d.Side2)
            .OrderByDescending(d => d.Side1)
            .FirstOrDefault();

        if (bestDouble is not null) return bestDouble;

        return player.Hand
            .OrderByDescending(d => d.SumOfSides())
            .ThenByDescending(d => Math.Max(d.Side1, d.Side2))
            .First();
    }

    public List<GameMove> GetPlayerPossibleMoves(GamePlayer player)
    {
        return placer.GetPlayerPossibleMoves(player);
    }

    public Dictionary<long, int> GetNumberOfDominoAllPlayer()
    {
        Dictionary<long, int> numberOfDomino = [];
        foreach (GamePlayer player in Players)
        {
            numberOfDomino.Add(player.Id, player.Hand.Count);
        }
        return numberOfDomino;
    }

    public bool CheckEndGame()
    {
        HandResolution resolution = ResolveHandEnd();
        UpdateClassement();
        if (resolution.IsOver)
        {
            LastHandResolution = resolution;
            LastHandWasDraw = resolution.IsDraw;
            if (!resolution.IsDraw && resolution.Winner is not null)
            {
                LastHandWinnerId = resolution.Winner.Id;
            }
            EndTime = DateTime.Now;
            return true;
        }
        return false;
    }

    public HandResolution ResolveHandEnd()
    {
        Dictionary<long, int> individualPips = Players.ToDictionary(p => p.Id, p => p.HandPipSum());

        GamePlayer? dominoOutWinner = Players.FirstOrDefault(CheckWin);
        if (dominoOutWinner is not null)
        {
            int winnerTeam = GetEffectiveTeamId(dominoOutWinner);
            int points = Players
                .Where(p => GetEffectiveTeamId(p) != winnerTeam)
                .Sum(p => p.HandPipSum());

            return new HandResolution
            {
                IsOver = true,
                WasBlocked = false,
                IsDraw = false,
                Winner = dominoOutWinner,
                WinningTeamId = winnerTeam,
                PointsAwarded = points,
                BreakTriggered = false,
                NextPoserId = dominoOutWinner.Id,
                IndividualPipCounts = individualPips
            };
        }

        if (GameBlocked())
        {
            int lowestIndividualPips = individualPips.Values.Min();
            List<GamePlayer> lowestPlayers = Players
                .Where(p => individualPips[p.Id] == lowestIndividualPips)
                .ToList();

            List<int> distinctLowestTeams = lowestPlayers
                .Select(GetEffectiveTeamId)
                .Distinct()
                .ToList();

            // Tie rule: if two opposing players tie for lowest individual pip total -> Draw
            if (distinctLowestTeams.Count > 1)
            {
                return new HandResolution
                {
                    IsOver = true,
                    WasBlocked = true,
                    IsDraw = true,
                    Winner = null,
                    WinningTeamId = -1,
                    PointsAwarded = 0,
                    BreakTriggered = false,
                    NextPoserId = CurrentHandPoserId ?? LastHandPoserId,
                    IndividualPipCounts = individualPips
                };
            }

            GamePlayer blockWinner = lowestPlayers[0];
            int winningTeam = distinctLowestTeams[0];
            int points = Players
                .Where(p => GetEffectiveTeamId(p) != winningTeam)
                .Sum(p => p.HandPipSum());

            return new HandResolution
            {
                IsOver = true,
                WasBlocked = true,
                IsDraw = false,
                Winner = blockWinner,
                WinningTeamId = winningTeam,
                PointsAwarded = points,
                BreakTriggered = false,
                NextPoserId = blockWinner.Id,
                IndividualPipCounts = individualPips
            };
        }

        return new HandResolution
        {
            IsOver = false,
            IndividualPipCounts = individualPips
        };
    }

    private int GetEffectiveTeamId(GamePlayer player)
    {
        if (player.TeamId >= 0) return player.TeamId;
        int idx = Players.IndexOf(player);
        return IsTeam2v2 ? (idx % 2) : idx;
    }

    private static bool CheckWin(GamePlayer player)
    {
        return player.Hand.Count == 0;
    }

    private bool GameBlocked()
    {
        if (!Stack.IsEmpty())
        {
            return false;
        }

        if (ConsecutivePasses >= Players.Count)
        {
            return true;
        }

        foreach (GamePlayer player in Players)
        {
            if (placer.GetPlayerPossibleMoves(player).Count != 0)
            {
                return false;
            }
        }
        return true;
    }

    public void NextTurn()
    {
        if (Players.Count == 0) return;

        if (TurnDirection == TurnDirection.Clockwise)
        {
            currentPlayerIndex = (currentPlayerIndex + 1) % Players.Count;
        }
        else
        {
            currentPlayerIndex = (currentPlayerIndex - 1 + Players.Count) % Players.Count;
        }
    }

    public GamePlayer PeekNextPlayer()
    {
        int nextIdx = TurnDirection == TurnDirection.Clockwise
            ? (currentPlayerIndex + 1) % Players.Count
            : (currentPlayerIndex - 1 + Players.Count) % Players.Count;
        return Players[nextIdx];
    }

    public void UpdateClassement()
    {
        Classement.Clear();

        IOrderedEnumerable<GamePlayer> orderedPlayers = Players
            .OrderBy(player => player.HandPipSum())
            .ThenBy(player => player.Hand.Count);

        foreach (GamePlayer player in orderedPlayers)
        {
            Classement.Add(player);
        }
    }

    public void CalculateTotalBet()
    {
        foreach (GamePlayer player in Players)
        {
            int playerBet = FlatBet;
            if (player.Score - (int)(player.Score * PercentageOfLevelBettedRanked) + FlatBet >= 100)
            {
                playerBet = (int)(player.Score * PercentageOfLevelBettedRanked) + FlatBet;
            }
            TotalBet += playerBet;
            player.Bet = playerBet;
        }
    }

    public void DistributeScore()
    {
        int sum = 0;
        for (int i = 1; i < Players.Count + 1; i++)
        {
            sum += i;
        }

        foreach (GamePlayer player in Players)
        {
            if (Classement.IndexOf(player) <= Players.Count)
            {
                double share = Players.Count - Classement.IndexOf(player);
                player.Gain = (int)(share / sum * TotalBet);
            }
        }
    }

    public bool PlayerLeave(GamePlayer player)
    {
        bool res = false;
        GamePlayer playerPlaying = Players[currentPlayerIndex];
        Players.Remove(player);
        if (playerPlaying.Id == player.Id)
        {
            NextTurn();
            res = true;
        }
        currentPlayerIndex = Players.IndexOf(playerPlaying);
        foreach (Domino domino in player.Hand)
        {
            Stack.Add(domino);
        }
        return res;
    }

    // Need for test only
    public void DisplayGrid() // Temp: print in the console
    {
        Console.WriteLine("Current State of the Grid:");
        for (int i = 0; i < gridDto.Grid.Count; i++)
        {
            for (int j = 0; j < gridDto.Grid[i].Count; j++)
            {
                if (gridDto.Grid[i][j] != GameGridDto.EmptySpotValue)
                    Console.Write($"{gridDto.Grid[i][j]} |"); // Temp: print in the console
                else
                    Console.Write("  |"); // Temp: print in the console
            }
            Console.WriteLine();
        }
    }
}
