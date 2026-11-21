using Domain;
using Domain.Game;
using DominoCommon.com.domino.common.Scripts.Enums;

namespace UnitTests;

public class RoatanMatchFormatsTests
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

    /// <summary>
    /// Simulates a clean Domino-Out hand win by the specified player index (0-based).
    /// </summary>
    private static HandResolution WinHandByDominoOut(Game game, int winnerIndex, bool prepareNextFirst)
    {
        if (prepareNextFirst)
        {
            game.PrepareNextHand(seed: 100 + game.HandNumber);
        }
        else
        {
            game.DistributeDominoes();
        }

        // Give the intended winner a single opening tile so posing empties their hand immediately (Domino Out!)
        foreach (GamePlayer p in game.Players) p.Hand.Clear();

        if (game.IsBreakResetPending || game.HandNumber == 1)
        {
            game.Players[winnerIndex].AddDominoToHand(new Domino(6, 6));
            for (int i = 0; i < game.Players.Count; i++)
            {
                if (i != winnerIndex)
                {
                    game.Players[i].AddDominoToHand(new Domino(1, 2));
                }
            }
            game.PlaceFirstDomino();
        }
        else
        {
            // Give poser a non-winning hand and winner a matching tile, or if winner is poser give 1 tile
            for (int i = 0; i < game.Players.Count; i++)
            {
                game.Players[i].AddDominoToHand(new Domino(3, 3));
                if (i != winnerIndex)
                {
                    game.Players[i].AddDominoToHand(new Domino(1, 2));
                }
            }
            game.PlaceFirstDomino();
            // Clear intended winner's hand to simulate Domino Out
            game.Players[winnerIndex].Hand.Clear();
            // Ensure everyone else still holds a tile
            for (int i = 0; i < game.Players.Count; i++)
            {
                if (i != winnerIndex && game.Players[i].Hand.Count == 0)
                {
                    game.Players[i].AddDominoToHand(new Domino(1, 2));
                }
            }
        }

        Check.That(game.CheckEndGame()).IsTrue();
        return game.LastHandResolution!;
    }

    // ==================== 1. SIX_LOV (2v2 Partners) ====================

    [Fact]
    public void SixLov_SixConsecutiveWins_WinsMatchSixToZero()
    {
        Game game = new(1, CreatePlayers(4), GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.SIX_LOV, seed: 10, oneOnePlayoff: false);
        Check.That(game.IsTeam2v2).IsTrue();

        for (int hand = 1; hand <= 6; hand++)
        {
            // Team 0 = Seats 0 & 2 alternating wins
            int winnerIdx = (hand % 2 == 1) ? 0 : 2;
            HandResolution res = WinHandByDominoOut(game, winnerIdx, prepareNextFirst: hand > 1);

            Check.That(res.BreakTriggered).IsFalse();
            Check.That(game.TeamScores[0]).IsEqualTo(hand);
            Check.That(game.TeamScores[1]).IsEqualTo(0);

            if (hand < 6)
            {
                Check.That(res.MatchOver).IsFalse();
                Check.That(res.RequiresChoosePoser).IsTrue();
                Check.That(game.Phase).IsEqualTo(MatchPhase.CHOOSE_POSER);
                Check.That(game.PartnerHandsHiddenDuringChoosePoser).IsTrue();
                Check.That(game.ChoosePoserRemainingSeconds).IsEqualTo(Game.ChoosePoserTimeoutSeconds);
                game.ConfirmPoserChoice(res.PoserChooserId!.Value, PoserChoice.POSE_MYSELF);
            }
            else
            {
                Check.That(res.MatchOver).IsTrue();
                Check.That(res.MatchWinningTeamId).IsEqualTo(0);
                Check.That(game.Phase).IsEqualTo(MatchPhase.MATCH_OVER);
            }
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void SixLov_StandardBreak_TrailingPairWinResetsToZeroZeroAndForcesDoubleSix(int leadingWins)
    {
        Game game = new(1, CreatePlayers(4), GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.SIX_LOV, seed: 20, oneOnePlayoff: false);

        for (int i = 0; i < leadingWins; i++)
        {
            HandResolution r = WinHandByDominoOut(game, 0, prepareNextFirst: i > 0);
            game.ConfirmPoserChoice(r.PoserChooserId!.Value, PoserChoice.POSE_MYSELF);
        }

        Check.That(game.TeamScores[0]).IsEqualTo(leadingWins);
        Check.That(game.TeamScores[1]).IsEqualTo(0);

        // Trailing Team 1 (Seat 1) wins -> Game Bruk!
        HandResolution breakRes = WinHandByDominoOut(game, 1, prepareNextFirst: true);
        Check.That(breakRes.BreakTriggered).IsTrue();
        Check.That(breakRes.RequiresChoosePoser).IsFalse();
        Check.That(game.TeamScores[0]).IsEqualTo(0);
        Check.That(game.TeamScores[1]).IsEqualTo(0);
        Check.That(game.IsBreakResetPending).IsTrue();

        // Next hand MUST require [6|6] to pose
        game.PrepareNextHand(seed: 99);
        foreach (GamePlayer p in game.Players) p.Hand.Clear();
        game.Players[0].AddDominoToHand(new Domino(5, 5));
        game.Players[1].AddDominoToHand(new Domino(4, 4));
        game.Players[2].AddDominoToHand(new Domino(6, 6)); // Seat 2 holds [6|6]
        game.Players[3].AddDominoToHand(new Domino(3, 3));

        (GamePlayer poser, Domino required) = game.ResolveOpeningPoser();
        Check.That(poser.Id).IsEqualTo(game.Players[2].Id);
        Check.That(required.Side1).IsEqualTo(6);
        Check.That(required.Side2).IsEqualTo(6);
    }

    [Fact]
    public void SixLov_OneOnePlayoff_Transitions1_0To1_1To2_0AndBreaksAt2_0()
    {
        Game game = new(1, CreatePlayers(4), GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.SIX_LOV, seed: 30, oneOnePlayoff: true);

        // 0-0 -> Team 0 wins -> 1-0
        HandResolution h1 = WinHandByDominoOut(game, 0, prepareNextFirst: false);
        Check.That(game.TeamScores[0]).IsEqualTo(1);
        Check.That(game.TeamScores[1]).IsEqualTo(0);
        Check.That(h1.BreakTriggered).IsFalse();
        game.ConfirmPoserChoice(h1.PoserChooserId!.Value, PoserChoice.POSE_MYSELF);

        // 1-0 -> Team 1 wins -> 1-1 (NO Break!)
        HandResolution h2 = WinHandByDominoOut(game, 1, prepareNextFirst: true);
        Check.That(h2.BreakTriggered).IsFalse();
        Check.That(game.TeamScores[0]).IsEqualTo(1);
        Check.That(game.TeamScores[1]).IsEqualTo(1);
        Check.That(h2.RequiresChoosePoser).IsTrue();
        game.ConfirmPoserChoice(h2.PoserChooserId!.Value, PoserChoice.PASS_TO_PADNA);

        // 1-1 -> Team 1 wins -> advances directly to 2-0!
        HandResolution h3 = WinHandByDominoOut(game, 3, prepareNextFirst: true);
        Check.That(h3.BreakTriggered).IsFalse();
        Check.That(game.TeamScores[0]).IsEqualTo(0);
        Check.That(game.TeamScores[1]).IsEqualTo(2);
        Check.That(h3.RequiresChoosePoser).IsTrue();
        game.ConfirmPoserChoice(h3.PoserChooserId!.Value, PoserChoice.POSE_MYSELF);

        // 2-0 (Team 1 leads) -> Team 0 wins -> BREAK back to 0-0!
        HandResolution h4 = WinHandByDominoOut(game, 0, prepareNextFirst: true);
        Check.That(h4.BreakTriggered).IsTrue();
        Check.That(game.TeamScores[0]).IsEqualTo(0);
        Check.That(game.TeamScores[1]).IsEqualTo(0);
        Check.That(game.IsBreakResetPending).IsTrue();
    }

    [Fact]
    public void CallYourPadna_ChoosePoser_PassToPadnaAllowsAnyTileAndTimeoutDefaultsToPoseMyself()
    {
        Game game = new(1, CreatePlayers(4), GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.SIX_LOV, seed: 40);

        // Hand 1: Seat 0 wins -> enters CHOOSE_POSER with Partner = Seat 2
        HandResolution h1 = WinHandByDominoOut(game, 0, prepareNextFirst: false);
        Check.That(game.Phase).IsEqualTo(MatchPhase.CHOOSE_POSER);
        Check.That(game.PoserChooserId).IsEqualTo(game.Players[0].Id);
        Check.That(game.PartnerCandidateId).IsEqualTo(game.Players[2].Id);
        Check.That(game.PartnerHandsHiddenDuringChoosePoser).IsTrue();

        // Wrong player cannot confirm choice
        Check.That(game.ConfirmPoserChoice(game.Players[1].Id, PoserChoice.PASS_TO_PADNA)).IsNull();

        // Seat 0 passes pose to Padna (Seat 2)
        long? confirmedPoser = game.ConfirmPoserChoice(game.Players[0].Id, PoserChoice.PASS_TO_PADNA);
        Check.That(confirmedPoser).IsEqualTo(game.Players[2].Id);
        Check.That(game.CanPoserPlayAnyTile).IsTrue();

        // Next hand: Padna (Seat 2) may pose ANY tile from their hand (e.g., [1|2], even if holding [6|6])
        game.PrepareNextHand(seed: 41);
        game.Players[2].Hand.Clear();
        game.Players[2].AddDominoToHand(new Domino(6, 6));
        game.Players[2].AddDominoToHand(new Domino(1, 2));
        Check.That(game.PlaceOpeningDomino(game.Players[2].Id, new Domino(1, 2))).IsTrue();
        Check.That(game.LastGameMove.Domino.Side1).IsEqualTo(1);
        Check.That(game.LastGameMove.Domino.Side2).IsEqualTo(2);

        // Test 8-second countdown timeout defaults to POSE_MYSELF
        HandResolution h2 = WinHandByDominoOut(game, 2, prepareNextFirst: true);
        Check.That(game.Phase).IsEqualTo(MatchPhase.CHOOSE_POSER);
        Check.That(game.TickChoosePoserTimer(5.0)).IsFalse();
        Check.That(game.ChoosePoserRemainingSeconds).IsEqualTo(3.0);
        Check.That(game.TickChoosePoserTimer(3.0)).IsTrue(); // 8s elapsed -> auto-defaults to Seat 2 (Pose Myself)
        Check.That(game.Phase).IsEqualTo(MatchPhase.AWAITING_POSE);
        Check.That(game.ConfirmedPoserId).IsEqualTo(game.Players[2].Id);
    }

    // ==================== 2. TWO_BEST (1v1 or 2v2 Quick Match) ====================

    [Fact]
    public void TwoBest_1v1_NoBreakAt1_1AndWinsAt2_1()
    {
        Game game = new(1, CreatePlayers(2), GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.TWO_BEST, seed: 50);
        Check.That(game.IsTeam2v2).IsFalse();

        // Hand 1: Player 0 wins -> 1-0
        HandResolution h1 = WinHandByDominoOut(game, 0, prepareNextFirst: false);
        Check.That(h1.BreakTriggered).IsFalse();
        Check.That(h1.MatchOver).IsFalse();
        Check.That(h1.RequiresChoosePoser).IsFalse();
        Check.That(game.PlayerScores[game.Players[0].Id]).IsEqualTo(1);

        // Hand 2: Player 1 wins -> 1-1 (NO Break!)
        HandResolution h2 = WinHandByDominoOut(game, 1, prepareNextFirst: true);
        Check.That(h2.BreakTriggered).IsFalse();
        Check.That(h2.MatchOver).IsFalse();
        Check.That(game.PlayerScores[game.Players[0].Id]).IsEqualTo(1);
        Check.That(game.PlayerScores[game.Players[1].Id]).IsEqualTo(1);

        // Hand 3: Player 1 wins -> 2-1 Match Win!
        HandResolution h3 = WinHandByDominoOut(game, 1, prepareNextFirst: true);
        Check.That(h3.MatchOver).IsTrue();
        Check.That(h3.MatchWinner!.Id).IsEqualTo(game.Players[1].Id);
        Check.That(game.Phase).IsEqualTo(MatchPhase.MATCH_OVER);
    }

    [Fact]
    public void TwoBest_2v2_UsesChoosePoserAndNoBreakAt1_1()
    {
        Game game = new(1, CreatePlayers(4), GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.TWO_BEST, seed: 60, isTeam2v2: true);
        Check.That(game.IsTeam2v2).IsTrue();

        // Hand 1: Team 0 (Seat 0) wins -> 1-0 + CHOOSE_POSER
        HandResolution h1 = WinHandByDominoOut(game, 0, prepareNextFirst: false);
        Check.That(h1.RequiresChoosePoser).IsTrue();
        Check.That(game.TeamScores[0]).IsEqualTo(1);
        game.ConfirmPoserChoice(h1.PoserChooserId!.Value, PoserChoice.PASS_TO_PADNA);

        // Hand 2: Team 1 (Seat 1) wins -> 1-1 (NO Break!) + CHOOSE_POSER
        HandResolution h2 = WinHandByDominoOut(game, 1, prepareNextFirst: true);
        Check.That(h2.BreakTriggered).IsFalse();
        Check.That(h2.RequiresChoosePoser).IsTrue();
        Check.That(game.TeamScores[0]).IsEqualTo(1);
        Check.That(game.TeamScores[1]).IsEqualTo(1);
        game.ConfirmPoserChoice(h2.PoserChooserId!.Value, PoserChoice.POSE_MYSELF);

        // Hand 3: Team 0 (Seat 2) wins -> 2-1 Match Win!
        HandResolution h3 = WinHandByDominoOut(game, 2, prepareNextFirst: true);
        Check.That(h3.MatchOver).IsTrue();
        Check.That(h3.MatchWinningTeamId).IsEqualTo(0);
    }

    // ==================== 3. CUT_THROAT (Free-For-All: 2, 3, or 4 Players) ====================

    [Fact]
    public void CutThroat_3P_AllowsNonConsecutiveWinsWhileZeroExistsAndBreaksWhenAllScore()
    {
        Game game = new(1, CreatePlayers(3), GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.CUT_THROAT, seed: 70);
        Check.That(game.IsTeam2v2).IsFalse();

        // 3-0-0 -> 3-1-0 (NO Break because Seat 2 is still at 0!)
        WinHandByDominoOut(game, 0, prepareNextFirst: false);
        WinHandByDominoOut(game, 0, prepareNextFirst: true);
        WinHandByDominoOut(game, 0, prepareNextFirst: true);
        HandResolution h4 = WinHandByDominoOut(game, 1, prepareNextFirst: true);

        Check.That(h4.BreakTriggered).IsFalse();
        Check.That(game.PlayerScores[game.Players[0].Id]).IsEqualTo(3);
        Check.That(game.PlayerScores[game.Players[1].Id]).IsEqualTo(1);
        Check.That(game.PlayerScores[game.Players[2].Id]).IsEqualTo(0);

        // Seat 2 wins -> 3-1-1 -> ALL players have >= 1 -> BREAK resets everyone to 0-0-0!
        HandResolution h5 = WinHandByDominoOut(game, 2, prepareNextFirst: true);
        Check.That(h5.BreakTriggered).IsTrue();
        Check.That(game.PlayerScores[game.Players[0].Id]).IsEqualTo(0);
        Check.That(game.PlayerScores[game.Players[1].Id]).IsEqualTo(0);
        Check.That(game.PlayerScores[game.Players[2].Id]).IsEqualTo(0);
        Check.That(game.IsBreakResetPending).IsTrue();
    }

    [Fact]
    public void CutThroat_4P_BreaksAt2_1_1_1And1v1BreaksAt1_1()
    {
        // 4P: 2-1-1-0 -> Seat 3 wins -> 2-1-1-1 -> BREAK!
        Game game4 = new(1, CreatePlayers(4), GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.CUT_THROAT, seed: 80);
        WinHandByDominoOut(game4, 0, prepareNextFirst: false);
        WinHandByDominoOut(game4, 0, prepareNextFirst: true);
        WinHandByDominoOut(game4, 1, prepareNextFirst: true);
        HandResolution beforeBreak = WinHandByDominoOut(game4, 2, prepareNextFirst: true);
        Check.That(beforeBreak.BreakTriggered).IsFalse(); // Seat 3 still at 0

        HandResolution break4 = WinHandByDominoOut(game4, 3, prepareNextFirst: true);
        Check.That(break4.BreakTriggered).IsTrue();
        Check.That(game4.PlayerScores.Values.All(v => v == 0)).IsTrue();

        // 1v1: 1-0 -> Seat 1 wins -> 1-1 -> BREAK!
        Game game2 = new(2, CreatePlayers(2), GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.CUT_THROAT, seed: 81);
        WinHandByDominoOut(game2, 0, prepareNextFirst: false);
        HandResolution break2 = WinHandByDominoOut(game2, 1, prepareNextFirst: true);
        Check.That(break2.BreakTriggered).IsTrue();
        Check.That(game2.PlayerScores.Values.All(v => v == 0)).IsTrue();
    }

    [Fact]
    public void CutThroat_FirstTo6WinsAndCutThroatsZeroPointPlayers()
    {
        Game game = new(1, CreatePlayers(4), GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.CUT_THROAT, seed: 90);

        // Seat 0 wins 5, Seat 1 wins 1 -> 5-1-0-0 (Seats 2 & 3 at 0 -> no Break)
        for (int i = 0; i < 5; i++) WinHandByDominoOut(game, 0, prepareNextFirst: i > 0);
        HandResolution h6 = WinHandByDominoOut(game, 1, prepareNextFirst: true);
        Check.That(h6.BreakTriggered).IsFalse();

        // Seat 0 wins 6th -> Match Over! Seats 2 & 3 (still at 0) are Cut-Throated!
        HandResolution winRes = WinHandByDominoOut(game, 0, prepareNextFirst: true);
        Check.That(winRes.MatchOver).IsTrue();
        Check.That(winRes.MatchWinner!.Id).IsEqualTo(game.Players[0].Id);
        Check.That(winRes.CutThroatedPlayerIds).ContainsExactly(game.Players[2].Id, game.Players[3].Id);
        Check.That(game.Phase).IsEqualTo(MatchPhase.MATCH_OVER);
    }
}
