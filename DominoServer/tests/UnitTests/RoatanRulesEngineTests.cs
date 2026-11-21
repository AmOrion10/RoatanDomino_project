using Domain;
using Domain.Game;
using DominoCommon.com.domino.common.Scripts.Enums;
using DominoCommon.com.domino.common.Scripts.Networking;

namespace UnitTests;

public class RoatanRulesEngineTests
{
    private static List<GamePlayer> CreatePlayers(int count)
    {
        string[] names = ["You", "Anthony", "Patrik", "Astor"];
        List<GamePlayer> players = [];
        for (int i = 0; i < count; i++)
        {
            players.Add(new GamePlayer(new User(string.Empty, names[i]))
            {
                Id = i + 1
            });
        }
        return players;
    }

    // ==================== 1. DECOUPLED TABLE STYLE & MATCH FORMAT ====================

    [Theory]
    [InlineData(TableStyle.BLOCK, MatchFormat.SIX_LOV)]
    [InlineData(TableStyle.BLOCK, MatchFormat.TWO_BEST)]
    [InlineData(TableStyle.BLOCK, MatchFormat.CUT_THROAT)]
    [InlineData(TableStyle.DRAW, MatchFormat.SIX_LOV)]
    [InlineData(TableStyle.DRAW, MatchFormat.TWO_BEST)]
    [InlineData(TableStyle.DRAW, MatchFormat.CUT_THROAT)]
    public void DecoupledTableStyleAndMatchFormatAreIndependent(TableStyle tableStyle, MatchFormat matchFormat)
    {
        Game game = new(1, CreatePlayers(4), GameMode.UnRanked, tableStyle, matchFormat, seed: 42);
        Check.That(game.TableStyle).IsEqualTo(tableStyle);
        Check.That(game.MatchFormat).IsEqualTo(matchFormat);
        Check.That(game.TurnDirection).IsEqualTo(TurnDirection.CounterClockwise);
    }

    [Fact]
    public void LegacyConstructorRemainsBackwardCompatible()
    {
        Game game = new(1, CreatePlayers(2), GameMode.UnRanked, seed: 2002);
        Check.That(game.TableStyle).IsEqualTo(TableStyle.DRAW);
        Check.That(game.MatchFormat).IsEqualTo(MatchFormat.CUT_THROAT);
        Check.That(game.IsLegacyDrawAdvance).IsTrue();
        Check.That(game.TurnDirection).IsEqualTo(TurnDirection.CounterClockwise);
    }

    // ==================== 2. DECK & DEALING RULES ====================

    [Fact]
    public void Deal_2P_Block_Deals7EachAndLocksRemaining14()
    {
        Game game = new(1, CreatePlayers(2), GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.TWO_BEST, seed: 101);
        game.DistributeDominoes();

        Check.That(game.Players[0].Hand.Count).IsEqualTo(7);
        Check.That(game.Players[1].Hand.Count).IsEqualTo(7);
        Check.That(game.Stack.IsLocked).IsTrue();
        Check.That(game.Stack.LockedPileCount).IsEqualTo(14);
        Check.That(game.Stack.ActiveBoneyardCount).IsEqualTo(0);
        Check.That(game.Stack.IsEmpty()).IsTrue();
        Check.That(game.Draw(game.Players[0].Id)).IsNull();
    }

    [Fact]
    public void Deal_2P_Draw_Deals7EachAndKeeps14InActiveBoneyard()
    {
        Game game = new(1, CreatePlayers(2), GameMode.UnRanked, TableStyle.DRAW, MatchFormat.CUT_THROAT, seed: 102);
        game.DistributeDominoes();

        Check.That(game.Players[0].Hand.Count).IsEqualTo(7);
        Check.That(game.Players[1].Hand.Count).IsEqualTo(7);
        Check.That(game.Stack.IsLocked).IsFalse();
        Check.That(game.Stack.ActiveBoneyardCount).IsEqualTo(14);
        Check.That(game.Stack.LockedPileCount).IsEqualTo(0);
    }

    [Fact]
    public void Deal_3P_Block_RemovesDoubleBlankAndDeals9EachWith0Boneyard()
    {
        Game game = new(1, CreatePlayers(3), GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.CUT_THROAT, seed: 103);
        game.DistributeDominoes();

        Check.That(game.Players[0].Hand.Count).IsEqualTo(9);
        Check.That(game.Players[1].Hand.Count).IsEqualTo(9);
        Check.That(game.Players[2].Hand.Count).IsEqualTo(9);
        Check.That(game.Stack.ActiveBoneyardCount).IsEqualTo(0);
        Check.That(game.Stack.TotalRemainingTiles()).IsEqualTo(0);

        bool anyHasDoubleBlank = game.Players.SelectMany(p => p.Hand).Any(d => d.Side1 == 0 && d.Side2 == 0);
        Check.That(anyHasDoubleBlank).IsFalse();
    }

