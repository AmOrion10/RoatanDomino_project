using Domain;
using Domain.Game;

using DominoCommon.com.domino.common.Scripts.Enums;

namespace UnitTests;

public class RoatanKeyCapicuTests
{
    private static List<GamePlayer> CreatePlayers(int count)
    {
        List<GamePlayer> list = [];
        for (int i = 1; i <= count; i++)
        {
            list.Add(new GamePlayer(new User(string.Empty, $"Player{i}"))
            {
                Id = i,
                Score = 1000
            });
        }
        return list;
    }

    private static void EmptyStack(Game game)
    {
        game.Stack.UnlockPile();
        while (!game.Stack.IsEmpty())
        {
            game.Stack.Draw();
        }
    }

    private static void PlayValidTile(Game game, GamePlayer player, int s1, int s2)
    {
        List<GameMove> moves = game.GetPlayerPossibleMoves(player);
        GameMove? chosen = moves.FirstOrDefault(m =>
            (m.Domino.Side1 == s1 && m.Domino.Side2 == s2) ||
            (m.Domino.Side1 == s2 && m.Domino.Side2 == s1));
        Assert.NotNull(chosen);
        Assert.True(game.PlaceDomino(chosen!));
        game.NextTurn();
    }

    [Fact]
    public void ValidKeyCapicu_AwardsPlus2Wins_WhenNonDoubleMatchesBothOpenEnds_AndBothSuitsExhausted()
    {
        List<GamePlayer> players = CreatePlayers(2);
        Game game = new(701, players, GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.CUT_THROAT, seed: 10);
        game.DistributeDominoes();
        EmptyStack(game);

        // Setup:
        // Player 1 poses [6|6], then Player 2 plays [6|5], Player 1 plays [6|4] -> open ends are {5, 4}.
        // Player 2's final tile is [5|4] (non-double, matches both open ends {5, 4}).
        // Remaining tiles off-board: Player 1 holds [1|2] (no 5s and no 4s anywhere off-board!).
        players[0].Hand.Clear();
        players[1].Hand.Clear();
        players[0].Hand.Add(new Domino(6, 6));
        players[0].Hand.Add(new Domino(6, 4));
        players[0].Hand.Add(new Domino(1, 2)); // No 4 or 5!

        players[1].Hand.Add(new Domino(6, 5));
        players[1].Hand.Add(new Domino(5, 4)); // Final winning Key tile!

        game.PlaceFirstDomino(); // P1 plays [6|6]
        game.NextTurn();
        PlayValidTile(game, players[1], 6, 5); // Open ends: {6, 5}
        PlayValidTile(game, players[0], 6, 4); // Open ends: {4, 5}
        PlayValidTile(game, players[1], 5, 4); // P2 goes out on [5|4] -> NATURAL KEY!

        Assert.True(game.CheckEndGame());
        HandResolution res = game.LastHandResolution!;
        Assert.True(res.IsKeyWin);
        Assert.Equal(2, res.PointsAwarded);
        Assert.Equal(2, game.PlayerScores[players[1].Id]);
        Assert.Equal(2, players[1].MatchScore);
    }

    [Fact]
    public void NonExhaustedSuit_ReturnsPlus1Win_AndIsKeyWinFalse()
    {
        List<GamePlayer> players = CreatePlayers(2);
        Game game = new(702, players, GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.CUT_THROAT, seed: 11);
        game.DistributeDominoes();
        EmptyStack(game);

        // Same board setup ({4, 5} open ends, P2 goes out on [5|4]),
        // EXCEPT P1 still holds [4|1] (suit 4 is NOT exhausted!)
        players[0].Hand.Clear();
        players[1].Hand.Clear();
        players[0].Hand.Add(new Domino(6, 6));
        players[0].Hand.Add(new Domino(6, 4));
        players[0].Hand.Add(new Domino(4, 1)); // Suit 4 still off-board!

        players[1].Hand.Add(new Domino(6, 5));
        players[1].Hand.Add(new Domino(5, 4));

        game.PlaceFirstDomino();
        game.NextTurn();
        PlayValidTile(game, players[1], 6, 5);
        PlayValidTile(game, players[0], 6, 4);
        PlayValidTile(game, players[1], 5, 4);

        Assert.True(game.CheckEndGame());
        HandResolution res = game.LastHandResolution!;
        Assert.False(res.IsKeyWin);
        Assert.Equal(1, res.PointsAwarded);
        Assert.Equal(1, game.PlayerScores[players[1].Id]);
    }

