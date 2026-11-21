using Application.Interfaces;
using Application.Interfaces.Managers;
using Application.Messages.Commands.User;

using Domain;
using Domain.Game;

using DominoCommon.com.domino.common.Scripts.Enums;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;

using Infrastructure.Services;
using Infrastructure.Services.Managers;

namespace UnitTests;

public class RoatanDisconnectRejoinTests
{
    private static List<GamePlayer> CreatePlayers(int count)
    {
        List<GamePlayer> players = [];
        for (int i = 1; i <= count; i++)
        {
            players.Add(new GamePlayer(new User(string.Empty, $"Player{i}"))
            {
                Id = i,
                Score = 1000
            });
        }
        return players;
    }

    [Fact]
    public void SocketDrop_MarksSeatDisconnectedWith40sGraceWindow_WithoutAbortingMatch()
    {
        List<GamePlayer> players = CreatePlayers(4);
        Game game = new(501, players, GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.SIX_LOV, seed: 42);
        game.DistributeDominoes();
        game.PlaceFirstDomino();

        string token = players[0].ReconnectToken;
        Assert.False(string.IsNullOrWhiteSpace(token));

        bool marked = game.MarkPlayerDisconnected(players[0].Id);
        Assert.True(marked);
        Assert.Equal(4, game.Players.Count); // Seat NOT removed
        Assert.False(game.IsMatchOver);       // Match NOT aborted
        Assert.True(players[0].IsDisconnected);
        Assert.False(players[0].IsPermanentlyBot);
        Assert.Equal(40.0, players[0].DisconnectGraceRemainingSeconds);

        // Rejoinable within grace window
        Assert.True(game.CanPlayerRejoin(players[0].Id, token, out int remSec));
        Assert.Equal(40, remSec);
    }

    [Fact]
    public void DisconnectedSeat_StillPlaysViaTurnTimeoutAndBotTakeover_ZeroFreeze()
    {
        List<GamePlayer> players = CreatePlayers(2);
        Game game = new(502, players, GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.TWO_BEST, seed: 10);
        game.DistributeDominoes();

        players[0].Hand.Clear();
        players[1].Hand.Clear();
        players[0].Hand.Add(new Domino(6, 6));
        players[0].Hand.Add(new Domino(5, 4));
        players[1].Hand.Add(new Domino(6, 5));
        players[1].Hand.Add(new Domino(4, 3));

        game.PlaceFirstDomino(); // Player 1 poses [6|6]
        game.NextTurn();         // Player 2 turn

        // Player 2 drops internet during their turn
        Assert.True(game.MarkPlayerDisconnected(players[1].Id));
        Assert.True(players[1].IsDisconnected);

        // 10s turn timer expires -> server auto-plays for disconnected seat without freezing table
        var autoResult = game.ExecuteTimeoutAutoPlay(players[1].Id);
        Assert.True(autoResult.PlayedTile);
        Assert.Equal(players[0].Id, game.PlayerPlaying.Id); // Turn smoothly advanced to Player 1!
        Assert.Equal(1, players[1].ConsecutiveAutoPlays);
    }

