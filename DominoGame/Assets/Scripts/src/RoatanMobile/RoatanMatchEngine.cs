using System;
using System.Collections.Generic;
using System.Linq;

using DominoCommon.com.domino.common.Scripts.Enums;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;

public class RoatanMatchEngine
{
    public const float ChoosePoserTimeoutSeconds = 8.0f;
    public const float AutoPassTimeoutSeconds = 5.0f;
    public const float TurnTimeoutSeconds = 10.0f;
    public const int BotTakeoverStrikeLimit = 3;
    public const float BotTakeoverPacingSeconds = 1.2f;

    public class MatchPlayer
    {
        public int Id { get; set; }
        public int TeamId { get; set; }
        public string Name { get; set; }
        public bool IsBot { get; set; }
        public BotDifficulty? Difficulty { get; set; }
        public int MatchScore { get; set; }
        public int ConsecutiveAutoPlays { get; set; }
        public bool IsBotControlled { get; set; }
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
        public bool MatchOver { get; set; }
        public MatchPlayer MatchWinner { get; set; }
        public int MatchWinningTeamId { get; set; } = -1;
        public bool RequiresChoosePoser { get; set; }
        public int PoserChooserIndex { get; set; } = -1;
        public int PartnerCandidateIndex { get; set; } = -1;
        public bool CanPoserPlayAnyTile { get; set; }
        public int PointsAwarded { get; set; }
        public bool IsKeyWin { get; set; }
        public int NextPoserIndex { get; set; } = -1;
        public List<MatchPlayer> CutThroatedPlayers { get; set; } = new List<MatchPlayer>();
        public List<PostHandAction> AvailablePostHandActions { get; set; } = new List<PostHandAction>();
        public List<MatchPlayer> Standings { get; set; }
    }

    public RoatanGameMode Mode { get; }
    public TableStyle TableStyle { get; }
    public MatchFormat MatchFormat { get; }
    public bool OneOnePlayoff { get; }
    public TurnDirection TurnDirection { get; set; }
    public bool IsTeam2v2 { get; }
    public BotDifficulty Difficulty { get; }
    public List<MatchPlayer> Players { get; } = new List<MatchPlayer>();
    public MatchPlayer CurrentPlayer => Players[currentPlayerIndex];
    public int CurrentPlayerIndex => currentPlayerIndex;
    public int BoneyardCount => boneyard.Count;
    public int LockedPileCount => lockedPile.Count;
    public int RoundNumber { get; private set; }
    public int TargetMatchScore => MatchFormat == MatchFormat.TWO_BEST ? 2 : 6;
    public int ConsecutivePasses => consecutivePasses;
    public MatchPhase Phase { get; private set; } = MatchPhase.AWAITING_POSE;
    public bool IsBreakResetPending { get; private set; } = true;
    public int CurrentHandPoserIndex { get; private set; } = -1;
    public int LastHandPoserIndex { get; private set; } = -1;
    public bool LastHandWasDraw { get; private set; }

