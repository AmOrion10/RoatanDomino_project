using Application.Common;
using Application.Exceptions.Lobby;
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
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Teams;
using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame;

using Infrastructure.Database;
using Infrastructure.Database.Entities;
using Infrastructure.Services;
using Infrastructure.Services.FrameHandlers;
using Infrastructure.Services.Managers;
using Infrastructure.Services.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTests;

public class RoatanTeamChallengesAndLeaderboardTests
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

    private sealed class RecordingClient(int id) : IClient
    {
        public int Id { get; } = id;
        public bool IsLoggedIn { get; set; } = true;
        public User? User { get; set; }
        public List<MessageFrame> SentFrames { get; } = [];

        public bool IsConnected() => true;
        public void CloseConnection() { }
        public Task<NetworkFrame> ReceiveAsync() => Task.FromResult(new NetworkFrame([]));

        public Task SendAsync(NetworkFrame frame)
        {
            if (frame.Frames is not null)
            {
                SentFrames.AddRange(frame.Frames);
            }
            return Task.CompletedTask;
        }
    }

    private static int nextClientId = 100;

    private static ApplicationDbContext CreateInMemoryDb(string dbName)
    {
        DbContextOptions<ApplicationDbContext> opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"{dbName}-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(opts);
    }

    private static LobbyManager CreateLobbyManager(out ChatManager chatManager)
    {
        chatManager = new ChatManager();
        return new LobbyManager(new TimerManager(), new CodeGenerator(new RandomGenerator()), chatManager);
    }

    private static async Task<UserEntity> SeedUserAsync(
        ApplicationDbContext db,
        string login,
        int elo = 1000)
    {
        UserEntity entity = new()
        {
            Login = login,
            Email = $"{login.ToLowerInvariant()}@roatan.test",
            Password = "Password1!",
            Birthdate = new DateOnly(1995, 5, 1),
            DateSignup = DateTime.UtcNow,
            Elo = elo,
            OnlineMatchesLast7Days = 5,
            LastOnlineMatchUtc = DateTime.UtcNow
        };
        db.Users.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    private static RecordingClient ConnectUser(ClientManagerMock clientManager, UserEntity entity)
    {
        RecordingClient client = new(Interlocked.Increment(ref nextClientId))
        {
            IsLoggedIn = true,
            User = new User(entity.Email, entity.Login)
            {
                Id = entity.Id,
                Elo = entity.Elo
            }
        };
        clientManager.HandleNewClient(client);
        return client;
    }

    private static async Task<(long TeamId, RecordingClient Captain, RecordingClient Vice, RecordingClient Teammate)> CreateCrewAsync(
        ApplicationDbContext db,
        TeamService teamService,
        ClientManagerMock clientManager,
        string teamName,
        string tag,
        string prefix,
        int initialTeamElo = 1000)
    {
        UserEntity capUser = await SeedUserAsync(db, $"{prefix}Cap");
        UserEntity viceUser = await SeedUserAsync(db, $"{prefix}Vice");
        UserEntity mateUser = await SeedUserAsync(db, $"{prefix}Mate");

        RecordingClient capClient = ConnectUser(clientManager, capUser);
        RecordingClient viceClient = ConnectUser(clientManager, viceUser);
        RecordingClient mateClient = ConnectUser(clientManager, mateUser);

        TeamActionResult<MyTeamDetailsDto> created = await teamService.CreateTeamAsync(capUser.Id, new CreateTeamDto
        {
            Name = teamName,
            Tag = tag,
            JoinPolicy = TeamJoinPolicy.Open,
            MinElo = 100,
            ActivityRequirement = TeamActivityRequirement.Any
        });
        Assert.True(created.Ok);
        long teamId = created.Data!.TeamInfo!.Id;

        Assert.True((await teamService.JoinOrRequestTeamAsync(viceUser.Id, teamId)).Ok);
        Assert.True((await teamService.JoinOrRequestTeamAsync(mateUser.Id, teamId)).Ok);
        Assert.True((await teamService.PromoteOrDemoteMemberAsync(capUser.Id, viceUser.Id, TeamRole.ViceCaptain)).Ok);

        if (initialTeamElo != RoatanRankTierHelper.DefaultElo)
        {
            TeamEntity? teamEntity = await db.Teams.FirstOrDefaultAsync(t => t.Id == teamId);
            Assert.NotNull(teamEntity);
            teamEntity!.TeamElo = initialTeamElo;
            await db.SaveChangesAsync();
        }

        return (teamId, capClient, viceClient, mateClient);
    }

    [Fact]
    public async Task SendTeamChallenge_EnforcesRolePermissions_CreatesTable_AndNotifiesOnlyTargetLeaders()
    {
        using ApplicationDbContext db = CreateInMemoryDb(nameof(SendTeamChallenge_EnforcesRolePermissions_CreatesTable_AndNotifiesOnlyTargetLeaders));
        ClientManagerMock clientManager = new();
        LobbyManager lobbyManager = CreateLobbyManager(out _);
        TeamService teamService = new(db, clientManager, lobbyManager);

        (long teamAId, RecordingClient capA, RecordingClient viceA, RecordingClient mateA) =
            await CreateCrewAsync(db, teamService, clientManager, "West End Crew", "WEST", "A");
        (long teamBId, RecordingClient capB, RecordingClient viceB, RecordingClient mateB) =
            await CreateCrewAsync(db, teamService, clientManager, "Coxen Hole Kings", "COXN", "B");

        // Teammate is strictly blocked from challenging another crew
        TeamActionResult<TeamChallengeActionResponseDto> mateAttempt = await teamService.SendTeamChallengeAsync(
            mateA,
            new SendTeamChallengeDto { TargetTeamId = teamBId, GameMode = MatchFormat.SIX_LOV, Variant = TableStyle.DRAW });
        Assert.False(mateAttempt.Ok);
        Assert.Contains("Captain or Vice-Captain", mateAttempt.ErrorMessage);

        // Cannot challenge your own team
        TeamActionResult<TeamChallengeActionResponseDto> selfAttempt = await teamService.SendTeamChallengeAsync(
            capA,
            new SendTeamChallengeDto { TargetTeamId = teamAId, GameMode = MatchFormat.SIX_LOV, Variant = TableStyle.DRAW });
        Assert.False(selfAttempt.Ok);
        Assert.Contains("own team", selfAttempt.ErrorMessage);

        // Vice-Captain of Team A sends a Six-Lov 2v2 Block challenge to Team B
        TeamActionResult<TeamChallengeActionResponseDto> sent = await teamService.SendTeamChallengeAsync(
            viceA,
            new SendTeamChallengeDto { TargetTeamId = teamBId, GameMode = MatchFormat.SIX_LOV, Variant = TableStyle.BLOCK });

        Assert.True(sent.Ok);
        Assert.NotNull(sent.Data?.Challenge);
        Assert.NotNull(sent.Data?.Table);
        Assert.Equal(TeamChallengeStatus.Pending, sent.Data!.Challenge.Status);
        Assert.Equal(MatchFormat.SIX_LOV, sent.Data.Challenge.GameMode);
        Assert.Equal(TableStyle.BLOCK, sent.Data.Challenge.Variant);
        Assert.True(sent.Data.Table!.IsTeamChallenge);
        Assert.Equal(teamAId, sent.Data.Table.TeamAId);
        Assert.Equal(teamBId, sent.Data.Table.TeamBId);
        Assert.Equal(4, sent.Data.Table.MaxNumberOfPlayer);
        Assert.True(lobbyManager.PlayerIsInLobby(viceA, sent.Data.Table.Id));

        // Challenger seated on Seat 0 (Team A North/South)
        LobbySeatDto? seat0 = sent.Data.Table.Seats?.FirstOrDefault(s => s.SeatIndex == 0);
        Assert.NotNull(seat0);
        Assert.True(seat0!.IsOccupied);
        Assert.Equal(viceA.User!.Id, seat0.Player!.Id);

        // Target Captain & Vice-Captain received TeamChallengeNotification, Teammate did NOT
        Assert.Contains(capB.SentFrames, f => f.CommandType == FrameCommandType.TeamChallengeNotification);
        Assert.Contains(viceB.SentFrames, f => f.CommandType == FrameCommandType.TeamChallengeNotification);
        Assert.DoesNotContain(mateB.SentFrames, f => f.CommandType == FrameCommandType.TeamChallengeNotification);
    }

    [Fact]
    public async Task RespondTeamChallenge_EnforcesTargetLeaderRole_AcceptSeatsTeamB_AndDeclineRemovesTable()
    {
        using ApplicationDbContext db = CreateInMemoryDb(nameof(RespondTeamChallenge_EnforcesTargetLeaderRole_AcceptSeatsTeamB_AndDeclineRemovesTable));
        ClientManagerMock clientManager = new();
        LobbyManager lobbyManager = CreateLobbyManager(out _);
        TeamService teamService = new(db, clientManager, lobbyManager);

        (_, RecordingClient capA, RecordingClient viceA, RecordingClient mateA) =
            await CreateCrewAsync(db, teamService, clientManager, "Sandy Bay Crew", "SAND", "C");
        (long teamBId, RecordingClient capB, RecordingClient viceB, RecordingClient mateB) =
            await CreateCrewAsync(db, teamService, clientManager, "French Harbour", "FRNH", "D");

        // 1. Send challenge and verify unauthorized actors cannot accept/decline
        TeamActionResult<TeamChallengeActionResponseDto> sent = await teamService.SendTeamChallengeAsync(
            capA,
            new SendTeamChallengeDto { TargetTeamId = teamBId, GameMode = MatchFormat.SIX_LOV, Variant = TableStyle.DRAW });
        Assert.True(sent.Ok);
        long challengeId = sent.Data!.Challenge.ChallengeId;
        int tableId = sent.Data.Table!.Id;

        // Challenger Captain cannot accept their own challenge
        TeamActionResult<TeamChallengeActionResponseDto> wrongTeamAccept = await teamService.RespondTeamChallengeAsync(
            capA, new RespondTeamChallengeDto { ChallengeId = challengeId, Accept = true });
        Assert.False(wrongTeamAccept.Ok);

        // Target Teammate is strictly blocked from accepting
        TeamActionResult<TeamChallengeActionResponseDto> mateAccept = await teamService.RespondTeamChallengeAsync(
            mateB, new RespondTeamChallengeDto { ChallengeId = challengeId, Accept = true });
        Assert.False(mateAccept.Ok);
        Assert.Contains("Captain or Vice-Captain", mateAccept.ErrorMessage);

        // Target Vice-Captain accepts -> auto-seated into Seat 1 (Team B)
        capA.SentFrames.Clear();
        mateA.SentFrames.Clear();
        mateB.SentFrames.Clear();

        TeamActionResult<TeamChallengeActionResponseDto> accepted = await teamService.RespondTeamChallengeAsync(
            viceB, new RespondTeamChallengeDto { ChallengeId = challengeId, Accept = true });
        Assert.True(accepted.Ok);
        Assert.Equal(TeamChallengeStatus.Accepted, accepted.Data!.Challenge.Status);
        Assert.NotNull(accepted.Data.Table);
        Assert.True(lobbyManager.PlayerIsInLobby(viceB, tableId));

        LobbySeatDto? seat1 = accepted.Data.Table!.Seats?.FirstOrDefault(s => s.SeatIndex == 1);
        Assert.NotNull(seat1);
        Assert.True(seat1!.IsOccupied);
        Assert.Equal(viceB.User!.Id, seat1.Player!.Id);

        // Both teams' online members receive Accepted notification so teammates can join open seats
        Assert.Contains(mateA.SentFrames, f => f.CommandType == FrameCommandType.TeamChallengeNotification);
        Assert.Contains(mateB.SentFrames, f => f.CommandType == FrameCommandType.TeamChallengeNotification);

        // 2. Test Decline flow on a second challenge (1v1 Two-Best)
        lobbyManager.RemovePlayerFromLobby(capA, tableId);
        lobbyManager.RemovePlayerFromLobby(viceB, tableId);

        TeamActionResult<TeamChallengeActionResponseDto> sent2 = await teamService.SendTeamChallengeAsync(
            viceA,
            new SendTeamChallengeDto { TargetTeamId = teamBId, GameMode = MatchFormat.TWO_BEST, Variant = TableStyle.BLOCK });
        Assert.True(sent2.Ok);
        int table2Id = sent2.Data!.Table!.Id;
        Assert.Equal(2, sent2.Data.Table.MaxNumberOfPlayer);

        TeamActionResult<TeamChallengeActionResponseDto> declined = await teamService.RespondTeamChallengeAsync(
            capB, new RespondTeamChallengeDto { ChallengeId = sent2.Data.Challenge.ChallengeId, Accept = false });
        Assert.True(declined.Ok);
        Assert.Equal(TeamChallengeStatus.Declined, declined.Data!.Challenge.Status);
        Assert.Null(lobbyManager.GetLobbyById(table2Id));
        Assert.False(lobbyManager.PlayerIsAlreadyInALobby(viceA));
    }

    [Fact]
    public async Task TeamChallengeAutoSeating_PlacesTeamAOnNorthSouth_TeamBOnEastWest_AndRejectsOutsiders()
    {
        using ApplicationDbContext db = CreateInMemoryDb(nameof(TeamChallengeAutoSeating_PlacesTeamAOnNorthSouth_TeamBOnEastWest_AndRejectsOutsiders));
        ClientManagerMock clientManager = new();
        LobbyManager lobbyManager = CreateLobbyManager(out _);
        TeamService teamService = new(db, clientManager, lobbyManager);

        (long teamAId, RecordingClient capA, RecordingClient viceA, RecordingClient mateA) =
            await CreateCrewAsync(db, teamService, clientManager, "Oak Ridge", "OAKR", "E");
        (long teamBId, RecordingClient capB, _, RecordingClient mateB) =
            await CreateCrewAsync(db, teamService, clientManager, "Punta Gorda", "PUNT", "F");
        UserEntity outsiderUser = await SeedUserAsync(db, "SoloOutsider");
        RecordingClient outsider = ConnectUser(clientManager, outsiderUser);

        // Team A Captain challenges Team B in Six-Lov 2v2
        TeamActionResult<TeamChallengeActionResponseDto> sent = await teamService.SendTeamChallengeAsync(
            capA, new SendTeamChallengeDto { TargetTeamId = teamBId, GameMode = MatchFormat.SIX_LOV, Variant = TableStyle.DRAW });
        Assert.True(sent.Ok);
        int tableId = sent.Data!.Table!.Id;

        // Team B Captain accepts -> Seat 1
        TeamActionResult<TeamChallengeActionResponseDto> accepted = await teamService.RespondTeamChallengeAsync(
            capB, new RespondTeamChallengeDto { ChallengeId = sent.Data.Challenge.ChallengeId, Accept = true });
        Assert.True(accepted.Ok);

        // Outsider tries to join -> rejected cleanly
        Assert.Throws<MaxNumberOfPlayerReachedException>(() =>
            lobbyManager.AddPlayerToTeamChallengeLobby(outsider, tableId, null));

        // Team A Teammate joins -> automatically placed on Seat 2 (North/South partner of Seat 0!)
        int mateASeat = lobbyManager.AddPlayerToTeamChallengeLobby(mateA, tableId, teamAId);
        Assert.Equal(2, mateASeat);

        // 3rd member of Team A (Vice-Captain) tries to join -> Team A's 2 seats are full, rejected cleanly
        Assert.Throws<MaxNumberOfPlayerReachedException>(() =>
            lobbyManager.AddPlayerToTeamChallengeLobby(viceA, tableId, teamAId));

        // Team B Teammate joins -> automatically placed on Seat 3 (East/West partner of Seat 1!)
        int mateBSeat = lobbyManager.AddPlayerToTeamChallengeLobby(mateB, tableId, teamBId);
        Assert.Equal(3, mateBSeat);

        Lobby lobby = lobbyManager.GetLobbyById(tableId)!;
        Assert.Equal(capA.User!.Id, lobby.GetPlayerAtSeat(0)!.Id);
        Assert.Equal(capB.User!.Id, lobby.GetPlayerAtSeat(1)!.Id);
        Assert.Equal(mateA.User!.Id, lobby.GetPlayerAtSeat(2)!.Id);
        Assert.Equal(mateB.User!.Id, lobby.GetPlayerAtSeat(3)!.Id);
    }

    [Fact]
    public async Task TeamEloCalculator_AndGameEndingIntegration_UpdatesTeamEloWinsLossesAndBreakdown()
    {
        // 1. Pure formula checks (K=32, +6 Key/Capicu or Game Bruk bonus, 100 Elo floor)
        TeamEloBreakdownDto standardWin = RoatanEloCalculator.CalculateTeamChallengeElo(new TeamChallengeEloInput
        {
            ChallengeId = 1,
            TeamAId = 10, TeamAName = "Alpha", TeamATag = "ALPH", TeamACurrentElo = 1000,
            TeamBId = 20, TeamBName = "Beta", TeamBTag = "BETA", TeamBCurrentElo = 1000,
            WinningTeamSlot = 0,
            IsKeyWin = false,
            IsGameBrukWin = false
        });
        Assert.Equal(1016, standardWin.TeamA.NewElo);
        Assert.Equal(16, standardWin.TeamA.Delta);
        Assert.Equal(984, standardWin.TeamB.NewElo);
        Assert.Equal(-16, standardWin.TeamB.Delta);

        TeamEloBreakdownDto keyWin = RoatanEloCalculator.CalculateTeamChallengeElo(new TeamChallengeEloInput
        {
            ChallengeId = 2,
            TeamAId = 10, TeamAName = "Alpha", TeamATag = "ALPH", TeamACurrentElo = 1000,
            TeamBId = 20, TeamBName = "Beta", TeamBTag = "BETA", TeamBCurrentElo = 1000,
            WinningTeamSlot = 1,
            IsKeyWin = true
        });
        Assert.Equal(1022, keyWin.TeamB.NewElo);
        Assert.Equal(6, keyWin.TeamB.SpecialWinBonus);
        Assert.Equal(984, keyWin.TeamA.NewElo);

        TeamEloBreakdownDto floorCheck = RoatanEloCalculator.CalculateTeamChallengeElo(new TeamChallengeEloInput
        {
            ChallengeId = 3,
            TeamAId = 10, TeamAName = "Alpha", TeamATag = "ALPH", TeamACurrentElo = 105,
            TeamBId = 20, TeamBName = "Beta", TeamBTag = "BETA", TeamBCurrentElo = 110,
            WinningTeamSlot = 1,
            IsGameBrukWin = true
        });
        Assert.Equal(100, floorCheck.TeamA.NewElo);
        Assert.Equal(RoatanRankTier.Cobbler, floorCheck.TeamA.NewTier);
        Assert.Equal(6, floorCheck.TeamB.SpecialWinBonus);

        // 2. Full GameEndingCommand integration with TeamService (ITeamChallengeEloUpdater)
        using ApplicationDbContext db = CreateInMemoryDb(nameof(TeamEloCalculator_AndGameEndingIntegration_UpdatesTeamEloWinsLossesAndBreakdown));
        ClientManagerMock clientManager = new();
        LobbyManager lobbyManager = CreateLobbyManager(out ChatManager chatManager);
        TeamService teamService = new(db, clientManager, lobbyManager);

        (long teamAId, RecordingClient capA, _, _) =
            await CreateCrewAsync(db, teamService, clientManager, "Reef Sharks", "REEF", "G", initialTeamElo: 1000);
        (long teamBId, RecordingClient capB, _, _) =
            await CreateCrewAsync(db, teamService, clientManager, "Coral Kings", "CORL", "H", initialTeamElo: 1000);

        TeamActionResult<TeamChallengeActionResponseDto> sent = await teamService.SendTeamChallengeAsync(
            capA, new SendTeamChallengeDto { TargetTeamId = teamBId, GameMode = MatchFormat.TWO_BEST, Variant = TableStyle.DRAW });
        Assert.True(sent.Ok);
        long challengeId = sent.Data!.Challenge.ChallengeId;

        Assert.True((await teamService.RespondTeamChallengeAsync(
            capB, new RespondTeamChallengeDto { ChallengeId = challengeId, Accept = true })).Ok);

        GameManager gameManager = new(chatManager);
        Lobby challengeLobby = lobbyManager.GetLobbyById(sent.Data.Table!.Id)!;
        gameManager.CreateGameFromLobby(challengeLobby, seed: 42);
        Game game = gameManager.GetById(challengeLobby.Id)!;
        Assert.True(game.IsTeamChallenge);
        Assert.Equal(challengeId, game.TeamChallengeId);
        Assert.Equal(teamAId, game.TeamAId);
        Assert.Equal(teamBId, game.TeamBId);

        game.DistributeDominoes();
        game.PlaceFirstDomino();

        // Simulate Team A (Seat 0 / capA) winning the Two-Best match cleanly!
        GamePlayer gpA = game.Players[0];
        gpA.Hand.Clear();
        game.PlayerScores[gpA.Id] = 1;
        gpA.MatchScore = 1;
        Assert.True(game.CheckEndGame());
        Assert.True(game.IsMatchOver);

        UserRepository userRepo = new(db, new NationalityRepository(db));
        InlineGameClientsDispatcher dispatcher = new([capA, capB]);
        GameEndingCommandHandler handler = new(gameManager, chatManager, dispatcher, userRepo, teamService);

        Game endedGame = await handler.Handle(new GameEndingCommand(game.Id), CancellationToken.None);

        Assert.NotNull(endedGame.LastMatchEloBreakdown?.TeamBreakdown);
        Assert.Equal(1016, endedGame.LastMatchEloBreakdown!.TeamBreakdown!.TeamA.NewElo);
        Assert.Equal(984, endedGame.LastMatchEloBreakdown.TeamBreakdown.TeamB.NewElo);

        EndGameDto endDto = DtoBuilder.GetEndGameDto(true, endedGame);
        Assert.NotNull(endDto.EloBreakdown?.TeamBreakdown);
        Assert.Equal(1016, endDto.EloBreakdown!.TeamBreakdown!.TeamA.NewElo);

        TeamEntity updatedA = await db.Teams.FirstAsync(t => t.Id == teamAId);
        TeamEntity updatedB = await db.Teams.FirstAsync(t => t.Id == teamBId);
        TeamChallengeEntity completedCh = await db.TeamChallenges.FirstAsync(c => c.Id == challengeId);

        Assert.Equal(1016, updatedA.TeamElo);
        Assert.Equal(1, updatedA.TeamWins);
        Assert.Equal(0, updatedA.TeamLosses);
        Assert.Equal(984, updatedB.TeamElo);
        Assert.Equal(0, updatedB.TeamWins);
        Assert.Equal(1, updatedB.TeamLosses);
        Assert.Equal(TeamChallengeStatus.Completed, completedCh.Status);
    }

    [Fact]
    public async Task TeamLeaderboard_SortsByTeamElo_PinsMyTeam_AndTeamFrameHandlersWorkEndToEnd()
    {
        using ApplicationDbContext db = CreateInMemoryDb(nameof(TeamLeaderboard_SortsByTeamElo_PinsMyTeam_AndTeamFrameHandlersWorkEndToEnd));
        ClientManagerMock clientManager = new();
        LobbyManager lobbyManager = CreateLobbyManager(out _);
        TeamService teamService = new(db, clientManager, lobbyManager);

        (long t1Id, RecordingClient cap1, _, _) =
            await CreateCrewAsync(db, teamService, clientManager, "Low Crew", "LOWC", "L", initialTeamElo: 850);
        (long t2Id, RecordingClient cap2, _, _) =
            await CreateCrewAsync(db, teamService, clientManager, "Mid Crew", "MIDC", "M", initialTeamElo: 1320);
        (long t3Id, RecordingClient cap3, _, _) =
            await CreateCrewAsync(db, teamService, clientManager, "Top Legends", "TOPL", "T", initialTeamElo: 2150);

        // Request top 2 teams as Low Crew's captain -> Low Crew should still be pinned as MyTeamEntry with Rank #3!
        TeamLeaderboardDto lb = await teamService.GetTeamLeaderboardAsync(cap1.User!.Id, limit: 2);
        Assert.Equal(3, lb.TotalTeams);
        Assert.Equal(2, lb.Entries.Count);
        Assert.Equal(t3Id, lb.Entries[0].TeamId);
        Assert.Equal(1, lb.Entries[0].Rank);
        Assert.Equal(RoatanRankTier.GrandSlamLegend, lb.Entries[0].TeamRankTier);
        Assert.Equal(t2Id, lb.Entries[1].TeamId);
        Assert.Equal(2, lb.Entries[1].Rank);
        Assert.Equal(RoatanRankTier.Navigator, lb.Entries[1].TeamRankTier);

        Assert.NotNull(lb.MyTeamEntry);
        Assert.Equal(t1Id, lb.MyTeamEntry!.TeamId);
        Assert.Equal(3, lb.MyTeamEntry.Rank);
        Assert.Equal(RoatanRankTier.Deckhand, lb.MyTeamEntry.TeamRankTier);

        // End-to-end TeamFrameHandlers test for GetTeamLeaderboard, SendTeamChallenge, and RespondTeamChallenge
        MessageFrameHandler frameHandler = new(NullLogger<MessageFrameHandler>.Instance);
        TeamFrameHandlers teamFrameHandlers = new(teamService);
        teamFrameHandlers.RegisterHandlers(frameHandler);

        cap1.SentFrames.Clear();
        await frameHandler.HandleAsync(MessageFrameBuilder.GetTeamLeaderboardFrame(), cap1);
        MessageFrame? lbRespFrame = cap1.SentFrames.FirstOrDefault(f => f.CommandType == FrameCommandType.Response && f.SubCommandType == FrameCommandType.GetTeamLeaderboard);
        Assert.NotNull(lbRespFrame);
        ResponseObjectDto<TeamLeaderboardDto> lbResp = lbRespFrame!.GetObjectFromMessage<ResponseObjectDto<TeamLeaderboardDto>>();
        Assert.True(lbResp.OkResult);
        Assert.Equal(3, lbResp.Object.Entries.Count);

        cap2.SentFrames.Clear();
        await frameHandler.HandleAsync(MessageFrameBuilder.SendTeamChallengeFrame(new SendTeamChallengeDto
        {
            TargetTeamId = t3Id,
            GameMode = MatchFormat.TWO_BEST,
            Variant = TableStyle.DRAW
        }), cap2);
        MessageFrame? chSentFrame = cap2.SentFrames.FirstOrDefault(f => f.CommandType == FrameCommandType.Response && f.SubCommandType == FrameCommandType.SendTeamChallenge);
        Assert.NotNull(chSentFrame);
        ResponseObjectDto<TeamChallengeActionResponseDto> chSentResp = chSentFrame!.GetObjectFromMessage<ResponseObjectDto<TeamChallengeActionResponseDto>>();
        Assert.True(chSentResp.OkResult);
        long chId = chSentResp.Object.Challenge.ChallengeId;

        cap3.SentFrames.Clear();
        await frameHandler.HandleAsync(MessageFrameBuilder.RespondTeamChallengeFrame(new RespondTeamChallengeDto
        {
            ChallengeId = chId,
            Accept = true
        }), cap3);
        MessageFrame? chAccFrame = cap3.SentFrames.FirstOrDefault(f => f.CommandType == FrameCommandType.Response && f.SubCommandType == FrameCommandType.RespondTeamChallenge);
        Assert.NotNull(chAccFrame);
        ResponseObjectDto<TeamChallengeActionResponseDto> chAccResp = chAccFrame!.GetObjectFromMessage<ResponseObjectDto<TeamChallengeActionResponseDto>>();
        Assert.True(chAccResp.OkResult);
        Assert.Equal(TeamChallengeStatus.Accepted, chAccResp.Object.Challenge.Status);
    }
}