    [Fact]
    public void Deal_3P_Draw_Uses28TilesDeals7EachAnd7Boneyard()
    {
        Game game = new(1, CreatePlayers(3), GameMode.UnRanked, TableStyle.DRAW, MatchFormat.CUT_THROAT, seed: 104);
        game.DistributeDominoes();

        Check.That(game.Players[0].Hand.Count).IsEqualTo(7);
        Check.That(game.Players[1].Hand.Count).IsEqualTo(7);
        Check.That(game.Players[2].Hand.Count).IsEqualTo(7);
        Check.That(game.Stack.ActiveBoneyardCount).IsEqualTo(7);
    }

    [Fact]
    public void Deal_4P_Block_Deals7EachWith0Boneyard()
    {
        Game game = new(1, CreatePlayers(4), GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.SIX_LOV, seed: 105, isTeam2v2: true);
        game.DistributeDominoes();

        foreach (GamePlayer player in game.Players)
        {
            Check.That(player.Hand.Count).IsEqualTo(7);
        }
        Check.That(game.Stack.ActiveBoneyardCount).IsEqualTo(0);
        Check.That(game.Stack.TotalRemainingTiles()).IsEqualTo(0);
    }

    [Fact]
    public void Deal_4P_Draw_Deals6EachWith4Boneyard()
    {
        Game game = new(1, CreatePlayers(4), GameMode.UnRanked, TableStyle.DRAW, MatchFormat.CUT_THROAT, seed: 106);
        game.DistributeDominoes();

        foreach (GamePlayer player in game.Players)
        {
            Check.That(player.Hand.Count).IsEqualTo(6);
        }
        Check.That(game.Stack.ActiveBoneyardCount).IsEqualTo(4);
    }

    // ==================== 3. TURN ORDER & DRAW / PASS LOGIC ====================

    [Fact]
    public void TurnOrder_DefaultsToCounterClockwiseFor4Players()
    {
        Game game = new(1, CreatePlayers(4), GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.SIX_LOV, seed: 10);
        // Player 0 starts at index 0
        Check.That(game.PlayerPlaying.Id).IsEqualTo(1);

        game.NextTurn();
        Check.That(game.PlayerPlaying.Id).IsEqualTo(4); // 0 -> 3 (counter-clockwise)

        game.NextTurn();
        Check.That(game.PlayerPlaying.Id).IsEqualTo(3); // 3 -> 2

        game.NextTurn();
        Check.That(game.PlayerPlaying.Id).IsEqualTo(2); // 2 -> 1

        game.NextTurn();
        Check.That(game.PlayerPlaying.Id).IsEqualTo(1); // 1 -> 0
    }

    [Fact]
    public void DrawMode_ForcesDrawUntilPlayableOrBoneyardEmptyBeforeAllowingPass()
    {
        List<GamePlayer> players = CreatePlayers(2);
        Game game = new(1, players, GameMode.UnRanked, TableStyle.DRAW, MatchFormat.CUT_THROAT, seed: 7);

        // Drain stack and control exact hands & boneyard
        while (!game.Stack.IsEmpty()) game.Stack.Draw();

        players[0].AddDominoToHand(new Domino(6, 6));
        players[0].AddDominoToHand(new Domino(0, 1));
        players[1].AddDominoToHand(new Domino(1, 2)); // cannot match 6|6
        game.Stack.Add(new Domino(3, 4)); // unplayable draw #1
        game.Stack.Add(new Domino(2, 5)); // unplayable draw #2

        game.PlaceFirstDomino(); // Player 1 plays [6|6]
        game.NextTurn();         // Now Player 2's turn (no moves matching 6)

        long p2Id = players[1].Id;
        Check.That(game.PlayerPlaying.Id).IsEqualTo(p2Id);
        Check.That(game.CanDraw(p2Id)).IsTrue();
        Check.That(game.CanPass(p2Id)).IsFalse();
        Check.That(game.PassTurn(p2Id)).IsFalse(); // Passing MUST be rejected while boneyard is non-empty!

        // Draw #1: stays on Player 2's turn
        Domino? draw1 = game.Draw(p2Id);
        Check.That(draw1).IsNotNull();
        Check.That(game.PlayerPlaying.Id).IsEqualTo(p2Id);
        Check.That(game.CanPass(p2Id)).IsFalse();

        // Draw #2: empties boneyard, still unplayable -> now PASS is allowed
        Domino? draw2 = game.Draw(p2Id);
        Check.That(draw2).IsNotNull();
        Check.That(game.Stack.IsEmpty()).IsTrue();
        Check.That(game.CanDraw(p2Id)).IsFalse();
        Check.That(game.CanPass(p2Id)).IsTrue();
        Check.That(game.PassTurn(p2Id)).IsTrue();
        Check.That(game.PlayerPlaying.Id).IsEqualTo(players[0].Id);
    }

