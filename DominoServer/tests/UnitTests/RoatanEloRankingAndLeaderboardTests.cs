using Application.Common;
using Application.Interfaces;
using Application.Interfaces.Managers;
using Application.Messages.Commands.Game;
using Application.Messages.Queries.Game;

using CommonTests.Fixtures;

using Domain;
using Domain.Game;

using DominoCommon.com.domino.common.Scripts.Enums;
using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;
using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;

using Infrastructure.Database;
using Infrastructure.Database.Entities;
using Infrastructure.Services;
using Infrastructure.Services.FrameHandlers;
using Infrastructure.Services.Managers;
using Infrastructure.Services.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTests;

public class RoatanEloRankingAndLeaderboardTests
{
    private sealed class InlineGameClientsDispatcher(List<IClient> clients) : IMessageDispatcher
    {
        public Task<TResponse> Dispatch<TResponse>(ICommand<TResponse> command)
        {
            return Task.FromResult(default(TResponse))!;
        }

        public Task<TResponse> Dispatch<TResponse>(IQuery<TResponse> query)
        {
            if (query is GetAllClientsForGameQuery)
            {
                return Task.FromResult((TResponse)(object)clients);
            }
            return Task.FromResult(default(TResponse))!;
        }
    }
    private static ApplicationDbContext CreateInMemoryDb(string dbName)
    {
        DbContextOptions<ApplicationDbContext> opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(opts);
    }

    [Theory]
    [InlineData(-50, RoatanRankTier.Cobbler, "Cobbler", 100)]
    [InlineData(0, RoatanRankTier.Cobbler, "Cobbler", 100)]
    [InlineData(100, RoatanRankTier.Cobbler, "Cobbler", 100)]
    [InlineData(499, RoatanRankTier.Cobbler, "Cobbler", 499)]
    [InlineData(500, RoatanRankTier.Deckhand, "Deckhand", 500)]
    [InlineData(899, RoatanRankTier.Deckhand, "Deckhand", 899)]
    [InlineData(900, RoatanRankTier.Sailor, "Sailor", 900)]
    [InlineData(1000, RoatanRankTier.Sailor, "Sailor", 1000)]
    [InlineData(1199, RoatanRankTier.Sailor, "Sailor", 1199)]
    [InlineData(1200, RoatanRankTier.Navigator, "Navigator", 1200)]
    [InlineData(1499, RoatanRankTier.Navigator, "Navigator", 1499)]
    [InlineData(1500, RoatanRankTier.ReefMaster, "Reef Master", 1500)]
    [InlineData(1799, RoatanRankTier.ReefMaster, "Reef Master", 1799)]
    [InlineData(1800, RoatanRankTier.IslandChampion, "Island Champion", 1800)]
    [InlineData(2099, RoatanRankTier.IslandChampion, "Island Champion", 2099)]
    [InlineData(2100, RoatanRankTier.GrandSlamLegend, "Grand Slam Legend", 2100)]
    [InlineData(2650, RoatanRankTier.GrandSlamLegend, "Grand Slam Legend", 2650)]
    public void SevenTierRankMapping_MapsBoundariesDefaultsAndFloorCorrectly(
        int rawElo,
        RoatanRankTier expectedTier,
        string expectedDisplayName,
        int expectedClampedElo)
    {
        Assert.Equal(7, RoatanRankTierHelper.AllTiers.Count);
        Assert.Equal(1000, RoatanRankTierHelper.DefaultElo);
        Assert.Equal(100, RoatanRankTierHelper.MinElo);

        int clamped = RoatanRankTierHelper.ClampElo(rawElo);
        RoatanRankTier tier = RoatanRankTierHelper.FromElo(rawElo);
        RoatanRankTierInfo info = RoatanRankTierHelper.GetInfoForElo(rawElo);

        Assert.Equal(expectedClampedElo, clamped);
        Assert.Equal(expectedTier, tier);
        Assert.Equal(expectedDisplayName, info.DisplayName);
        Assert.Equal(expectedDisplayName, tier.GetDisplayName());
        Assert.StartsWith("#", info.BadgeColorHex);
        Assert.Equal($"[{expectedDisplayName}] \u2022 {expectedClampedElo}", RoatanRankTierHelper.FormatPlainBadge(rawElo));
    }