    // Format & Partner Pose Handoff ("Call Your Padna") state
    public int[] TeamScores { get; } = new int[2];
    public bool IsMatchOver { get; private set; }
    public MatchPlayer MatchWinner { get; private set; }
    public int MatchWinningTeamId { get; private set; } = -1;
    public List<MatchPlayer> CutThroatedPlayers { get; } = new List<MatchPlayer>();
    public int PoserChooserIndex { get; private set; } = -1;
    public int PartnerCandidateIndex { get; private set; } = -1;
    public int ConfirmedPoserIndex { get; private set; } = -1;
    public float ChoosePoserRemainingSeconds { get; private set; }
    public bool CanPoserPlayAnyTile { get; private set; }
    public bool PartnerHandsHiddenDuringChoosePoser => Phase == MatchPhase.CHOOSE_POSER;

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
    private bool lastDominoOutWasKey;

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
            turnDirection: TurnDirection.CounterClockwise,
            oneOnePlayoff: false)
    {
    }

    public RoatanMatchEngine(
        TableStyle tableStyle,
        MatchFormat matchFormat,
        BotDifficulty difficulty,
        int botCount,
        int? seed = null,
        bool isTeam2v2 = false,
        TurnDirection turnDirection = TurnDirection.CounterClockwise,
        bool oneOnePlayoff = false)
    {
        TableStyle = tableStyle;
        MatchFormat = matchFormat;
        OneOnePlayoff = oneOnePlayoff;
        Mode = tableStyle == TableStyle.BLOCK ? RoatanGameMode.BlockedCerrado : RoatanGameMode.ClassicDraw;
        TurnDirection = turnDirection;
        Difficulty = difficulty;
        rng = seed.HasValue ? new Random(seed.Value) : new Random();

        int clampedBots = matchFormat == MatchFormat.SIX_LOV ? 3 : Math.Max(1, Math.Min(3, botCount));
        int totalPlayers = 1 + clampedBots;

        if (matchFormat == MatchFormat.SIX_LOV)
        {
            IsTeam2v2 = totalPlayers == 4;
        }
        else if (matchFormat == MatchFormat.CUT_THROAT)
        {
            IsTeam2v2 = false;
        }
        else
        {
            IsTeam2v2 = totalPlayers == 4;
        }

        Players.Add(new MatchPlayer
        {
            Id = 0,
            TeamId = 0,
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

    public MatchPlayer GetPartner(MatchPlayer player)
    {
        if (!IsTeam2v2 || Players.Count != 4 || player == null) return null;
        int idx = Players.FindIndex(p => p.Id == player.Id);
        if (idx < 0) return null;
        return Players[(idx + 2) % 4];
    }

    public static int GetTilesPerPlayer(int playerCount, TableStyle tableStyle)
    {
        if (playerCount == 2) return 7;
        if (playerCount == 3) return tableStyle == TableStyle.BLOCK ? 9 : 7;
        if (playerCount == 4) return tableStyle == TableStyle.BLOCK ? 7 : 6;
        return 7;
    }

    public static List<PostHandAction> GetPostHandActions(bool breakTriggered, bool matchOver)
    {
        if (breakTriggered || matchOver)
        {
            return new List<PostHandAction>
            {
                PostHandAction.Rematch,
                PostHandAction.NewMatch,
                PostHandAction.Leave
            };
        }

        return new List<PostHandAction>
        {
            PostHandAction.NextRound,
            PostHandAction.Leave
        };
    }

    public void TriggerBreakReset()
    {
        IsBreakResetPending = true;
        ConfirmedPoserIndex = -1;
        CanPoserPlayAnyTile = false;
    }

    /// <summary>
    /// Resets all match scores to 0 and immediately starts a new [6|6] opening hand with the same players.
    /// Used by the post-Break (Game Bruk) "Rematch" action.
    /// </summary>
    public PlacedTile StartRematchAfterBreak()
    {
        TeamScores[0] = 0;
        TeamScores[1] = 0;
        foreach (MatchPlayer player in Players)
        {
            player.MatchScore = 0;
        }

        IsMatchOver = false;
        MatchWinner = null;
        MatchWinningTeamId = -1;
        CutThroatedPlayers.Clear();
        LastHandWasDraw = false;
        lastRoundWinnerIndex = -1;
        LastHandPoserIndex = -1;
        CurrentHandPoserIndex = -1;
        TriggerBreakReset();

        return StartNewRound();
    }

    public void DealRoundWithoutOpeningPose()
    {
        if (Phase == MatchPhase.CHOOSE_POSER && PoserChooserIndex >= 0)
        {
            ConfirmPoserChoice(Players[PoserChooserIndex].Id, PoserChoice.POSE_MYSELF);
        }

        RoundNumber++;
        centerTile = null;
        leftChain.Clear();
        rightChain.Clear();
        playedDominoes.Clear();
        lockedPile.Clear();
        LeftEndPip = -1;
        RightEndPip = -1;
        consecutivePasses = 0;
        lastDominoOutWasKey = false;
        Phase = MatchPhase.AWAITING_POSE;

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

        if (TableStyle == TableStyle.BLOCK && boneyard.Count > 0)
        {
            lockedPile.AddRange(boneyard);
            boneyard.Clear();
        }
    }

    public PlacedTile StartNewRound()
    {
        DealRoundWithoutOpeningPose();
        ResolveOpeningPose(out int starterIdx, out DominoDto starterTile);
        return ApplyOpeningPose(starterIdx, starterTile);
    }

    public void ResolveOpeningPose(out int starterIdx, out DominoDto starterTile)
    {
        if (!IsBreakResetPending && ConfirmedPoserIndex >= 0 && ConfirmedPoserIndex < Players.Count)
        {
            starterIdx = ConfirmedPoserIndex;
            starterTile = FindBestOpeningTile(Players[starterIdx]);
            return;
        }

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

    public PlacedTile PlayOpeningPose(int playerId, DominoDto chosenTile)
    {
        if (Phase != MatchPhase.AWAITING_POSE || chosenTile == null) return null;

        ResolveOpeningPose(out int starterIdx, out DominoDto requiredTile);
        MatchPlayer starter = Players[starterIdx];
        if (starter.Id != playerId) return null;

        DominoDto inHand = FindInHand(starter, chosenTile);
        if (inHand == null) return null;

        bool mustPlayRequired = IsBreakResetPending || (RoundNumber <= 1 && !CanPoserPlayAnyTile);
        if (mustPlayRequired)
        {
            bool matches = (inHand.Side1 == requiredTile.Side1 && inHand.Side2 == requiredTile.Side2) ||
                           (inHand.Side1 == requiredTile.Side2 && inHand.Side2 == requiredTile.Side1);
            if (!matches) return null;
        }

        return ApplyOpeningPose(starterIdx, inHand);
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
        CanPoserPlayAnyTile = false;
        ConfirmedPoserIndex = -1;
        PoserChooserIndex = -1;
        PartnerCandidateIndex = -1;
        Phase = MatchPhase.PLAYING;
        currentPlayerIndex = StepTurnIndex(starterIdx);
        return centerTile;
    }

    public int ConfirmPoserChoice(int chooserPlayerId, PoserChoice choice)
    {
        if (Phase != MatchPhase.CHOOSE_POSER || PoserChooserIndex < 0)
        {
            return -1;
        }

        if (Players[PoserChooserIndex].Id != chooserPlayerId)
        {
            return -1;
        }

        ConfirmedPoserIndex = (choice == PoserChoice.PASS_TO_PADNA && PartnerCandidateIndex >= 0)
            ? PartnerCandidateIndex
            : PoserChooserIndex;

        CanPoserPlayAnyTile = true;
        ChoosePoserRemainingSeconds = 0f;
        Phase = MatchPhase.AWAITING_POSE;
        return ConfirmedPoserIndex;
    }

    public bool TickChoosePoserTimer(float deltaSeconds)
    {
        if (Phase != MatchPhase.CHOOSE_POSER)
        {
            return false;
        }

        ChoosePoserRemainingSeconds = Math.Max(0f, ChoosePoserRemainingSeconds - Math.Max(0f, deltaSeconds));
        if (ChoosePoserRemainingSeconds <= 0f && PoserChooserIndex >= 0)
        {
            ConfirmPoserChoice(Players[PoserChooserIndex].Id, PoserChoice.POSE_MYSELF);
            return true;
        }

        return false;
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

        int leftBefore = LeftEndPip;
        int rightBefore = RightEndPip;
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
        lastDominoOutWasKey = player.Hand.Count == 0 && IsNaturalKeyWin(inHand, leftBefore, rightBefore);

        currentPlayerIndex = StepTurnIndex(currentPlayerIndex);
        return placed;
    }

    private bool IsNaturalKeyWin(DominoDto winningTile, int leftOpenBefore, int rightOpenBefore)
    {
        if (winningTile == null || leftOpenBefore < 0 || rightOpenBefore < 0) return false;
        int a = winningTile.Side1;
        int b = winningTile.Side2;
        if (a == b) return false; // Doubles never qualify as Key/Capicu

        bool matchesBothEnds = (leftOpenBefore == a && rightOpenBefore == b) ||
                               (leftOpenBefore == b && rightOpenBefore == a);
        if (!matchesBothEnds) return false;

        // Verify both suits A and B are completely exhausted from all hands, boneyard, and locked pile
        foreach (MatchPlayer p in Players)
        {
            foreach (DominoDto d in p.Hand)
            {
                if (d.Side1 == a || d.Side2 == a || d.Side1 == b || d.Side2 == b)
                {
                    return false;
                }
            }
        }

        foreach (DominoDto d in boneyard)
        {
            if (d.Side1 == a || d.Side2 == a || d.Side1 == b || d.Side2 == b)
            {
                return false;
            }
        }

        foreach (DominoDto d in lockedPile)
        {
            if (d.Side1 == a || d.Side2 == a || d.Side1 == b || d.Side2 == b)
            {
                return false;
            }
        }

        return true;
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

    public bool IsCurrentPlayerAutoPassTurn()
    {
        return Players.Count > 0 && CanCurrentPlayerPass();
    }

    public float GetActiveTurnTimeoutSeconds()
    {
        return IsCurrentPlayerAutoPassTurn() ? AutoPassTimeoutSeconds : TurnTimeoutSeconds;
    }

    public void RecordManualAction(int playerId)
    {
        MatchPlayer player = Players.FirstOrDefault(p => p.Id == playerId);
        if (player == null) return;
        player.ConsecutiveAutoPlays = 0;
        player.IsBotControlled = false;
    }

    public bool ReclaimManualControl(int playerId)
    {
        MatchPlayer player = Players.FirstOrDefault(p => p.Id == playerId);
        if (player == null) return false;
        player.IsBotControlled = false;
        player.ConsecutiveAutoPlays = 0;
        return true;
    }

    /// <summary>
    /// Executes the 5-second auto-pass when the active player has zero playable tiles
    /// (and an empty boneyard in DRAW mode). Must NOT increment ConsecutiveAutoPlays.
    /// </summary>
    public bool ExecuteAutoPassNoPenalty()
    {
        if (!CanCurrentPlayerPass())
        {
            return false;
        }

        return PassTurn();
    }

    /// <summary>
    /// Executes the 10-second turn timeout auto-play: performs any required boneyard draws
    /// and plays a valid tile using RoatanBotBrain selection logic, incrementing ConsecutiveAutoPlays (+1)
    /// and activating 3-strike bot takeover at 3 consecutive timeouts.
    /// </summary>
    public bool ExecuteTimeoutAutoPlay(
        RoatanBotBrain brain,
        out PlacedTile placedTile,
        out List<DominoDto> drawnTiles,
        out bool passed,
        out bool triggeredBotTakeover)
    {
        placedTile = null;
        drawnTiles = new List<DominoDto>();
        passed = false;
        triggeredBotTakeover = false;

        if (Players.Count == 0) return false;
        MatchPlayer active = CurrentPlayer;

        // Auto-passing when having no valid moves must NOT increment the AFK auto-play counter
        if (CanCurrentPlayerPass())
        {
            passed = ExecuteAutoPassNoPenalty();
            return passed;
        }

        while (CanCurrentPlayerDraw())
        {
            DominoDto drawn = DrawFromBoneyard();
            if (drawn == null) break;
            drawnTiles.Add(drawn);
        }

        List<LegalMove> moves = GetLegalMoves(active);
        if (moves.Count > 0)
        {
            LegalMove chosen = brain != null
                ? brain.ChooseMove(this, active, moves, Difficulty)
                : moves[0];
            placedTile = PlayMove(chosen);
        }
        else if (CanCurrentPlayerPass())
        {
            passed = PassTurn();
        }

        active.ConsecutiveAutoPlays++;
        if (active.ConsecutiveAutoPlays >= BotTakeoverStrikeLimit)
        {
            active.IsBotControlled = true;
            triggeredBotTakeover = true;
        }

        return placedTile != null || passed;
    }

    /// <summary>
    /// Plays a turn automatically for a human seat currently under 3-strike bot takeover (IsBotControlled == true).
    /// Leaves IsBotControlled == true until the player reclaims control.
    /// </summary>
    public bool ExecuteBotControlledTurn(
        RoatanBotBrain brain,
        out PlacedTile placedTile,
        out List<DominoDto> drawnTiles,
        out bool passed)
    {
        placedTile = null;
        drawnTiles = new List<DominoDto>();
        passed = false;

        if (Players.Count == 0) return false;
        MatchPlayer active = CurrentPlayer;
        if (!active.IsBotControlled) return false;

        while (CanCurrentPlayerDraw())
        {
            DominoDto drawn = DrawFromBoneyard();
            if (drawn == null) break;
            drawnTiles.Add(drawn);
        }

        List<LegalMove> moves = GetLegalMoves(active);
        if (moves.Count > 0)
        {
            LegalMove chosen = brain != null
                ? brain.ChooseMove(this, active, moves, Difficulty)
                : moves[0];
            placedTile = PlayMove(chosen);
        }
        else if (CanCurrentPlayerPass())
        {
            passed = PassTurn();
        }

        return placedTile != null || passed;
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

        MatchPlayer winner = Players.FirstOrDefault(p => p.Hand.Count == 0);
        int winningTeam = -1;

        if (winner != null)
        {
            wasBlocked = false;
            winningTeam = winner.TeamId;
        }
        else
        {
            wasBlocked = true;
            int lowestIndividualPips = Players.Min(p => p.HandPipSum());
            List<MatchPlayer> lowestPlayers = Players
                .Where(p => p.HandPipSum() == lowestIndividualPips)
                .ToList();

            List<int> distinctLowestTeams = lowestPlayers
                .Select(p => p.TeamId)
                .Distinct()
                .ToList();

            if (distinctLowestTeams.Count > 1)
            {
                LastHandWasDraw = true;
                ConfirmedPoserIndex = CurrentHandPoserIndex;
                CanPoserPlayAnyTile = true;
                Phase = MatchPhase.HAND_OVER;

                return new RoundResult
                {
                    Winner = null,
                    WinningTeamId = -1,
                    WasBlocked = true,
                    IsDraw = true,
                    BreakTriggered = false,
                    MatchOver = false,
                    RequiresChoosePoser = false,
                    CanPoserPlayAnyTile = true,
                    PointsAwarded = 0,
                    NextPoserIndex = CurrentHandPoserIndex,
                    AvailablePostHandActions = GetPostHandActions(breakTriggered: false, matchOver: false),
                    Standings = standings
                };
            }

            winner = lowestPlayers[0];
            winningTeam = distinctLowestTeams[0];
        }

        LastHandWasDraw = false;
        int winnerIdx = Players.FindIndex(p => p.Id == winner.Id);
        lastRoundWinnerIndex = winnerIdx;

        bool isKeyWin = !wasBlocked && lastDominoOutWasKey;
        int winAward = isKeyWin ? 2 : 1;
        int awardedPoints = winAward;

        bool breakTriggered = false;
        bool matchOver = false;
        bool requiresChoosePoser = false;
        int chooserIdx = -1;
        int partnerIdx = -1;
        int nextPoserIdx = winnerIdx;
        bool canPlayAnyTile = true;
        List<MatchPlayer> cutThroated = new List<MatchPlayer>();

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
                    awardedPoints = 1;
                }
                else if (winScore == 1 && oppScore == 1)
                {
                    TeamScores[winningTeam] = Math.Min(6, 1 + winAward);
                    TeamScores[oppTeam] = 0;
                }
                else if (oppScore >= 2 && oppScore <= 5)
                {
                    TeamScores[0] = 0;
                    TeamScores[1] = 0;
                    breakTriggered = true;
                    awardedPoints = 0;
                }
                else
                {
                    TeamScores[winningTeam] = Math.Min(6, winScore + winAward);
                }
            }
            else
            {
                if (oppScore >= 1 && oppScore <= 5)
                {
                    TeamScores[0] = 0;
                    TeamScores[1] = 0;
                    breakTriggered = true;
                    awardedPoints = 0;
                }
                else
                {
                    TeamScores[winningTeam] = Math.Min(6, winScore + winAward);
                }
            }

            SyncTeamScores();

            if (breakTriggered)
            {
                TriggerBreakReset();
                nextPoserIdx = -1;
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
                EnterChoosePoserPhase(winnerIdx);
                requiresChoosePoser = true;
                chooserIdx = PoserChooserIndex;
                partnerIdx = PartnerCandidateIndex;
                nextPoserIdx = -1;
                canPlayAnyTile = true;
            }
        }
        else if (MatchFormat == MatchFormat.TWO_BEST)
        {
            if (IsTeam2v2)
            {
                TeamScores[winningTeam] = Math.Min(2, TeamScores[winningTeam] + winAward);
                SyncTeamScores();

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
                    EnterChoosePoserPhase(winnerIdx);
                    requiresChoosePoser = true;
                    chooserIdx = PoserChooserIndex;
                    partnerIdx = PartnerCandidateIndex;
                    nextPoserIdx = -1;
                    canPlayAnyTile = true;
                }
            }
            else
            {
                winner.MatchScore = Math.Min(2, winner.MatchScore + winAward);
                if (winner.MatchScore >= 2)
                {
                    matchOver = true;
                    IsMatchOver = true;
                    MatchWinner = winner;
                    MatchWinningTeamId = winningTeam;
                    Phase = MatchPhase.MATCH_OVER;
                }
                else
                {
                    ConfirmedPoserIndex = winnerIdx;
                    CanPoserPlayAnyTile = true;
                    Phase = MatchPhase.HAND_OVER;
                }
            }
        }
        else // CUT_THROAT (Free-For-All)
        {
            bool everyoneHasScoredAfterWin = Players.All(p => p.Id == winner.Id || p.MatchScore >= 1);
            int nextScore = Math.Min(6, winner.MatchScore + winAward);

            if (everyoneHasScoredAfterWin && nextScore < 6)
            {
                foreach (MatchPlayer p in Players)
                {
                    p.MatchScore = 0;
                }
                breakTriggered = true;
                awardedPoints = 0;
                TriggerBreakReset();
                nextPoserIdx = -1;
                canPlayAnyTile = false;
                Phase = MatchPhase.HAND_OVER;
            }
            else
            {
                winner.MatchScore = nextScore;
                if (winner.MatchScore >= 6)
                {
                    matchOver = true;
                    IsMatchOver = true;
                    MatchWinner = winner;
                    MatchWinningTeamId = winningTeam;
                    CutThroatedPlayers.Clear();
                    foreach (MatchPlayer p in Players)
                    {
                        if (p.MatchScore == 0)
                        {
                            CutThroatedPlayers.Add(p);
                            cutThroated.Add(p);
                        }
                    }
                    Phase = MatchPhase.MATCH_OVER;
                }
                else
                {
                    ConfirmedPoserIndex = winnerIdx;
                    CanPoserPlayAnyTile = true;
                    Phase = MatchPhase.HAND_OVER;
                }
            }
        }

        return new RoundResult
        {
            Winner = winner,
            WinningTeamId = winningTeam,
            WasBlocked = wasBlocked,
            IsDraw = false,
            BreakTriggered = breakTriggered,
            MatchOver = matchOver,
            MatchWinner = matchOver ? winner : null,
            MatchWinningTeamId = matchOver ? winningTeam : -1,
            RequiresChoosePoser = requiresChoosePoser,
            PoserChooserIndex = chooserIdx,
            PartnerCandidateIndex = partnerIdx,
            CanPoserPlayAnyTile = canPlayAnyTile,
            PointsAwarded = awardedPoints,
            IsKeyWin = isKeyWin,
            NextPoserIndex = nextPoserIdx,
            CutThroatedPlayers = cutThroated,
            AvailablePostHandActions = GetPostHandActions(breakTriggered, matchOver),
            Standings = standings
        };
    }

    private void EnterChoosePoserPhase(int winnerIdx)
    {
        PoserChooserIndex = winnerIdx;
        PartnerCandidateIndex = (winnerIdx + 2) % Players.Count;
        ConfirmedPoserIndex = -1;
        CanPoserPlayAnyTile = true;
        ChoosePoserRemainingSeconds = ChoosePoserTimeoutSeconds;
        Phase = MatchPhase.CHOOSE_POSER;
    }

    private void SyncTeamScores()
    {
        foreach (MatchPlayer p in Players)
        {
            if (p.TeamId >= 0 && p.TeamId < TeamScores.Length)
            {
                p.MatchScore = TeamScores[p.TeamId];
            }
        }
    }

    public bool HasMatchChampion(out MatchPlayer champion)
    {
        if (IsMatchOver && MatchWinner != null)
        {
            champion = MatchWinner;
            return true;
        }

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
