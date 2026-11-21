using DominoCommon.com.domino.common.Scripts.Enums;
using DominoCommon.com.domino.common.Scripts.Networking;

namespace Domain.Game;

public class Game
{
    private const int NumberOfRows = 60;
    private const int NumberOfCols = 60;

    private const double PercentageOfLevelBettedRanked = 0.01;
    private const int FlatBet = 10;

    public const double ChoosePoserTimeoutSeconds = 8.0;
    public const double AutoPassTimeoutSeconds = 5.0;
    public const double TurnActionTimeoutSeconds = 10.0;
    public const int BotTakeoverStrikeLimit = 3;
    public const double BotTakeoverDelaySeconds = 1.2;

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
    public bool OneOnePlayoff { get; }
    public TurnDirection TurnDirection { get; set; }
    public bool IsTeam2v2 { get; }
    public bool IsLegacyDrawAdvance { get; }
    public int TargetHandWins => MatchFormat == MatchFormat.TWO_BEST ? 2 : 6;

    public MatchPhase Phase { get; private set; } = MatchPhase.PLAYING;
    public int HandNumber { get; private set; }
    public int ConsecutivePasses { get; private set; }
    public bool IsBreakResetPending { get; private set; } = true;
    public long? CurrentHandPoserId { get; private set; }
    public long? LastHandPoserId { get; private set; }
    public bool LastHandWasDraw { get; private set; }
    public long? LastHandWinnerId { get; private set; }
    public HandResolution? LastHandResolution { get; private set; }

    public Dictionary<int, int> TeamScores { get; } = [];
    public Dictionary<long, int> PlayerScores { get; } = [];
    public bool IsMatchOver { get; private set; }
    public GamePlayer? MatchWinner { get; private set; }
    public int MatchWinningTeamId { get; private set; } = -1;
    public List<long> CutThroatedPlayerIds { get; } = [];

    // "Call Your Padna" (CHOOSE_POSER) state
    public long? PoserChooserId { get; private set; }
    public long? PartnerCandidateId { get; private set; }
    public long? ConfirmedPoserId { get; private set; }
    public double ChoosePoserRemainingSeconds { get; private set; }
    public bool CanPoserPlayAnyTile { get; private set; }
    public bool PartnerHandsHiddenDuringChoosePoser => Phase == MatchPhase.CHOOSE_POSER;

    public int TotalBet { get; set; }
    public int ChatId { get; set; }

    private GameGridDto gridDto = new(NumberOfRows, NumberOfCols);
    private DominoPlacer placer;
    private int currentPlayerIndex;
    private bool currentHandScored;
    public List<GamePlayer> Classement { get; } = [];