    [Fact]
    public void EloCalculator_RejectsOfflineMatchesAndSingleHumanMatches()
    {
        // Offline match with 2 humans -> unranked, 0 delta
        MatchEloBreakdownDto offlineResult = RoatanEloCalculator.CalculateMatchRatings(new EloMatchContext
        {
            GameId = 1,
            IsOnlineMatch = false,
            IsTeam2v2 = false,
            Players =
            [
                new EloPlayerMatchInput { PlayerId = 1, Username = "Alice", CurrentElo = 1000, IsHuman = true, IsWinner = true },
                new EloPlayerMatchInput { PlayerId = 2, Username = "Bob", CurrentElo = 1000, IsHuman = true, IsWinner = false }
            ]
        });

        Assert.False(offlineResult.IsRankedMatch);
        Assert.All(offlineResult.Players, p =>
        {
            Assert.False(p.IsRankedApplied);
            Assert.Equal(0, p.Delta);
            Assert.Equal(1000, p.NewElo);
        });

        // Online match with only 1 real human + 3 bots -> unranked, 0 delta
        MatchEloBreakdownDto soloVsBotsResult = RoatanEloCalculator.CalculateMatchRatings(new EloMatchContext
        {
            GameId = 2,
            IsOnlineMatch = true,
            IsTeam2v2 = true,
            Players =
            [
                new EloPlayerMatchInput { PlayerId = 1, Username = "SoloHuman", CurrentElo = 1000, IsHuman = true, TeamId = 0, IsWinner = true },
                new EloPlayerMatchInput { PlayerId = -1, Username = "Bot1", CurrentElo = 1200, IsHuman = false, TeamId = 1, IsWinner = false },
                new EloPlayerMatchInput { PlayerId = -2, Username = "Bot2", CurrentElo = 1200, IsHuman = false, TeamId = 0, IsWinner = true },
                new EloPlayerMatchInput { PlayerId = -3, Username = "Bot3", CurrentElo = 1200, IsHuman = false, TeamId = 1, IsWinner = false }
            ]
        });

        Assert.False(soloVsBotsResult.IsRankedMatch);
        Assert.Single(soloVsBotsResult.Players);
        Assert.Equal(0, soloVsBotsResult.Players[0].Delta);
        Assert.Equal(1000, soloVsBotsResult.Players[0].NewElo);
    }