    [Fact]
    public void BlockMode_AllowsImmediatePassWhenNoValidMoveAndForbidsDrawEvenWithLockedPile()
    {
        List<GamePlayer> players = CreatePlayers(2);
        Game game = new(1, players, GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.TWO_BEST, seed: 9);

        // Put 14 tiles in locked pile and controlled hands
        game.Stack.LockRemainingPile();
        players[0].AddDominoToHand(new Domino(6, 6));
        players[0].AddDominoToHand(new Domino(0, 1));
        players[1].AddDominoToHand(new Domino(2, 3));

        game.PlaceFirstDomino();
        game.NextTurn();

        long p2Id = players[1].Id;
        Check.That(game.CanDraw(p2Id)).IsFalse();
        Check.That(game.Draw(p2Id)).IsNull();
        Check.That(game.CanPass(p2Id)).IsTrue();
        Check.That(game.PassTurn(p2Id)).IsTrue();
        Check.That(game.ConsecutivePasses).IsEqualTo(1);
    }

    // ==================== 4. OPENING POSE RESOLUTION ====================

    [Fact]
    public void OpeningPose_Hand1_AssignsDoubleSixHolder()
    {
        List<GamePlayer> players = CreatePlayers(2);
        Game game = new(1, players, GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.TWO_BEST);
        players[0].AddDominoToHand(new Domino(5, 5));
        players[1].AddDominoToHand(new Domino(6, 6));

        (GamePlayer poser, Domino tile) = game.ResolveOpeningPoser();
        Check.That(poser.Id).IsEqualTo(players[1].Id);
        Check.That(tile.Side1).IsEqualTo(6);
        Check.That(tile.Side2).IsEqualTo(6);
    }

    [Fact]
    public void OpeningPose_Fallback_HighestDoubleDownToDoubleBlank()
    {
        List<GamePlayer> players = CreatePlayers(2);
        Game game = new(1, players, GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.TWO_BEST);
        players[0].AddDominoToHand(new Domino(5, 6)); // sum 11, not a double
        players[1].AddDominoToHand(new Domino(0, 0)); // lowest double still beats non-double

        (GamePlayer poser, Domino tile) = game.ResolveOpeningPoser();
        Check.That(poser.Id).IsEqualTo(players[1].Id);
        Check.That(tile.Side1).IsEqualTo(0);
        Check.That(tile.Side2).IsEqualTo(0);
    }

    [Fact]
    public void OpeningPose_Fallback_NoDoubles_HighestSumWithHighestSingleEndTieBreaker()
    {
        List<GamePlayer> players = CreatePlayers(2);
        Game game = new(1, players, GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.TWO_BEST);
        players[0].AddDominoToHand(new Domino(3, 5)); // sum = 8, maxEnd = 5
        players[1].AddDominoToHand(new Domino(2, 6)); // sum = 8, maxEnd = 6 (wins tie-breaker!)

        (GamePlayer poser, Domino tile) = game.ResolveOpeningPoser();
        Check.That(poser.Id).IsEqualTo(players[1].Id);
        Check.That(tile.Side1).IsEqualTo(2);
        Check.That(tile.Side2).IsEqualTo(6);
    }

    // ==================== 5. HAND-END & BLOCKED BOARD RESOLUTION ====================