    [Fact]
    public void NonExhaustedSuitInLocked1v1Pile_ReturnsPlus1Win_AndIsKeyWinFalse()
    {
        List<GamePlayer> players = CreatePlayers(2);
        Game game = new(703, players, GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.CUT_THROAT, seed: 12);
        game.DistributeDominoes();
        EmptyStack(game);
        game.Stack.Add(new Domino(5, 0)); // Suit 5 sitting in locked 1v1 pile!
        game.Stack.LockRemainingPile();

        players[0].Hand.Clear();
        players[1].Hand.Clear();
        players[0].Hand.Add(new Domino(6, 6));
        players[0].Hand.Add(new Domino(6, 4));
        players[0].Hand.Add(new Domino(1, 2));

        players[1].Hand.Add(new Domino(6, 5));
        players[1].Hand.Add(new Domino(5, 4));

        game.PlaceFirstDomino();
        game.NextTurn();
        PlayValidTile(game, players[1], 6, 5);
        PlayValidTile(game, players[0], 6, 4);
        PlayValidTile(game, players[1], 5, 4);

        Assert.True(game.CheckEndGame());
        HandResolution res = game.LastHandResolution!;
        Assert.False(res.IsKeyWin);
        Assert.Equal(1, res.PointsAwarded);
    }

    [Fact]
    public void DoubleExclusion_GoingOutOnDouble_NeverCountsAsKey()
    {
        List<GamePlayer> players = CreatePlayers(2);
        Game game = new(704, players, GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.CUT_THROAT, seed: 13);
        game.DistributeDominoes();
        EmptyStack(game);

        // Open ends are {4, 4} and all 4s are exhausted, but final tile is double [4|4] -> NEVER a Key!
        players[0].Hand.Clear();
        players[1].Hand.Clear();
        players[0].Hand.Add(new Domino(6, 6));
        players[0].Hand.Add(new Domino(6, 4));
        players[0].Hand.Add(new Domino(1, 2));

        players[1].Hand.Add(new Domino(6, 4));
        players[1].Hand.Add(new Domino(4, 4)); // Double!

        game.PlaceFirstDomino();
        game.NextTurn();
        PlayValidTile(game, players[1], 6, 4); // Open ends: {6, 4}
        PlayValidTile(game, players[0], 6, 4); // Open ends: {4, 4}
        PlayValidTile(game, players[1], 4, 4); // Goes out on double [4|4]

        Assert.True(game.CheckEndGame());
        HandResolution res = game.LastHandResolution!;
        Assert.False(res.IsKeyWin);
        Assert.Equal(1, res.PointsAwarded);
        Assert.Equal(1, game.PlayerScores[players[1].Id]);
    }

    [Fact]
    public void SixLov_At5To0_KeyClampsTo6_WinsMatch_AndPreservesIsKeyWinTrue()
    {
        List<GamePlayer> players = CreatePlayers(4);
        Game game = new(705, players, GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.SIX_LOV, seed: 14);

        // Set Team 0 to 5-0
        game.TeamScores[0] = 5;
        game.TeamScores[1] = 0;

        game.DistributeDominoes();
        EmptyStack(game);

        // Counter-clockwise turn order: index 0 -> 3 -> 2 -> 1 -> 0
        // Seat 0 (Team 0) poses [6|6], Seat 3 plays [6|5], Seat 2 (Team 0 partner) plays [6|4],
        // Seat 1 passes, Seat 0 goes out on [5|4] with all 4s & 5s exhausted!
        foreach (GamePlayer p in players) p.Hand.Clear();
        players[0].Hand.Add(new Domino(6, 6));
        players[0].Hand.Add(new Domino(5, 4)); // Final Key tile for Team 0!
        players[3].Hand.Add(new Domino(6, 5));
        players[3].Hand.Add(new Domino(1, 1));
        players[2].Hand.Add(new Domino(6, 4));
        players[2].Hand.Add(new Domino(2, 2));
        players[1].Hand.Add(new Domino(0, 1)); // Cannot play on {4, 5} -> passes!

        game.PlaceFirstDomino(); // Seat 0 plays [6|6]
        game.NextTurn();         // Seat 3
        PlayValidTile(game, players[3], 6, 5); // Open ends: {6, 5} -> Seat 2
        PlayValidTile(game, players[2], 6, 4); // Open ends: {4, 5} -> Seat 1
        Assert.True(game.PassTurn(players[1].Id)); // Seat 1 passes -> Seat 0
        PlayValidTile(game, players[0], 5, 4); // Seat 0 goes out on Key [5|4]!

        Assert.True(game.CheckEndGame());
        HandResolution res = game.LastHandResolution!;
        Assert.True(res.IsKeyWin);
        Assert.Equal(2, res.PointsAwarded);
        Assert.Equal(6, game.TeamScores[0]); // 5 + 2 clamped cleanly to 6!
        Assert.Equal(0, game.TeamScores[1]);
        Assert.True(res.MatchOver);
        Assert.Equal(0, res.MatchWinningTeamId);
        Assert.Equal(MatchPhase.MATCH_OVER, game.Phase);
    }