    [Fact]
    public void EloCalculator_AppliesBaseChessDelta_SpecialBonuses_PerformanceModifiers_AndFloor()
    {
        // Equal Elo (1000 vs 1000), Winner wins by Key/Capicu (+6) with strong stats (+8 max modifier)
        // Loser plays poorly (trapped doubles, forced passes, slow turns -> -4 min modifier)
        MatchEloBreakdownDto keyWin = RoatanEloCalculator.CalculateMatchRatings(new EloMatchContext
        {
            GameId = 10,
            IsOnlineMatch = true,
            IsTeam2v2 = true,
            Players =
            [
                new EloPlayerMatchInput
                {
                    PlayerId = 1, Username = "Ace", CurrentElo = 1000, IsHuman = true, TeamId = 0,
                    IsWinner = true, IsKeyWin = true, WinAssists = 3, TotalMoves = 10, OptimalMoves = 9,
                    ForcedPasses = 0, TrappedDoubles = 0, AverageTurnSeconds = 2.8
                },
                new EloPlayerMatchInput
                {
                    PlayerId = 2, Username = "Padna", CurrentElo = 1000, IsHuman = true, TeamId = 0,
                    IsWinner = true, IsKeyWin = true, WinAssists = 2, TotalMoves = 8, OptimalMoves = 6,
                    ForcedPasses = 0, TrappedDoubles = 0, AverageTurnSeconds = 4.9
                },
                new EloPlayerMatchInput
                {
                    PlayerId = 3, Username = "Opp1", CurrentElo = 1000, IsHuman = true, TeamId = 1,
                    IsWinner = false, WinAssists = 0, TotalMoves = 10, OptimalMoves = 2,
                    ForcedPasses = 4, TrappedDoubles = 2, AverageTurnSeconds = 9.5
                },
                new EloPlayerMatchInput
                {
                    PlayerId = 4, Username = "Opp2", CurrentElo = 1000, IsHuman = true, TeamId = 1,
                    IsWinner = false, WinAssists = 0, TotalMoves = 10, OptimalMoves = 5,
                    ForcedPasses = 0, TrappedDoubles = 0, AverageTurnSeconds = 7.0
                }
            ]
        });

        Assert.True(keyWin.IsRankedMatch);
        PlayerEloBreakdownDto ace = keyWin.Players.First(p => p.PlayerId == 1);
        Assert.Equal(16, ace.BaseEloDelta);
        Assert.Equal(6, ace.SpecialWinBonus);
        Assert.Equal(8, ace.PerformanceModifier); // Clamped max +8
        Assert.Equal(30, ace.Delta);
        Assert.Equal(1030, ace.NewElo);
        Assert.Equal("Key / Capicu Win", ace.ResultLabel);

        PlayerEloBreakdownDto padna = keyWin.Players.First(p => p.PlayerId == 2);
        Assert.Equal(16, padna.BaseEloDelta);
        Assert.Equal(6, padna.SpecialWinBonus);
        Assert.Equal(5, padna.PerformanceModifier); // +2 assists +2 accuracy (75%) +1 speed (<6s) = +5
        Assert.Equal(27, padna.Delta);

        PlayerEloBreakdownDto opp1 = keyWin.Players.First(p => p.PlayerId == 3);
        Assert.Equal(-16, opp1.BaseEloDelta);
        Assert.Equal(0, opp1.SpecialWinBonus);
        Assert.Equal(-4, opp1.PerformanceModifier); // Clamped min -4
        Assert.Equal(-20, opp1.Delta);
        Assert.Equal(980, opp1.NewElo);

        // Game Bruk win also awards +6 bonus
        MatchEloBreakdownDto brukWin = RoatanEloCalculator.CalculateMatchRatings(new EloMatchContext
        {
            GameId = 11,
            IsOnlineMatch = true,
            IsTeam2v2 = false,
            Players =
            [
                new EloPlayerMatchInput
                {
                    PlayerId = 10, Username = "Breaker", CurrentElo = 1000, IsHuman = true,
                    IsWinner = true, IsGameBrukWin = true, TotalMoves = 6, OptimalMoves = 4, AverageTurnSeconds = 5.2
                },
                new EloPlayerMatchInput
                {
                    PlayerId = 11, Username = "LowElo", CurrentElo = 105, IsHuman = true,
                    IsWinner = false, TotalMoves = 6, OptimalMoves = 1, TrappedDoubles = 2, ForcedPasses = 3, AverageTurnSeconds = 9.5
                }
            ]
        });

        PlayerEloBreakdownDto breaker = brukWin.Players.First(p => p.PlayerId == 10);
        Assert.Equal(6, breaker.SpecialWinBonus);
        Assert.Equal("Game Bruk Win", breaker.ResultLabel);

        // Floor test: LowElo at 105 losing to a peer (110 Elo) drops below 100 before clamping -> strictly floored at 100 Elo
        MatchEloBreakdownDto floorTest = RoatanEloCalculator.CalculateMatchRatings(new EloMatchContext
        {
            GameId = 12,
            IsOnlineMatch = true,
            IsTeam2v2 = false,
            Players =
            [
                new EloPlayerMatchInput { PlayerId = 20, Username = "PeerWinner", CurrentElo = 110, IsHuman = true, IsWinner = true },
                new EloPlayerMatchInput
                {
                    PlayerId = 21, Username = "LowElo", CurrentElo = 105, IsHuman = true,
                    IsWinner = false, TotalMoves = 6, OptimalMoves = 1, TrappedDoubles = 2, ForcedPasses = 3, AverageTurnSeconds = 9.5
                }
            ]
        });

        PlayerEloBreakdownDto lowElo = floorTest.Players.First(p => p.PlayerId == 21);
        Assert.Equal(100, lowElo.NewElo);
        Assert.Equal(RoatanRankTier.Cobbler, lowElo.NewTier);
        Assert.Equal(-5, lowElo.Delta);
    }

