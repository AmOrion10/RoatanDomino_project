using Domain;
using Domain.Game;

using DominoCommon.com.domino.common.Scripts.Enums;

namespace UnitTests;

public class RoatanResilienceTests
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

    private static HandResolution ForceHandWin(Game game, int winnerIndex, int? seed = null)
    {
        if (game.HandNumber == 0)
        {
            game.DistributeDominoes();
        }
        else
        {
            game.PrepareNextHand(seed);
        }

        foreach (GamePlayer p in game.Players)
        {
            p.Hand.Clear();
        }

        if (game.IsBreakResetPending || game.HandNumber == 1)
        {
            game.Players[winnerIndex].Hand.Add(new Domino(6, 6));
            for (int i = 0; i < game.Players.Count; i++)
            {
                if (i != winnerIndex)
                {
                    game.Players[i].Hand.Add(new Domino(1, 2));
                }
            }
            game.PlaceFirstDomino();
        }
        else
        {
            for (int i = 0; i < game.Players.Count; i++)
            {
                game.Players[i].Hand.Add(new Domino(3, 3));
                if (i != winnerIndex)
                {
                    game.Players[i].Hand.Add(new Domino(1, 2));
                }
            }
            game.PlaceFirstDomino();
            game.Players[winnerIndex].Hand.Clear();
            for (int i = 0; i < game.Players.Count; i++)
            {
                if (i != winnerIndex && game.Players[i].Hand.Count == 0)
                {
                    game.Players[i].Hand.Add(new Domino(1, 2));
                }
            }
        }

        Assert.True(game.CheckEndGame());
        return game.LastHandResolution!;
    }

    [Fact]
    public void NormalHandEnd_ShowsNextRoundAndLeaveOnly()
    {
        List<GamePlayer> players = CreatePlayers(4);
        Game game = new(1001, players, GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.SIX_LOV, seed: 10);

        HandResolution res = ForceHandWin(game, winnerIndex: 0);

        Assert.False(res.BreakTriggered);
        Assert.False(res.MatchOver);
        Assert.Equal([PostHandAction.NextRound, PostHandAction.Leave], res.AvailablePostHandActions);
        Assert.DoesNotContain(PostHandAction.Rematch, res.AvailablePostHandActions);
        Assert.DoesNotContain(PostHandAction.NewMatch, res.AvailablePostHandActions);
    }

    [Fact]
    public void BreakHandEnd_InSixLov_ShowsRematchNewMatchLeave_NoNextRound_AndRematchResetsToSixSix()
    {
        List<GamePlayer> players = CreatePlayers(4);
        Game game = new(1002, players, GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.SIX_LOV, seed: 20, oneOnePlayoff: false);

        ForceHandWin(game, winnerIndex: 0); // Team 0 -> 1-0
        game.ConfirmPoserChoice(players[0].Id, PoserChoice.POSE_MYSELF);

        HandResolution breakRes = ForceHandWin(game, winnerIndex: 1); // Team 1 breaks!

        Assert.True(breakRes.BreakTriggered);
        Assert.DoesNotContain(PostHandAction.NextRound, breakRes.AvailablePostHandActions);
        Assert.Equal([PostHandAction.Rematch, PostHandAction.NewMatch, PostHandAction.Leave], breakRes.AvailablePostHandActions);

        // Rematch immediately resets scores to 0 and starts a new [6|6] opening hand with the same players
        game.StartRematchAfterBreak(seed: 99);
        Assert.Equal(0, game.TeamScores[0]);
        Assert.Equal(0, game.TeamScores[1]);
        Assert.Equal(MatchPhase.PLAYING, game.Phase);
        Assert.Equal(6, game.LastGameMove.Domino.Side1);
        Assert.Equal(6, game.LastGameMove.Domino.Side2);
        Assert.Equal(4, game.Players.Count);
    }

    [Fact]
    public void BreakHandEnd_InCutThroat_ShowsRematchNewMatchLeave_AndNoNextRound()
    {
        List<GamePlayer> players = CreatePlayers(3);
        Game game = new(1003, players, GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.CUT_THROAT, seed: 30);

        ForceHandWin(game, 0);
        ForceHandWin(game, 1);
        HandResolution breakRes = ForceHandWin(game, 2); // 1-1-1 -> Cut-Throat Break!

        Assert.True(breakRes.BreakTriggered);
        Assert.DoesNotContain(PostHandAction.NextRound, breakRes.AvailablePostHandActions);
        Assert.Equal([PostHandAction.Rematch, PostHandAction.NewMatch, PostHandAction.Leave], breakRes.AvailablePostHandActions);
    }

    [Fact]
    public void AutoPassTimer_Is5Seconds_AdvancesTurn_AndDoesNotIncrementAfkCounter()
    {
        List<GamePlayer> players = CreatePlayers(2);
        Game game = new(1004, players, GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.TWO_BEST, seed: 40);
        game.DistributeDominoes();

        players[0].Hand.Clear();
        players[1].Hand.Clear();
        players[0].Hand.Add(new Domino(6, 6));
        players[0].Hand.Add(new Domino(6, 5));
        players[1].Hand.Add(new Domino(1, 2)); // Cannot match 6|6!
        players[1].Hand.Add(new Domino(3, 4));

        game.PlaceFirstDomino(); // Player 1 poses [6|6]
        game.NextTurn();         // Now Player 2's turn with 0 playable tiles in BLOCK mode

        GamePlayer blockedPlayer = game.PlayerPlaying;
        Assert.Equal(players[1].Id, blockedPlayer.Id);
        Assert.True(game.IsCurrentPlayerAutoPassTurn());
        Assert.Equal(5.0, game.GetActiveTurnTimeoutSeconds());

        bool passed = game.ExecuteAutoPassOnTimeout(blockedPlayer.Id);
        Assert.True(passed);
        Assert.Equal(0, blockedPlayer.ConsecutiveAutoPlays);
        Assert.False(blockedPlayer.IsBotControlled);
        Assert.Equal(players[0].Id, game.PlayerPlaying.Id);
    }

    [Fact]
    public void TenSecondTurnTimer_AutoDrawsAndPlays_IncrementsStrikes_AndManualPlayResetsToZero()
    {
        List<GamePlayer> players = CreatePlayers(2);
        Game game = new(1005, players, GameMode.UnRanked, TableStyle.DRAW, MatchFormat.TWO_BEST, seed: 50);
        game.DistributeDominoes();

        players[0].Hand.Clear();
        players[1].Hand.Clear();
        players[0].Hand.Add(new Domino(6, 6));
        players[0].Hand.Add(new Domino(6, 0));
        players[0].Hand.Add(new Domino(0, 1));
        players[1].Hand.Add(new Domino(1, 2)); // No match initially, must draw in DRAW mode!

        // Put a guaranteed matching tile [6|4] in stack
        while (!game.Stack.IsEmpty()) game.Stack.Draw();
        game.Stack.Add(new Domino(6, 4));
        game.Stack.Add(new Domino(2, 3));

        game.PlaceFirstDomino(); // Player 1 plays [6|6]
        game.NextTurn();         // Player 2 has to draw from boneyard -> 10s playable action timer!

        GamePlayer p2 = game.PlayerPlaying;
        Assert.Equal(players[1].Id, p2.Id);
        Assert.False(game.IsCurrentPlayerAutoPassTurn());
        Assert.Equal(10.0, game.GetActiveTurnTimeoutSeconds());

        var result = game.ExecuteTimeoutAutoPlay(p2.Id);
        Assert.True(result.PlayedTile);
        Assert.NotEmpty(result.DrawnDominoes);
        Assert.Equal(1, p2.ConsecutiveAutoPlays);
        Assert.False(p2.IsBotControlled);

        // Now Player 1 manually plays -> resets strikes to 0 (verify by pre-setting strikes to 2)
        players[0].ConsecutiveAutoPlays = 2;
        List<GameMove> p1Moves = game.GetPlayerPossibleMoves(players[0]);
        Assert.NotEmpty(p1Moves);
        Assert.True(game.PlaceDomino(p1Moves[0]));
        Assert.Equal(0, players[0].ConsecutiveAutoPlays);
    }

    [Fact]
    public void ThreeConsecutiveTimeouts_TriggersBotTakeover_AndReclaimControlRestoresManualPlay()
    {
        List<GamePlayer> players = CreatePlayers(2);
        Game game = new(1006, players, GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.TWO_BEST, seed: 60);
        game.DistributeDominoes();

        players[0].Hand.Clear();
        players[1].Hand.Clear();
        players[0].Hand.Add(new Domino(6, 6));
        players[0].Hand.Add(new Domino(5, 4));
        players[0].Hand.Add(new Domino(4, 3));
        players[0].Hand.Add(new Domino(3, 2));
        players[0].Hand.Add(new Domino(2, 1));
        players[0].Hand.Add(new Domino(1, 0));

        players[1].Hand.Add(new Domino(6, 5));
        players[1].Hand.Add(new Domino(6, 4));
        players[1].Hand.Add(new Domino(6, 3));
        players[1].Hand.Add(new Domino(6, 2));
        players[1].Hand.Add(new Domino(6, 1));
        players[1].Hand.Add(new Domino(6, 0));

        game.PlaceFirstDomino(); // Player 1 plays [6|6]
        game.NextTurn();         // Player 2 turn

        // 3 consecutive timeouts for Player 2
        for (int strike = 1; strike <= 3; strike++)
        {
            Assert.Equal(players[1].Id, game.PlayerPlaying.Id);
            var autoRes = game.ExecuteTimeoutAutoPlay(players[1].Id);
            Assert.True(autoRes.PlayedTile);
            Assert.Equal(strike, players[1].ConsecutiveAutoPlays);
            Assert.Equal(strike >= 3, players[1].IsBotControlled);
            Assert.Equal(strike >= 3, autoRes.TriggeredBotTakeover);

            // Player 1 plays manually to pass turn back to Player 2
            List<GameMove> p1Moves = game.GetPlayerPossibleMoves(players[0]);
            if (p1Moves.Count > 0)
            {
                Assert.True(game.PlaceDomino(p1Moves[0]));
                game.NextTurn();
            }
            else
            {
                game.PassTurn(players[0].Id);
            }
        }

        // Bot takeover executes turns cleanly while keeping IsBotControlled == true
        Assert.True(players[1].IsBotControlled);
        var botTurn = game.ExecuteBotTakeoverTurn(players[1].Id);
        Assert.True(botTurn.PlayedTile || botTurn.Passed);
        Assert.True(players[1].IsBotControlled);

        // Instant manual reclaim resets IsBotControlled = false and ConsecutiveAutoPlays = 0
        Assert.True(game.ReclaimManualControl(players[1].Id));
        Assert.False(players[1].IsBotControlled);
        Assert.Equal(0, players[1].ConsecutiveAutoPlays);
    }
}