    /// <summary>
    /// Backward-compatible constructor for existing production lobbies and tests.
    /// Defaults to DRAW table style, CUT_THROAT match format, and counter-clockwise turns.
    /// </summary>
    public Game(int id, List<GamePlayer> players, GameMode mode, int? seed = null)
        : this(id, players, mode, TableStyle.DRAW, MatchFormat.CUT_THROAT, seed, isTeam2v2: false, turnDirection: TurnDirection.CounterClockwise, isLegacyDrawAdvance: true, oneOnePlayoff: false)
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
        bool isLegacyDrawAdvance = false,
        bool oneOnePlayoff = false)
    {
        Stack = new DominoStack(seed);
        Id = id;
        Players = players;
        placer = new DominoPlacer(gridDto);
        Mode = mode;
        TableStyle = tableStyle;
        MatchFormat = matchFormat;
        OneOnePlayoff = oneOnePlayoff;
        TurnDirection = turnDirection;
        IsLegacyDrawAdvance = isLegacyDrawAdvance;

        IsTeam2v2 = matchFormat switch
        {
            MatchFormat.SIX_LOV => players.Count == 4,
            MatchFormat.CUT_THROAT => false,
            MatchFormat.TWO_BEST => players.Count == 4 || (isTeam2v2 && players.Count == 4),
            _ => isTeam2v2 && players.Count == 4
        };

        AssignDefaultTeamsAndScores();
    }

    private void AssignDefaultTeamsAndScores()
    {
        TeamScores.Clear();
        PlayerScores.Clear();

        if (IsTeam2v2)
        {
            TeamScores[0] = 0;
            TeamScores[1] = 0;
        }

        for (int i = 0; i < Players.Count; i++)
        {
            Players[i].TeamId = IsTeam2v2 ? (i % 2) : i;
            Players[i].MatchScore = 0;
            PlayerScores[Players[i].Id] = 0;
        }
    }

    public GamePlayer? GetPartner(GamePlayer player)
    {
        if (!IsTeam2v2 || Players.Count != 4) return null;
        int idx = Players.IndexOf(player);
        if (idx < 0) return null;
        return Players[(idx + 2) % 4];
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

    public static List<PostHandAction> GetPostHandActions(bool breakTriggered, bool matchOver)
    {
        if (breakTriggered || matchOver)
        {
            return [PostHandAction.Rematch, PostHandAction.NewMatch, PostHandAction.Leave];
        }

        return [PostHandAction.NextRound, PostHandAction.Leave];
    }

    public void TriggerBreakReset()
    {
        IsBreakResetPending = true;
        ConfirmedPoserId = null;
        CanPoserPlayAnyTile = false;
    }

    /// <summary>
    /// Resets all match scores to 0 and immediately starts a new [6|6] opening hand with the same players.
    /// Used by the post-Break (Game Bruk) "Rematch" action.
    /// </summary>
    public void StartRematchAfterBreak(int? seed = null)
    {
        if (IsTeam2v2)
        {
            TeamScores[0] = 0;
            TeamScores[1] = 0;
        }

        foreach (GamePlayer player in Players)
        {
            player.MatchScore = 0;
            PlayerScores[player.Id] = 0;
        }

        IsMatchOver = false;
        MatchWinner = null;
        MatchWinningTeamId = -1;
        CutThroatedPlayerIds.Clear();
        LastHandWasDraw = false;
        LastHandWinnerId = null;
        LastHandPoserId = null;
        CurrentHandPoserId = null;
        TriggerBreakReset();

        gridDto = new GameGridDto(NumberOfRows, NumberOfCols);
        placer = new DominoPlacer(gridDto);
        Stack.Reset(seed);
        DistributeDominoes();
        PlaceFirstDomino();
    }

    /// <summary>
    /// Resets the board and deck to deal a new hand inside the same match while keeping match scores.
    /// </summary>
    public void PrepareNextHand(int? seed = null)
    {
        if (Phase == MatchPhase.CHOOSE_POSER)
        {
            // Default to Pose Myself if starting next hand while CHOOSE_POSER wasn't explicitly answered
            ConfirmPoserChoice(PoserChooserId!.Value, PoserChoice.POSE_MYSELF);
        }

        gridDto = new GameGridDto(NumberOfRows, NumberOfCols);
        placer = new DominoPlacer(gridDto);
        Stack.Reset(seed);
        DistributeDominoes();
    }

    public void DistributeDominoes()
    {
        StartTime = DateTime.Now;
        HandNumber++;
        ConsecutivePasses = 0;
        currentHandScored = false;
        LastHandResolution = null;
        Phase = MatchPhase.AWAITING_POSE;

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
        if (!IsBreakResetPending && ConfirmedPoserId.HasValue)
        {
            GamePlayer? confirmed = Players.FirstOrDefault(p => p.Id == ConfirmedPoserId.Value);
            if (confirmed is not null && confirmed.Hand.Count > 0)
            {
                return (confirmed, FindBestOpeningTileInHand(confirmed));
            }
        }

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
        ApplyOpeningDomino(poser, openingDomino);
    }

    /// <summary>
    /// Allows the confirmed Poser to pose a chosen domino from their hand.
    /// On Hand 1 / post-Break, only the required [6|6] (or fallback) tile is permitted.
    /// On subsequent free-pose hands (including after CHOOSE_POSER), any tile in the Poser's hand is permitted.
    /// </summary>
    public bool PlaceOpeningDomino(long playerId, Domino chosenDomino)
    {
        (GamePlayer poser, Domino requiredOrSuggested) = ResolveOpeningPoser();
        if (poser.Id != playerId)
        {
            return false;
        }

        Domino? inHand = poser.Hand.FirstOrDefault(d =>
            (d.Side1 == chosenDomino.Side1 && d.Side2 == chosenDomino.Side2) ||
            (d.Side1 == chosenDomino.Side2 && d.Side2 == chosenDomino.Side1));
        if (inHand is null)
        {
            return false;
        }

        bool mustPlayRequiredTile = IsBreakResetPending || HandNumber <= 1 && !CanPoserPlayAnyTile;
        if (mustPlayRequiredTile)
        {
            bool matchesRequired =
                (inHand.Side1 == requiredOrSuggested.Side1 && inHand.Side2 == requiredOrSuggested.Side2) ||
                (inHand.Side1 == requiredOrSuggested.Side2 && inHand.Side2 == requiredOrSuggested.Side1);
            if (!matchesRequired)
            {
                return false;
            }
        }

        ApplyOpeningDomino(poser, inHand);
        poser.RecordManualAction();
        return true;
    }

    private void ApplyOpeningDomino(GamePlayer poser, Domino openingDomino)
    {
        Orientation orientation = openingDomino.Side1 == openingDomino.Side2
            ? Orientation.PerpendicularVerticalFromLeft
            : Orientation.HorizontalLeft;

        GameMove move = new(openingDomino, NumberOfRows / 2, NumberOfCols / 2, orientation, poser.Id);
        placer.PlaceFirstDomino(poser, move);

        currentPlayerIndex = Players.FindIndex(p => p.Id == poser.Id);
        CurrentHandPoserId = poser.Id;
        LastHandPoserId = poser.Id;
        IsBreakResetPending = false;
        CanPoserPlayAnyTile = false;
        ConfirmedPoserId = null;
        PoserChooserId = null;
        PartnerCandidateId = null;
        ConsecutivePasses = 0;
        Phase = MatchPhase.PLAYING;
    }

    public long? ConfirmPoserChoice(long chooserId, PoserChoice choice)
    {
        if (Phase != MatchPhase.CHOOSE_POSER || !PoserChooserId.HasValue || chooserId != PoserChooserId.Value)
        {
            return null;
        }

        ConfirmedPoserId = choice == PoserChoice.PASS_TO_PADNA && PartnerCandidateId.HasValue
            ? PartnerCandidateId.Value
            : PoserChooserId.Value;

        CanPoserPlayAnyTile = true;
        ChoosePoserRemainingSeconds = 0;
        Phase = MatchPhase.AWAITING_POSE;
        return ConfirmedPoserId;
    }

    /// <summary>
    /// Advances the 8-second CHOOSE_POSER countdown. Defaults to POSE_MYSELF upon expiration.
    /// Returns true when the timer expires and auto-confirms the chooser.
    /// </summary>
    public bool TickChoosePoserTimer(double deltaSeconds)
    {
        if (Phase != MatchPhase.CHOOSE_POSER)
        {
            return false;
        }

        ChoosePoserRemainingSeconds = Math.Max(0.0, ChoosePoserRemainingSeconds - Math.Max(0.0, deltaSeconds));
        if (ChoosePoserRemainingSeconds <= 0.0 && PoserChooserId.HasValue)
        {
            ConfirmPoserChoice(PoserChooserId.Value, PoserChoice.POSE_MYSELF);
            return true;
        }

        return false;
    }

    public bool PlaceDomino(GameMove move, bool isAutomated = false)
    {
        if (move.PlayerId == PlayerPlaying.Id)
        {
            bool placed = placer.PlaceDomino(PlayerPlaying, new GameMove(move.Domino, move.Row, move.Col, move.Orientation, move.PlayerId));
            if (placed)
            {
                ConsecutivePasses = 0;
                if (!isAutomated)
                {
                    PlayerPlaying.RecordManualAction();
                }
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

    public Domino? Draw(long idUser, bool isAutomated = false)
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
                if (!isAutomated)
                {
                    PlayerPlaying.RecordManualAction();
                }
                return domino;
            }
        }
        return null;
    }

    public bool PassTurn(long idUser, bool isAutomated = false)
    {
        if (!CanPass(idUser))
        {
            return false;
        }

        if (!isAutomated && PlayerPlaying.IsBotControlled)
        {
            PlayerPlaying.ReclaimManualControl();
        }

        ConsecutivePasses++;
        NextTurn();
        return true;
    }

    public bool IsCurrentPlayerAutoPassTurn()
    {
        return Players.Count > 0 && CanPass(PlayerPlaying.Id);
    }

    public double GetActiveTurnTimeoutSeconds()
    {
        return IsCurrentPlayerAutoPassTurn() ? AutoPassTimeoutSeconds : TurnActionTimeoutSeconds;
    }

    public int GetActiveTurnDurationSeconds()
    {
        if (Players.Count > 0 && PlayerPlaying.IsBotControlled)
        {
            return Math.Max(1, (int)Math.Round(BotTakeoverDelaySeconds));
        }

        return (int)GetActiveTurnTimeoutSeconds();
    }

    /// <summary>
    /// Executes the 5-second auto-pass when the active player has zero valid moves (and empty boneyard in DRAW).
    /// Explicitly does NOT increment ConsecutiveAutoPlays.
    /// </summary>
    public bool ExecuteAutoPassOnTimeout(long playerId)
    {
        if (Players.Count == 0 || playerId != PlayerPlaying.Id || !CanPass(playerId))
        {
            return false;
        }

        return PassTurn(playerId, isAutomated: true);
    }

    /// <summary>
    /// Executes the 10-second turn timeout auto-play: performs any required boneyard draws in DRAW mode,
    /// plays a valid tile, increments ConsecutiveAutoPlays (+1), and triggers 3-strike bot takeover at 3.
    /// If the player already had no valid moves and could not draw, performs auto-pass without any AFK strike.
    /// </summary>
    public (bool PlayedTile, bool Passed, List<Domino> DrawnDominoes, GameMove? PlacedMove, bool TriggeredBotTakeover) ExecuteTimeoutAutoPlay(long playerId)
    {
        List<Domino> drawn = [];
        if (Players.Count == 0 || playerId != PlayerPlaying.Id)
        {
            return (false, false, drawn, null, false);
        }

        GamePlayer actingPlayer = PlayerPlaying;

        // No valid move and cannot draw -> 5s auto-pass, NEVER increments AFK strikes
        if (CanPass(playerId))
        {
            bool autoPassed = ExecuteAutoPassOnTimeout(playerId);
            return (false, autoPassed, drawn, null, false);
        }

        while (CanDraw(playerId))
        {
            Domino? tile = Draw(playerId, isAutomated: true);
            if (tile is null) break;
            drawn.Add(tile);
        }

        List<GameMove> possibleMoves = GetPlayerPossibleMoves(actingPlayer);
        if (possibleMoves.Count > 0)
        {
            GameMove chosen = ChooseBestAutoPlayMove(possibleMoves);
            PlaceDomino(chosen, isAutomated: true);
            NextTurn();
            bool tookOver = actingPlayer.RecordTimeoutAutoPlay(BotTakeoverStrikeLimit);
            return (true, false, drawn, chosen, tookOver);
        }

        bool passed = PassTurn(playerId, isAutomated: true);
        bool takeoverAfterDraw = actingPlayer.RecordTimeoutAutoPlay(BotTakeoverStrikeLimit);
        return (false, passed, drawn, null, takeoverAfterDraw);
    }

    /// <summary>
    /// Executes a turn automatically for a seat currently under 3-strike bot takeover (IsBotControlled == true).
    /// Keeps IsBotControlled == true until the human explicitly reclaims control.
    /// </summary>
    public (bool PlayedTile, bool Passed, List<Domino> DrawnDominoes, GameMove? PlacedMove) ExecuteBotTakeoverTurn(long playerId)
    {
        List<Domino> drawn = [];
        if (Players.Count == 0 || playerId != PlayerPlaying.Id || !PlayerPlaying.IsBotControlled)
        {
            return (false, false, drawn, null);
        }

        GamePlayer actingPlayer = PlayerPlaying;
        while (CanDraw(playerId))
        {
            Domino? tile = Draw(playerId, isAutomated: true);
            if (tile is null) break;
            drawn.Add(tile);
        }

        List<GameMove> possibleMoves = GetPlayerPossibleMoves(actingPlayer);
        if (possibleMoves.Count > 0)
        {
            GameMove chosen = ChooseBestAutoPlayMove(possibleMoves);
            PlaceDomino(chosen, isAutomated: true);
            NextTurn();
            return (true, false, drawn, chosen);
        }

        bool passed = PassTurn(playerId, isAutomated: true);
        return (false, passed, drawn, null);
    }

    public bool ReclaimManualControl(long playerId)
    {
        GamePlayer? player = Players.FirstOrDefault(p => p.Id == playerId);
        if (player is null) return false;
        player.ReclaimManualControl();
        return true;
    }

    private static GameMove ChooseBestAutoPlayMove(List<GameMove> moves)
    {
        return moves
            .OrderByDescending(m => m.Domino.Side1 == m.Domino.Side2 ? 1 : 0)
            .ThenByDescending(m => m.Domino.SumOfSides())
            .First();
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
        if (currentHandScored && LastHandResolution is not null)
        {
            return LastHandResolution.IsOver;
        }

        HandResolution raw = ResolveHandEnd();
        UpdateClassement();
        if (!raw.IsOver)
        {
            return false;
        }

        HandResolution finalResolution = ApplyMatchFormatResolution(raw);
        currentHandScored = true;
        LastHandResolution = finalResolution;
        EndTime = DateTime.Now;
        return true;
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

    private HandResolution ApplyMatchFormatResolution(HandResolution raw)
    {
        if (raw.IsDraw)
        {
            LastHandWasDraw = true;
            ConfirmedPoserId = CurrentHandPoserId ?? LastHandPoserId;
            CanPoserPlayAnyTile = true;
            Phase = MatchPhase.HAND_OVER;

            return new HandResolution
            {
                IsOver = true,
                WasBlocked = raw.WasBlocked,
                IsDraw = true,
                Winner = null,
                WinningTeamId = -1,
                PointsAwarded = 0,
                BreakTriggered = false,
                MatchOver = false,
                RequiresChoosePoser = false,
                NextPoserId = ConfirmedPoserId,
                CanPoserPlayAnyTile = true,
                AvailablePostHandActions = GetPostHandActions(breakTriggered: false, matchOver: false),
                IndividualPipCounts = raw.IndividualPipCounts,
                TeamScoresSnapshot = new Dictionary<int, int>(TeamScores),
                PlayerScoresSnapshot = new Dictionary<long, int>(PlayerScores)
            };
        }

        GamePlayer winner = raw.Winner!;
        int winningTeam = raw.WinningTeamId;
        LastHandWasDraw = false;
        LastHandWinnerId = winner.Id;

        bool breakTriggered = false;
        bool matchOver = false;
        bool requiresChoosePoser = false;
        long? poserChooserId = null;
        long? partnerId = null;
        long? nextPoserId = winner.Id;
        bool canPlayAnyTile = true;
        List<long> cutThroated = [];

        if (MatchFormat == MatchFormat.SIX_LOV && IsTeam2v2)
        {
            int oppTeam = winningTeam == 0 ? 1 : 0;
            int winScore = TeamScores[winningTeam];
            int oppScore = TeamScores[oppTeam];

            if (OneOnePlayoff)
            {
                if (winScore == 0 && oppScore == 1)
                {
                    TeamScores[winningTeam] = 1;
                    TeamScores[oppTeam] = 1;
                }
                else if (winScore == 1 && oppScore == 1)
                {
                    TeamScores[winningTeam] = 2;
                    TeamScores[oppTeam] = 0;
                }
                else if (oppScore >= 2 && oppScore <= 5)
                {
                    TeamScores[0] = 0;
                    TeamScores[1] = 0;
                    breakTriggered = true;
                }
                else
                {
                    TeamScores[winningTeam] = winScore + 1;
                }
            }
            else
            {
                if (oppScore >= 1 && oppScore <= 5)
                {
                    TeamScores[0] = 0;
                    TeamScores[1] = 0;
                    breakTriggered = true;
                }
                else
                {
                    TeamScores[winningTeam] = winScore + 1;
                }
            }

            SyncTeamScoresToPlayers();

            if (breakTriggered)
            {
                TriggerBreakReset();
                nextPoserId = null;
                canPlayAnyTile = false;
                Phase = MatchPhase.HAND_OVER;
            }
            else if (TeamScores[winningTeam] >= 6)
            {
                matchOver = true;
                IsMatchOver = true;
                MatchWinner = winner;
                MatchWinningTeamId = winningTeam;
                Phase = MatchPhase.MATCH_OVER;
            }
            else
            {
                EnterChoosePoserPhase(winner);
                requiresChoosePoser = true;
                poserChooserId = PoserChooserId;
                partnerId = PartnerCandidateId;
                nextPoserId = null;
                canPlayAnyTile = true;
            }
        }
        else if (MatchFormat == MatchFormat.TWO_BEST)
        {
            if (IsTeam2v2)
            {
                TeamScores[winningTeam]++;
                SyncTeamScoresToPlayers();

                if (TeamScores[winningTeam] >= 2)
                {
                    matchOver = true;
                    IsMatchOver = true;
                    MatchWinner = winner;
                    MatchWinningTeamId = winningTeam;
                    Phase = MatchPhase.MATCH_OVER;
                }
                else
                {
                    EnterChoosePoserPhase(winner);
                    requiresChoosePoser = true;
                    poserChooserId = PoserChooserId;
                    partnerId = PartnerCandidateId;
                    nextPoserId = null;
                    canPlayAnyTile = true;
                }
            }
            else
            {
                PlayerScores[winner.Id]++;
                winner.MatchScore = PlayerScores[winner.Id];

                if (PlayerScores[winner.Id] >= 2)
                {
                    matchOver = true;
                    IsMatchOver = true;
                    MatchWinner = winner;
                    MatchWinningTeamId = winningTeam;
                    Phase = MatchPhase.MATCH_OVER;
                }
                else
                {
                    ConfirmedPoserId = winner.Id;
                    CanPoserPlayAnyTile = true;
                    nextPoserId = winner.Id;
                    canPlayAnyTile = true;
                    Phase = MatchPhase.HAND_OVER;
                }
            }
        }
        else // CUT_THROAT (Free-For-All: 2, 3, or 4 players)
        {
            PlayerScores[winner.Id]++;
            winner.MatchScore = PlayerScores[winner.Id];

            if (PlayerScores[winner.Id] >= 6)
            {
                matchOver = true;
                IsMatchOver = true;
                MatchWinner = winner;
                MatchWinningTeamId = winningTeam;
                CutThroatedPlayerIds.Clear();
                foreach (GamePlayer p in Players)
                {
                    if (PlayerScores[p.Id] == 0)
                    {
                        CutThroatedPlayerIds.Add(p.Id);
                        cutThroated.Add(p.Id);
                    }
                }
                Phase = MatchPhase.MATCH_OVER;
            }
            else if (Players.All(p => PlayerScores[p.Id] >= 1))
            {
                // Cut-Throat Break: every player has won at least 1 hand -> reset all to 0 and force [6|6]/fallback
                foreach (GamePlayer p in Players)
                {
                    PlayerScores[p.Id] = 0;
                    p.MatchScore = 0;
                }
                breakTriggered = true;
                TriggerBreakReset();
                nextPoserId = null;
                canPlayAnyTile = false;
                Phase = MatchPhase.HAND_OVER;
            }
            else
            {
                ConfirmedPoserId = winner.Id;
                CanPoserPlayAnyTile = true;
                nextPoserId = winner.Id;
                canPlayAnyTile = true;
                Phase = MatchPhase.HAND_OVER;
            }
        }

        return new HandResolution
        {
            IsOver = true,
            WasBlocked = raw.WasBlocked,
            IsDraw = false,
            Winner = winner,
            WinningTeamId = winningTeam,
            PointsAwarded = breakTriggered ? 0 : 1,
            BreakTriggered = breakTriggered,
            MatchOver = matchOver,
            MatchWinner = matchOver ? winner : null,
            MatchWinningTeamId = matchOver ? winningTeam : -1,
            RequiresChoosePoser = requiresChoosePoser,
            PoserChooserId = poserChooserId,
            PartnerCandidateId = partnerId,
            NextPoserId = nextPoserId,
            CanPoserPlayAnyTile = canPlayAnyTile,
            CutThroatedPlayerIds = cutThroated,
            AvailablePostHandActions = GetPostHandActions(breakTriggered, matchOver),
            IndividualPipCounts = raw.IndividualPipCounts,
            TeamScoresSnapshot = new Dictionary<int, int>(TeamScores),
            PlayerScoresSnapshot = new Dictionary<long, int>(PlayerScores)
        };
    }

    private void EnterChoosePoserPhase(GamePlayer handWinner)
    {
        GamePlayer? partner = GetPartner(handWinner);
        PoserChooserId = handWinner.Id;
        PartnerCandidateId = partner?.Id;
        ConfirmedPoserId = null;
        CanPoserPlayAnyTile = true;
        ChoosePoserRemainingSeconds = ChoosePoserTimeoutSeconds;
        Phase = MatchPhase.CHOOSE_POSER;
    }

    private void SyncTeamScoresToPlayers()
    {
        foreach (GamePlayer p in Players)
        {
            int team = GetEffectiveTeamId(p);
            int score = TeamScores.TryGetValue(team, out int val) ? val : 0;
            p.MatchScore = score;
            PlayerScores[p.Id] = score;
        }
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