    [Fact]
    public void PerformanceModifier_StrictlyClampedBetweenMinus4AndPlus8()
    {
        int maxMod = RoatanEloCalculator.CalculatePerformanceModifier(
            winAssists: 10, optimalMoves: 20, totalMoves: 20, forcedPasses: 0, trappedDoubles: 0, averageTurnSeconds: 1.2);
        Assert.Equal(8, maxMod);

        int minMod = RoatanEloCalculator.CalculatePerformanceModifier(
            winAssists: 0, optimalMoves: 0, totalMoves: 20, forcedPasses: 8, trappedDoubles: 5, averageTurnSeconds: 10.0);
        Assert.Equal(-4, minMod);

        int fastSpeedMod = RoatanEloCalculator.CalculatePerformanceModifier(
            winAssists: 0, optimalMoves: 0, totalMoves: 0, forcedPasses: 0, trappedDoubles: 0, averageTurnSeconds: 5.5);
        int slowSpeedMod = RoatanEloCalculator.CalculatePerformanceModifier(
            winAssists: 0, optimalMoves: 0, totalMoves: 0, forcedPasses: 0, trappedDoubles: 0, averageTurnSeconds: 9.5);
        Assert.True(fastSpeedMod > slowSpeedMod);
    }

    [Fact]
    public async Task GameEndingCommand_PersistsEloAndRankedStats_AndBuildsEndGameBreakdown()
    {
        using ApplicationDbContext db = CreateInMemoryDb($"elo-endgame-{Guid.NewGuid():N}");
        NationalityRepository natRepo = new(db);
        UserRepository userRepo = new(db, natRepo);

        User u1 = await userRepo.Create(new User("alice@roatan.com", "AliceRoatan"), "StrongPass1!", new DateOnly(1995, 5, 10));
        User u2 = await userRepo.Create(new User("bob@roatan.com", "BobRoatan"), "StrongPass1!", new DateOnly(1994, 8, 12));

        Assert.Equal(1000, u1.Elo);
        Assert.Equal(1000, u2.Elo);

        ChatManager chatManager = new();
        GameManager gameManager = new(chatManager);
        LobbyManager lobbyManager = new(new TimerManager(), new CodeGenerator(new RandomGenerator()), chatManager);

        ClientMock c1 = new() { Id = 1, IsLoggedIn = true, User = u1 };
        ClientMock c2 = new() { Id = 2, IsLoggedIn = true, User = u2 };

        Lobby lobby = lobbyManager.CreateLobby(c1, 2, true, "Ranked Island 1v1", tableStyle: TableStyle.BLOCK, matchFormat: MatchFormat.TWO_BEST);
        lobbyManager.AddPlayerToLobby(c2, lobby.Id);
        gameManager.CreateGameFromLobby(lobby, seed: 42);

        Game game = gameManager.GetById(lobby.Id)!;
        game.DistributeDominoes();
        game.PlaceFirstDomino();

        // Simulate u1 winning the match cleanly (force empty hand & win condition)
        GamePlayer gp1 = game.Players.First(p => p.Id == u1.Id);
        GamePlayer gp2 = game.Players.First(p => p.Id == u2.Id);
        gp1.Hand.Clear();
        gp1.MatchMovesCount = 7;
        gp1.MatchOptimalMovesCount = 6;
        gp1.MatchTotalTurnSeconds = 21.0;
        gp1.MatchTimedTurns = 7;

        gp2.MatchMovesCount = 6;
        gp2.MatchOptimalMovesCount = 3;
        gp2.MatchTotalTurnSeconds = 48.0;
        gp2.MatchTimedTurns = 6;

        // First hand win -> 1-0, then second hand win -> 2-0 MATCH_OVER
        game.PlayerScores[gp1.Id] = 1;
        gp1.MatchScore = 1;
        Assert.True(game.CheckEndGame());
        Assert.True(game.IsMatchOver);

        InlineGameClientsDispatcher dispatcher = new([c1, c2]);

        GameEndingCommandHandler handler = new(gameManager, chatManager, dispatcher, userRepo);
        Game ended = await handler.Handle(new GameEndingCommand(game.Id), CancellationToken.None);

        Assert.NotNull(ended.LastMatchEloBreakdown);
        Assert.True(ended.LastMatchEloBreakdown!.IsRankedMatch);
        Assert.Equal(2, ended.LastMatchEloBreakdown.Players.Count);

        EndGameDto endDto = DtoBuilder.GetEndGameDto(true, ended);
        Assert.NotNull(endDto.EloBreakdown);
        Assert.Equal(2, endDto.EloBreakdown!.Players.Count);

        // Verify persisted stats in DB
        User persisted1 = await userRepo.GetById(u1.Id);
        User persisted2 = await userRepo.GetById(u2.Id);

        Assert.True(persisted1.Elo > 1000);
        Assert.True(persisted2.Elo < 1000);
        Assert.Equal(1, persisted1.Wins);
        Assert.Equal(0, persisted1.Losses);
        Assert.Equal(7, persisted1.TotalMoves);
        Assert.Equal(6, persisted1.OptimalMoves);
        Assert.Equal(3.0, persisted1.AverageTurnSeconds);
        Assert.Equal(1, persisted1.OnlineMatchesLast7Days);
        Assert.NotNull(persisted1.LastOnlineMatchUtc);

        Assert.Equal(0, persisted2.Wins);
        Assert.Equal(1, persisted2.Losses);
        Assert.Equal(1, persisted2.OnlineMatchesLast7Days);

        // Connected client User reference is also updated in memory
        Assert.Equal(persisted1.Elo, c1.User!.Elo);
        Assert.Equal(persisted2.Elo, c2.User!.Elo);
    }