    [Fact]
    public void BlockedBoard_2v2_UsesIndividualLowestPipNotCombinedPartnerTotal()
    {
        List<GamePlayer> players = CreatePlayers(4);
        Game game = new(1, players, GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.SIX_LOV, isTeam2v2: true);
        while (!game.Stack.IsEmpty()) game.Stack.Draw();

        // Team 0 = Players 0 & 2: Player 0 has [0|3] (3 pips), Player 2 has [5|6] + [4|5] (20 pips) -> combined = 23
        // Team 1 = Players 1 & 3: Player 1 has [2|4] (6 pips), Player 3 has [3|4] (7 pips) -> combined = 13
        // Individual lowest is Player 0 (3 pips) -> Team 0 MUST win (NOT Team 1)!
        players[0].AddDominoToHand(new Domino(0, 0)); // opening pose tile -> board ends 0|0
        players[0].AddDominoToHand(new Domino(1, 2)); // 3 pips (doesn't match 0)
        players[1].AddDominoToHand(new Domino(2, 4)); // 6 pips
        players[2].AddDominoToHand(new Domino(5, 6)); // 11 pips
        players[2].AddDominoToHand(new Domino(4, 5)); // +9 = 20 pips
        players[3].AddDominoToHand(new Domino(3, 4)); // 7 pips

        game.PlaceFirstDomino(); // Player 0 plays [0|0], ends are 0 and 0, nobody can match 0
        game.NextTurn();
        for (int i = 0; i < 4; i++)
        {
            Check.That(game.PassTurn(game.PlayerPlaying.Id)).IsTrue();
        }

        HandResolution res = game.ResolveHandEnd();
        Check.That(res.IsOver).IsTrue();
        Check.That(res.WasBlocked).IsTrue();
        Check.That(res.IsDraw).IsFalse();
        Check.That(res.Winner!.Id).IsEqualTo(players[0].Id);
        Check.That(res.WinningTeamId).IsEqualTo(0);
    }

    [Fact]
    public void BlockedBoard_OpposingTie_DeclaresDrawWithZeroPointsNoBreakAndSamePoserNextHand()
    {
        List<GamePlayer> players = CreatePlayers(2);
        Game game = new(1, players, GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.TWO_BEST);
        while (!game.Stack.IsEmpty()) game.Stack.Draw();

        // Player 0 poses [0|0], leaving [2|3] (5 pips); Player 1 holds [1|4] (5 pips) -> opposing tie!
        players[0].AddDominoToHand(new Domino(0, 0));
        players[0].AddDominoToHand(new Domino(2, 3)); // 5 pips
        players[1].AddDominoToHand(new Domino(1, 4)); // 5 pips

        game.PlaceFirstDomino(); // Poser is players[0] (Id = 1)
        long originalPoserId = game.CurrentHandPoserId!.Value;
        game.NextTurn();
        Check.That(game.PassTurn(game.PlayerPlaying.Id)).IsTrue();
        Check.That(game.PassTurn(game.PlayerPlaying.Id)).IsTrue();

        Check.That(game.CheckEndGame()).IsTrue();
        HandResolution res = game.LastHandResolution!;
        Check.That(res.IsOver).IsTrue();
        Check.That(res.WasBlocked).IsTrue();
        Check.That(res.IsDraw).IsTrue();
        Check.That(res.Winner).IsNull();
        Check.That(res.PointsAwarded).IsEqualTo(0);
        Check.That(res.BreakTriggered).IsFalse();
        Check.That(res.NextPoserId).IsEqualTo(originalPoserId);

        // Next hand after Draw: same Poser must pose even if opponent holds [6|6]!
        game.Stack.Reset(seed: 55);
        game.DistributeDominoes(); // Hand 2
        players[0].Hand.Clear();
        players[1].Hand.Clear();
        players[0].AddDominoToHand(new Domino(1, 2));
        players[1].AddDominoToHand(new Domino(6, 6));

        (GamePlayer nextPoser, _) = game.ResolveOpeningPoser();
        Check.That(nextPoser.Id).IsEqualTo(originalPoserId);
    }

    [Fact]
    public void BreakReset_ForcesDoubleSixPoserOnNextHand()
    {
        List<GamePlayer> players = CreatePlayers(2);
        Game game = new(1, players, GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.SIX_LOV);
        while (!game.Stack.IsEmpty()) game.Stack.Draw();

        // Hand 1: Player 0 wins via Domino Out
        players[0].AddDominoToHand(new Domino(6, 6));
        players[1].AddDominoToHand(new Domino(1, 2));
        game.PlaceFirstDomino(); // Player 0 now has 0 tiles -> Domino Out
        Check.That(game.CheckEndGame()).IsTrue();
        Check.That(game.LastHandWinnerId).IsEqualTo(players[0].Id);

        // Trigger Break reset -> Hand 2 MUST assign [6|6] holder (Player 1), NOT previous winner (Player 0)
        game.TriggerBreakReset();
        game.Stack.Reset(seed: 77);
        game.DistributeDominoes();
        players[0].Hand.Clear();
        players[1].Hand.Clear();
        players[0].AddDominoToHand(new Domino(5, 5));
        players[1].AddDominoToHand(new Domino(6, 6));

        (GamePlayer nextPoser, Domino tile) = game.ResolveOpeningPoser();
        Check.That(nextPoser.Id).IsEqualTo(players[1].Id);
        Check.That(tile.Side1).IsEqualTo(6);
        Check.That(tile.Side2).IsEqualTo(6);
    }
}