    [Fact]
    public void TwoBest_KeyOnHand1_ImmediatelyWinsMatch2To0()
    {
        List<GamePlayer> players = CreatePlayers(2);
        Game game = new(706, players, GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.TWO_BEST, seed: 15);
        game.DistributeDominoes();
        EmptyStack(game);

        players[0].Hand.Clear();
        players[1].Hand.Clear();
        players[0].Hand.Add(new Domino(6, 6));
        players[0].Hand.Add(new Domino(6, 3));
        players[0].Hand.Add(new Domino(1, 2));

        players[1].Hand.Add(new Domino(6, 5));
        players[1].Hand.Add(new Domino(5, 3)); // Key!

        game.PlaceFirstDomino();
        game.NextTurn();
        PlayValidTile(game, players[1], 6, 5);
        PlayValidTile(game, players[0], 6, 3);
        PlayValidTile(game, players[1], 5, 3);

        Assert.True(game.CheckEndGame());
        HandResolution res = game.LastHandResolution!;
        Assert.True(res.IsKeyWin);
        Assert.Equal(2, res.PointsAwarded);
        Assert.Equal(2, game.PlayerScores[players[1].Id]);
        Assert.True(res.MatchOver);
        Assert.Equal(players[1].Id, res.MatchWinner!.Id);
    }

    [Fact]
    public void ThreePlayerBlock_Suit0QualifiesForKey_EvenWithDoubleBlankRemoved()
    {
        List<GamePlayer> players = CreatePlayers(3);
        Game game = new(707, players, GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.CUT_THROAT, seed: 16);
        game.DistributeDominoes(); // Automatically removes [0|0] from 27-tile deck!
        EmptyStack(game);

        // Counter-clockwise 3P: 0 -> 2 -> 1 -> 0
        // Seat 0 poses [6|6], Seat 2 plays [6|0], Seat 1 plays [6|5], Seat 0 goes out on [5|0]
        // with no remaining 0-tiles or 5-tiles off-board ([0|0] was removed at deal time).
        foreach (GamePlayer p in players) p.Hand.Clear();
        players[0].Hand.Add(new Domino(6, 6));
        players[0].Hand.Add(new Domino(5, 0)); // Key with suit 0!
        players[2].Hand.Add(new Domino(6, 0));
        players[2].Hand.Add(new Domino(1, 2));
        players[1].Hand.Add(new Domino(6, 5));
        players[1].Hand.Add(new Domino(3, 4));

        game.PlaceFirstDomino(); // Seat 0 -> [6|6]
        game.NextTurn();         // Seat 2
        PlayValidTile(game, players[2], 6, 0); // Open ends: {6, 0} -> Seat 1
        PlayValidTile(game, players[1], 6, 5); // Open ends: {5, 0} -> Seat 0
        PlayValidTile(game, players[0], 5, 0); // Seat 0 goes out on [5|0] -> KEY!

        Assert.True(game.CheckEndGame());
        HandResolution res = game.LastHandResolution!;
        Assert.True(res.IsKeyWin);
        Assert.Equal(2, res.PointsAwarded);
        Assert.Equal(2, game.PlayerScores[players[0].Id]);
    }
}