    [Fact]
    public async Task LeaderboardService_SortsByEloDescending_ExcludesBots_AndPinsLocalPlayerOutsideTopLimit()
    {
        using ApplicationDbContext db = CreateInMemoryDb($"leaderboard-{Guid.NewGuid():N}");
        db.Users.AddRange(
            new UserEntity { Id = 1, Login = "LegendOne", Email = "l1@t.com", Password = "p", Elo = 2250, Wins = 45, Losses = 5, WinAssists = 18, TotalMoves = 300, OptimalMoves = 270, Birthdate = new DateOnly(1990, 1, 1) },
            new UserEntity { Id = 2, Login = "IslandChamp", Email = "l2@t.com", Password = "p", Elo = 1920, Wins = 30, Losses = 10, WinAssists = 12, TotalMoves = 200, OptimalMoves = 160, Birthdate = new DateOnly(1991, 1, 1) },
            new UserEntity { Id = 3, Login = "SailorEqualA", Email = "l3@t.com", Password = "p", Elo = 1150, Wins = 10, Losses = 8, WinAssists = 4, TotalMoves = 100, OptimalMoves = 80, Birthdate = new DateOnly(1992, 1, 1) },
            new UserEntity { Id = 4, Login = "SailorEqualB", Email = "l4@t.com", Password = "p", Elo = 1150, Wins = 14, Losses = 6, WinAssists = 6, TotalMoves = 100, OptimalMoves = 70, Birthdate = new DateOnly(1993, 1, 1) },
            new UserEntity { Id = 5, Login = "LocalCobbler", Email = "l5@t.com", Password = "p", Elo = 420, Wins = 1, Losses = 19, WinAssists = 2, TotalMoves = 50, OptimalMoves = 20, Birthdate = new DateOnly(1994, 1, 1) }
        );
        await db.SaveChangesAsync();

        UserRepository userRepo = new(db, new NationalityRepository(db));
        LeaderboardService service = new(userRepo);

        // Request top 3 while requesting user is #5 (outside top 3) -> LocalPlayerEntry must still be pinned at Rank #5!
        LeaderboardDto board = await service.BuildLeaderboardAsync(requestingUserId: 5, limit: 3);

        Assert.Equal(5, board.TotalRankedPlayers);
        Assert.Equal(3, board.Entries.Count);

        Assert.Equal(1, board.Entries[0].Rank);
        Assert.Equal("LegendOne", board.Entries[0].Username);
        Assert.Equal(RoatanRankTier.GrandSlamLegend, board.Entries[0].Tier);
        Assert.Equal("Grand Slam Legend", board.Entries[0].TierName);
        Assert.Equal(90.0, board.Entries[0].AccuracyPercent);

        Assert.Equal(2, board.Entries[1].Rank);
        Assert.Equal("IslandChamp", board.Entries[1].Username);
        Assert.Equal(RoatanRankTier.IslandChampion, board.Entries[1].Tier);

        // Tie-breaker on equal Elo (1150): more Wins ranks higher (SailorEqualB has 14 wins vs SailorEqualA's 10 wins)
        Assert.Equal(3, board.Entries[2].Rank);
        Assert.Equal("SailorEqualB", board.Entries[2].Username);

        // Pinned local player banner shows Rank #5 accurately
        Assert.NotNull(board.LocalPlayerEntry);
        Assert.Equal(5, board.LocalPlayerEntry!.Rank);
        Assert.Equal("LocalCobbler", board.LocalPlayerEntry.Username);
        Assert.Equal(RoatanRankTier.Cobbler, board.LocalPlayerEntry.Tier);
        Assert.Equal(420, board.LocalPlayerEntry.Elo);
        Assert.Equal(40.0, board.LocalPlayerEntry.AccuracyPercent);

        // Verify ProfileFrameHandlers responds to GetLeaderboard frame
        ChatManager chatManager = new();
        LobbyManager lobbyManager = new(new TimerManager(), new CodeGenerator(new RandomGenerator()), chatManager);
        ClientManagerMock clientManager = new();
        GameManager gameManager = new(chatManager);
        PresenceService presence = new(userRepo, clientManager, lobbyManager, gameManager);
        ProfileFrameHandlers handlers = new(new MessageDispatcherMock(), userRepo, lobbyManager, presence, service);
        MessageFrameHandler frameHandler = new(NullLogger<MessageFrameHandler>.Instance);
        handlers.RegisterHandlers(frameHandler);

        ClientMock requestingClient = new()
        {
            Id = 55,
            IsLoggedIn = true,
            User = new User("l5@t.com", "LocalCobbler") { Id = 5 }
        };

        await frameHandler.HandleAsync(MessageFrameBuilder.GetLeaderboardFrame(), requestingClient);

        MessageFrame responseFrame = requestingClient.GetMessageFrameByCommandType(FrameCommandType.Response);
        Assert.Equal(FrameCommandType.GetLeaderboard, responseFrame.SubCommandType);
        ResponseObjectDto<LeaderboardDto> respDto = responseFrame.GetObjectFromMessage<ResponseObjectDto<LeaderboardDto>>();
        Assert.True(respDto.OkResult);
        Assert.Equal(5, respDto.Object.Entries.Count);
        Assert.Equal(5, respDto.Object.LocalPlayerEntry.Rank);
    }
}