    [Fact]
    public void RejoinWithin40Seconds_RestoresControl_ClearsDisconnectedAndBotFlags_AndBuildsFullSnapshot()
    {
        List<GamePlayer> players = CreatePlayers(4);
        Game game = new(503, players, GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.SIX_LOV, seed: 77, oneOnePlayoff: true)
        {
            LobbyId = 900,
            MatchTitle = "West End Six-Lov"
        };
        game.DistributeDominoes();
        game.PlaceFirstDomino();

        GamePlayer target = players[0];
        string token = target.ReconnectToken;

        // Player drops and takes bot control while away for 25 seconds
        game.MarkPlayerDisconnected(target.Id);
        target.IsBotControlled = true;
        target.ConsecutiveAutoPlays = 3;

        var tick = game.TickDisconnectGraceTimers(25.0);
        Assert.Empty(tick.PermanentlyConvertedPlayerIds);
        Assert.False(tick.ShouldCloseRoom);

        // CheckActiveMatch sees 15s remaining
        Assert.True(game.CanPlayerRejoin(target.Id, token, out int remSec));
        Assert.Equal(15, remSec);

        // Wrong token rejected
        Assert.False(game.CanPlayerRejoin(target.Id, "bad-token", out _));
        Assert.False(game.RejoinPlayer(target.Id, "bad-token"));

        // Valid RejoinMatch clears Disconnected & IsBotControlled & strikes
        Assert.True(game.RejoinPlayer(target.Id, token));
        Assert.False(target.IsDisconnected);
        Assert.False(target.IsBotControlled);
        Assert.Equal(0, target.ConsecutiveAutoPlays);

        // Full live board snapshot includes board tiles, hand, format, table style, scores, current turn
        var snapshotResp = DtoBuilder.GetRejoinMatchSnapshotDto(game, target, Guid.NewGuid());
        Assert.True(snapshotResp.OkResult);
        RejoinMatchSnapshotDto snap = snapshotResp.Object;
        Assert.Equal(503, snap.MatchId);
        Assert.Equal(900, snap.LobbyId);
        Assert.Equal(MatchFormat.SIX_LOV, snap.MatchFormat);
        Assert.Equal(TableStyle.BLOCK, snap.TableStyle);
        Assert.True(snap.OneOnePlayoff);
        Assert.NotEmpty(snap.BoardMoves);
        Assert.Equal(target.Hand.Count, snap.Hand.Count);
        Assert.Equal(game.PlayerPlaying.Id, snap.CurrentTurnPlayerId);
        Assert.Equal(4, snap.Players.Count);
    }

    [Fact]
    public void GraceTimerExpiry_PermanentlyConvertsSeatToBot_AndClosesRoomWhenAllHumansTimeout()
    {
        List<GamePlayer> players = CreatePlayers(2);
        Game game = new(504, players, GameMode.UnRanked, TableStyle.BLOCK, MatchFormat.TWO_BEST, seed: 88);
        game.DistributeDominoes();
        game.PlaceFirstDomino();

        string p1Token = players[0].ReconnectToken;
        string p2Token = players[1].ReconnectToken;

        // Player 1 disconnects -> 40s passes -> permanently converted to bot, room stays open (Player 2 still human)
        game.MarkPlayerDisconnected(players[0].Id);
        var tick1 = game.TickDisconnectGraceTimers(40.0);
        Assert.Contains(players[0].Id, tick1.PermanentlyConvertedPlayerIds);
        Assert.False(tick1.ShouldCloseRoom);
        Assert.True(players[0].IsPermanentlyBot);
        Assert.True(players[0].IsBotControlled);
        Assert.False(game.CanPlayerRejoin(players[0].Id, p1Token, out _));
        Assert.False(game.ReclaimManualControl(players[0].Id)); // Permanent bot cannot be reclaimed

        // Now Player 2 also disconnects and times out -> all human seats timed out -> room should close!
        game.MarkPlayerDisconnected(players[1].Id);
        var tick2 = game.TickDisconnectGraceTimers(40.0);
        Assert.Contains(players[1].Id, tick2.PermanentlyConvertedPlayerIds);
        Assert.True(tick2.ShouldCloseRoom);
        Assert.True(game.AreAllHumanSeatsTimedOut());
        Assert.False(game.CanPlayerRejoin(players[1].Id, p2Token, out _));
    }

    [Fact]
    public void AbandonDisconnectedSeat_ImmediatelyConvertsSeatToPermanentBot()
    {
        List<GamePlayer> players = CreatePlayers(2);
        Game game = new(505, players, GameMode.UnRanked, TableStyle.DRAW, MatchFormat.CUT_THROAT, seed: 99);
        game.DistributeDominoes();
        game.PlaceFirstDomino();

        string token = players[0].ReconnectToken;
        game.MarkPlayerDisconnected(players[0].Id);

        Assert.True(game.AbandonDisconnectedSeat(players[0].Id, token));
        Assert.True(players[0].IsPermanentlyBot);
        Assert.True(players[0].IsBotControlled);
        Assert.False(game.CanPlayerRejoin(players[0].Id, token, out _));
        Assert.False(game.AreAllHumanSeatsTimedOut()); // Player 2 still active
    }
}
