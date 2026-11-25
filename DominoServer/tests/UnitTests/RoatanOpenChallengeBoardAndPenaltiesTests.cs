using Application.Interfaces;
using Application.Interfaces.Managers;

using CommonTests.Fixtures;

using Domain;
using Domain.Game;

using DominoCommon.com.domino.common.Scripts.Enums;
using DominoCommon.com.domino.common.Scripts.Networking.Dto;
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

public class RoatanOpenChallengeBoardAndPenaltiesTests
{
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

    private static int nextClientId = 900;

    private static ApplicationDbContext CreateInMemoryDb(string name)
    {
        DbContextOptions<ApplicationDbContext> opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"{name}-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(opts);
    }

    private static LobbyManager CreateLobbyManager(out ChatManager chatManager)
    {
        chatManager = new ChatManager();
        return new LobbyManager(new TimerManager(), new CodeGenerator(new RandomGenerator()), chatManager);
    }

    private static async Task<UserEntity> SeedUserAsync(ApplicationDbContext db, string login, int elo = 1000)
    {
        UserEntity user = new()
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
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
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
        int teamElo = 1000,
        int memberElo = 1000)
    {
        UserEntity capU = await SeedUserAsync(db, $"{prefix}Cap", memberElo);
        UserEntity viceU = await SeedUserAsync(db, $"{prefix}Vice", memberElo);
        UserEntity mateU = await SeedUserAsync(db, $"{prefix}Mate", memberElo);

        RecordingClient cap = ConnectUser(clientManager, capU);
        RecordingClient vice = ConnectUser(clientManager, viceU);
        RecordingClient mate = ConnectUser(clientManager, mateU);

        TeamActionResult<MyTeamDetailsDto> created = await teamService.CreateTeamAsync(capU.Id, new CreateTeamDto
        {
            Name = teamName,
            Tag = tag,
            JoinPolicy = TeamJoinPolicy.Open,
            MinElo = 100,
            ActivityRequirement = TeamActivityRequirement.Any
        });
        Assert.True(created.Ok);
        long teamId = created.Data!.TeamInfo!.Id;

        Assert.True((await teamService.JoinOrRequestTeamAsync(viceU.Id, teamId)).Ok);
        Assert.True((await teamService.JoinOrRequestTeamAsync(mateU.Id, teamId)).Ok);
        Assert.True((await teamService.PromoteOrDemoteMemberAsync(capU.Id, viceU.Id, TeamRole.ViceCaptain)).Ok);

        TeamEntity teamRow = await db.Teams.FirstAsync(t => t.Id == teamId);
        teamRow.TeamElo = teamElo;
        await db.SaveChangesAsync();

        return (teamId, cap, vice, mate);
    }

    [Fact]
    public async Task OpenChallengeBoard_Conditions_TeammateAskLeaders_AtomicAccept_AndCancel()
    {
        using ApplicationDbContext db = CreateInMemoryDb(nameof(OpenChallengeBoard_Conditions_TeammateAskLeaders_AtomicAccept_AndCancel));
        ClientManagerMock clientManager = new();
        LobbyManager lobbyManager = CreateLobbyManager(out _);
        TeamService teamService = new(db, clientManager, lobbyManager);

        (long posterTeamId, RecordingClient capPoster, _, RecordingClient matePoster) =
            await CreateCrewAsync(db, teamService, clientManager, "Reef Raiders", "REEF", "P", teamElo: 1400);
        (_, RecordingClient capLow, _, RecordingClient mateLow) =
            await CreateCrewAsync(db, teamService, clientManager, "Rookie Sailors", "ROOK", "L", teamElo: 1000);
        (long eligible1Id, RecordingClient capElig1, RecordingClient viceElig1, RecordingClient mateElig1) =
            await CreateCrewAsync(db, teamService, clientManager, "West End Pros", "WEST", "E1", teamElo: 1350);
        (_, RecordingClient capElig2, _, _) =
            await CreateCrewAsync(db, teamService, clientManager, "Coxen Legends", "COXN", "E2", teamElo: 1500);

        // Teammate cannot post an open board challenge
        TeamActionResult<TeamChallengeDto> matePostFail = await teamService.PostOpenTeamChallengeAsync(
            matePoster,
            new PostOpenTeamChallengeDto { GameMode = MatchFormat.SIX_LOV, Variant = TableStyle.BLOCK, MinOpponentTeamElo = 1200, RequireFullHumanSquad = true });
        Assert.False(matePostFail.Ok);

        // Captain posts an Open Board Challenge requiring 1200+ Team Elo and Humans Only (No Bots)
        TeamActionResult<TeamChallengeDto> posted = await teamService.PostOpenTeamChallengeAsync(
            capPoster,
            new PostOpenTeamChallengeDto { GameMode = MatchFormat.SIX_LOV, Variant = TableStyle.BLOCK, MinOpponentTeamElo = 1200, RequireFullHumanSquad = true });
        Assert.True(posted.Ok);
        long chId = posted.Data!.ChallengeId;
        Assert.True(posted.Data.IsOpenBoardChallenge);
        Assert.Null(posted.Data.TargetTeamId);
        Assert.Equal(1200, posted.Data.MinOpponentTeamElo);
        Assert.True(posted.Data.RequireFullHumanSquad);

        // Low-Elo team sees MeetsConditions = false and cannot Ask Leaders or Accept
        OpenTeamChallengesDto boardForLow = await teamService.GetOpenTeamChallengesAsync(capLow.User!.Id);
        TeamChallengeDto chForLow = Assert.Single(boardForLow.OpenChallenges);
        Assert.False(chForLow.MeetsConditions);
        Assert.Contains("1200+ Elo", chForLow.DisqualifyReason);

        Assert.False((await teamService.RequestAcceptTeamChallengeAsync(mateLow.User!.Id, chId)).Ok);
        Assert.False((await teamService.AcceptOpenTeamChallengeAsync(capLow, chId)).Ok);

        // Eligible rival Teammate clicks "Ask Leaders" -> endorses challenge and notifies ONLY online Captain & Vice-Captain!
        capElig1.SentFrames.Clear();
        viceElig1.SentFrames.Clear();
        mateElig1.SentFrames.Clear();

        TeamActionResult<TeamChallengeDto> askRes = await teamService.RequestAcceptTeamChallengeAsync(mateElig1.User!.Id, chId);
        Assert.True(askRes.Ok);
        Assert.Contains(mateElig1.User!.Login, askRes.Data!.RequestedByTeammateUsernames);
        Assert.True(askRes.Data.AlreadyRequestedByMe);

        MessageFrame? capNotif = capElig1.SentFrames.FirstOrDefault(f => f.CommandType == FrameCommandType.TeamChallengeNotification);
        MessageFrame? viceNotif = viceElig1.SentFrames.FirstOrDefault(f => f.CommandType == FrameCommandType.TeamChallengeNotification);
        Assert.NotNull(capNotif);
        Assert.NotNull(viceNotif);
        Assert.Contains($"{mateElig1.User.Login} asked leaders to accept [REEF] Reef Raiders's challenge!", capNotif!.GetObjectFromMessage<TeamChallengeDto>().Message);
        Assert.DoesNotContain(mateElig1.SentFrames, f => f.CommandType == FrameCommandType.TeamChallengeNotification);

        // Atomic concurrency check: two eligible rival Captains race to accept the same open challenge -> strictly ONE wins!
        Task<TeamActionResult<TeamChallengeActionResponseDto>> race1 = teamService.AcceptOpenTeamChallengeAsync(capElig1, chId);
        Task<TeamActionResult<TeamChallengeActionResponseDto>> race2 = teamService.AcceptOpenTeamChallengeAsync(capElig2, chId);
        TeamActionResult<TeamChallengeActionResponseDto>[] raceResults = await Task.WhenAll(race1, race2);

        Assert.Equal(1, raceResults.Count(r => r.Ok));
        Assert.Equal(1, raceResults.Count(r => !r.Ok));

        TeamActionResult<TeamChallengeActionResponseDto> winnerRes = raceResults.First(r => r.Ok);
        Assert.Equal(TeamChallengeStatus.Accepted, winnerRes.Data!.Challenge.Status);
        Assert.NotNull(winnerRes.Data.Challenge.PlayDeadlineUtc);
        Assert.True(winnerRes.Data.Challenge.TeamBCheckedIn);
        Assert.NotNull(winnerRes.Data.Table);
        Assert.True(winnerRes.Data.Table!.RequireFullHumanSquad);

        // Verify Cancel works on a second open challenge ONLY for the posting team's leaders
        TeamActionResult<TeamChallengeDto> posted2 = await teamService.PostOpenTeamChallengeAsync(
            capPoster,
            new PostOpenTeamChallengeDto { GameMode = MatchFormat.TWO_BEST, Variant = TableStyle.DRAW, MinOpponentTeamElo = 100 });
        Assert.True(posted2.Ok);
        long ch2Id = posted2.Data!.ChallengeId;

        Assert.False((await teamService.CancelOpenTeamChallengeAsync(capElig1.User!.Id, ch2Id)).Ok);
        Assert.False((await teamService.CancelOpenTeamChallengeAsync(matePoster.User!.Id, ch2Id)).Ok);

        TeamActionResult<TeamChallengeDto> cancelled = await teamService.CancelOpenTeamChallengeAsync(capPoster.User!.Id, ch2Id);
        Assert.True(cancelled.Ok);
        Assert.Equal(TeamChallengeStatus.Cancelled, cancelled.Data!.Status);
        Assert.Equal(posterTeamId, cancelled.Data.ChallengerTeamId);
        Assert.NotEqual(eligible1Id, posterTeamId);
    }

    [Fact]
    public async Task RequireFullHumanSquad_BlocksAddTableBot_WithExactCompetitiveErrorMessage()
    {
        using ApplicationDbContext db = CreateInMemoryDb(nameof(RequireFullHumanSquad_BlocksAddTableBot_WithExactCompetitiveErrorMessage));
        ClientManagerMock clientManager = new();
        LobbyManager lobbyManager = CreateLobbyManager(out ChatManager chatManager);
        GameManager gameManager = new(chatManager);
        TeamService teamService = new(db, clientManager, lobbyManager);
        UserRepository userRepo = new(db, new NationalityRepository(db));
        PresenceService presence = new(userRepo, clientManager, lobbyManager, gameManager, db);

        (long teamAId, RecordingClient capA, _, _) =
            await CreateCrewAsync(db, teamService, clientManager, "Strict Humans", "HUMN", "H1");
        (long teamBId, RecordingClient capB, _, _) =
            await CreateCrewAsync(db, teamService, clientManager, "Island Rivals", "ISLD", "H2");

        // Post & accept a RequireFullHumanSquad = true Six-Lov 2v2 open challenge
        TeamActionResult<TeamChallengeDto> posted = await teamService.PostOpenTeamChallengeAsync(
            capA, new PostOpenTeamChallengeDto { GameMode = MatchFormat.SIX_LOV, Variant = TableStyle.DRAW, RequireFullHumanSquad = true });
        Assert.True(posted.Ok);

        TeamActionResult<TeamChallengeActionResponseDto> accepted = await teamService.AcceptOpenTeamChallengeAsync(capB, posted.Data!.ChallengeId);
        Assert.True(accepted.Ok);
        int tableId = accepted.Data!.Table!.Id;

        // LobbyManager directly refuses bot addition
        Assert.Null(lobbyManager.AddBotToLobby(capB, tableId, seatIndex: 2, difficulty: "Pro"));

        // LobbyFrameHandlers returns the exact required plain-English error message
        LobbyFrameHandlers lobbyHandlers = new(new MessageDispatcherMock(), lobbyManager, clientManager, new SeedGenerator(), presence, teamService, gameManager);
        MessageFrameHandler frameHandler = new(NullLogger<MessageFrameHandler>.Instance);
        lobbyHandlers.RegisterHandlers(frameHandler);

        capB.SentFrames.Clear();
        await frameHandler.HandleAsync(MessageFrameBuilder.AddTableBotFrame(new AddTableBotDto { SeatIndex = 2, Difficulty = "Pro" }), capB);

        MessageFrame respFrame = capB.SentFrames.First(f => f.CommandType == FrameCommandType.Response && f.SubCommandType == FrameCommandType.AddTableBot);
        ResponseDto resp = respFrame.GetObjectFromMessage<ResponseDto>();
        Assert.False(resp.OkResult);
        Assert.Equal("Bots are disabled for this competitive Team Challenge.", resp.ErrorMessage);
        Assert.NotEqual(teamAId, teamBId);
    }

    [Fact]
    public async Task PlayWindow24Hours_NoShowForfeitResolution_AwardsSingleSideForfeitAndPenalizesMutualNoShow()
    {
        using ApplicationDbContext db = CreateInMemoryDb(nameof(PlayWindow24Hours_NoShowForfeitResolution_AwardsSingleSideForfeitAndPenalizesMutualNoShow));
        ClientManagerMock clientManager = new();
        LobbyManager lobbyManager = CreateLobbyManager(out _);
        TeamService teamService = new(db, clientManager, lobbyManager);

        (long teamAId, _, _, _) = await CreateCrewAsync(db, teamService, clientManager, "Team Alpha", "ALPH", "N1", teamElo: 1000);
        (long teamBId, _, _, _) = await CreateCrewAsync(db, teamService, clientManager, "Team Beta", "BETA", "N2", teamElo: 1000);
        (long teamCId, _, _, _) = await CreateCrewAsync(db, teamService, clientManager, "Team Gamma", "GAMA", "N3", teamElo: 105);
        (long teamDId, _, _, _) = await CreateCrewAsync(db, teamService, clientManager, "Team Delta", "DELT", "N4", teamElo: 1000);
        (long teamEId, _, _, _) = await CreateCrewAsync(db, teamService, clientManager, "Team Echo", "ECHO", "N5", teamElo: 1000);
        (long teamFId, _, _, _) = await CreateCrewAsync(db, teamService, clientManager, "Team Foxtrot", "FOXT", "N6", teamElo: 105);

        DateTime baseUtc = new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);

        // Case 1: Only Team A checked in (Team B no-show) -> Team A +16 (Wins+1), Team B -16 (Losses+1), ForfeitWinA
        TeamChallengeEntity chOnlyA = new()
        {
            ChallengerTeamId = teamAId,
            TargetTeamId = teamBId,
            Status = TeamChallengeStatus.Accepted,
            CreatedUtc = baseUtc.AddHours(-25),
            PlayDeadlineUtc = baseUtc.AddHours(-1),
            TeamACheckedIn = true,
            TeamBCheckedIn = false
        };

        // Case 2: Only Team D checked in (Team C no-show, starting at 105 Elo -> floored at 100!) -> ForfeitWinB
        TeamChallengeEntity chOnlyB = new()
        {
            ChallengerTeamId = teamCId,
            TargetTeamId = teamDId,
            Status = TeamChallengeStatus.Accepted,
            CreatedUtc = baseUtc.AddHours(-25),
            PlayDeadlineUtc = baseUtc.AddHours(-1),
            TeamACheckedIn = false,
            TeamBCheckedIn = true
        };

        // Case 3: Neither side checked in -> BOTH teams -12 TeamElo (Team F floored at 100!), ExpiredMutualNoShow
        TeamChallengeEntity chMutual = new()
        {
            ChallengerTeamId = teamEId,
            TargetTeamId = teamFId,
            Status = TeamChallengeStatus.Accepted,
            CreatedUtc = baseUtc.AddHours(-25),
            PlayDeadlineUtc = baseUtc.AddHours(-1),
            TeamACheckedIn = false,
            TeamBCheckedIn = false
        };

        // Case 4: Still within 24h play window -> untouched
        TeamChallengeEntity chStillLive = new()
        {
            ChallengerTeamId = teamAId,
            TargetTeamId = teamEId,
            Status = TeamChallengeStatus.Accepted,
            CreatedUtc = baseUtc.AddHours(-2),
            PlayDeadlineUtc = baseUtc.AddHours(22),
            TeamACheckedIn = true,
            TeamBCheckedIn = false
        };

        db.TeamChallenges.AddRange(chOnlyA, chOnlyB, chMutual, chStillLive);
        await db.SaveChangesAsync();

        int resolved = await teamService.ResolveExpiredChallengesAsync(baseUtc);
        Assert.Equal(3, resolved);

        TeamEntity tA = await db.Teams.FirstAsync(t => t.Id == teamAId);
        TeamEntity tB = await db.Teams.FirstAsync(t => t.Id == teamBId);
        TeamEntity tC = await db.Teams.FirstAsync(t => t.Id == teamCId);
        TeamEntity tD = await db.Teams.FirstAsync(t => t.Id == teamDId);
        TeamEntity tE = await db.Teams.FirstAsync(t => t.Id == teamEId);
        TeamEntity tF = await db.Teams.FirstAsync(t => t.Id == teamFId);

        Assert.Equal(TeamChallengeStatus.ForfeitWinA, (await db.TeamChallenges.FirstAsync(c => c.Id == chOnlyA.Id)).Status);
        Assert.Equal(1016, tA.TeamElo);
        Assert.Equal(1, tA.TeamWins);
        Assert.Equal(984, tB.TeamElo);
        Assert.Equal(1, tB.TeamLosses);

        Assert.Equal(TeamChallengeStatus.ForfeitWinB, (await db.TeamChallenges.FirstAsync(c => c.Id == chOnlyB.Id)).Status);
        Assert.Equal(100, tC.TeamElo); // Floored at 100
        Assert.Equal(1, tC.TeamLosses);
        Assert.Equal(1016, tD.TeamElo);
        Assert.Equal(1, tD.TeamWins);

        Assert.Equal(TeamChallengeStatus.ExpiredMutualNoShow, (await db.TeamChallenges.FirstAsync(c => c.Id == chMutual.Id)).Status);
        Assert.Equal(988, tE.TeamElo);
        Assert.Equal(100, tF.TeamElo); // Floored at 100

        Assert.Equal(TeamChallengeStatus.Accepted, (await db.TeamChallenges.FirstAsync(c => c.Id == chStillLive.Id)).Status);
    }

    [Fact]
    public async Task MatchAbandonmentPenalty_Deducts15EloAndTeamElo_IncrementsAbandonedMatches_AndCannotBeRefarmed()
    {
        using ApplicationDbContext db = CreateInMemoryDb(nameof(MatchAbandonmentPenalty_Deducts15EloAndTeamElo_IncrementsAbandonedMatches_AndCannotBeRefarmed));
        ClientManagerMock clientManager = new();
        LobbyManager lobbyManager = CreateLobbyManager(out ChatManager chatManager);
        GameManager gameManager = new(chatManager);
        TeamService teamService = new(db, clientManager, lobbyManager);
        UserRepository userRepo = new(db, new NationalityRepository(db));
        MatchAbandonmentService abandonmentService = new(userRepo, clientManager, teamService, teamService);

        (long teamAId, RecordingClient capA, _, _) =
            await CreateCrewAsync(db, teamService, clientManager, "Quitters Crew", "QUIT", "Q1", teamElo: 1000, memberElo: 1000);
        (long teamBId, RecordingClient capB, _, _) =
            await CreateCrewAsync(db, teamService, clientManager, "Stayers Crew", "STAY", "Q2", teamElo: 1000, memberElo: 1000);

        // Start a 1v1 Team Challenge match
        TeamActionResult<TeamChallengeActionResponseDto> sent = await teamService.SendTeamChallengeAsync(
            capA, new SendTeamChallengeDto { TargetTeamId = teamBId, GameMode = MatchFormat.TWO_BEST, Variant = TableStyle.DRAW });
        Assert.True(sent.Ok);
        long challengeId = sent.Data!.Challenge.ChallengeId;

        Assert.True((await teamService.RespondTeamChallengeAsync(
            capB, new RespondTeamChallengeDto { ChallengeId = challengeId, Accept = true })).Ok);

        Lobby lobby = lobbyManager.GetLobbyById(sent.Data.Table!.Id)!;
        gameManager.CreateGameFromLobby(lobby, seed: 99);
        Game game = gameManager.GetById(lobby.Id)!;
        game.DistributeDominoes();
        game.PlaceFirstDomino();

        // capA abandons mid-match -> -15 Elo + AbandonedMatches=1 on capA, +15 Elo on capB, -15/+15 TeamElo, Completed!
        AbandonmentPenaltyResult res = await abandonmentService.ApplyAbandonmentPenaltyAsync(game, capA.User!.Id);
        Assert.True(res.Applied);
        Assert.Equal(985, res.AbandonerNewElo);
        Assert.Equal(1, res.AbandonerAbandonedMatches);
        Assert.Equal(1015, res.OpponentNewElos[capB.User!.Id]);
        Assert.NotNull(res.TeamBreakdown);
        Assert.Equal(985, res.TeamBreakdown!.TeamA.NewElo);
        Assert.Equal(1015, res.TeamBreakdown.TeamB.NewElo);

        // Verify persisted in DB
        UserEntity persistedAbandoner = await db.Users.FirstAsync(u => u.Id == capA.User.Id);
        UserEntity persistedOpponent = await db.Users.FirstAsync(u => u.Id == capB.User.Id);
        TeamEntity persistedTeamA = await db.Teams.FirstAsync(t => t.Id == teamAId);
        TeamEntity persistedTeamB = await db.Teams.FirstAsync(t => t.Id == teamBId);
        TeamChallengeEntity persistedChallenge = await db.TeamChallenges.FirstAsync(c => c.Id == challengeId);

        Assert.Equal(985, persistedAbandoner.Elo);
        Assert.Equal(1, persistedAbandoner.AbandonedMatches);
        Assert.Equal(1015, persistedOpponent.Elo);
        Assert.Equal(985, persistedTeamA.TeamElo);
        Assert.Equal(1, persistedTeamA.TeamLosses);
        Assert.Equal(1015, persistedTeamB.TeamElo);
        Assert.Equal(1, persistedTeamB.TeamWins);
        Assert.Equal(TeamChallengeStatus.Completed, persistedChallenge.Status);

        // Attempting to trigger abandonment a second time on the same match is blocked (cannot be re-farmed)
        AbandonmentPenaltyResult repeat = await abandonmentService.ApplyAbandonmentPenaltyAsync(game, capA.User.Id);
        Assert.False(repeat.Applied);
        Assert.Equal(985, (await db.Teams.FirstAsync(t => t.Id == teamAId)).TeamElo);
    }

    [Fact]
    public async Task SquadAvgElo_BreaksTeamLeaderboardTiesByAverageMemberElo()
    {
        using ApplicationDbContext db = CreateInMemoryDb(nameof(SquadAvgElo_BreaksTeamLeaderboardTiesByAverageMemberElo));
        ClientManagerMock clientManager = new();
        TeamService teamService = new(db, clientManager);

        (long lowAvgId, _, _, _) =
            await CreateCrewAsync(db, teamService, clientManager, "Alpha LowAvg", "LOWA", "S1", teamElo: 1200, memberElo: 950);
        (long highAvgId, RecordingClient capHigh, _, _) =
            await CreateCrewAsync(db, teamService, clientManager, "Zeta HighAvg", "HIGA", "S2", teamElo: 1200, memberElo: 1650);

        TeamLeaderboardDto lb = await teamService.GetTeamLeaderboardAsync(capHigh.User!.Id, limit: 10);
        Assert.Equal(2, lb.Entries.Count);

        // Both teams have 1200 TeamElo, so Zeta HighAvg (Squad Avg 1650) MUST rank #1 ahead of Alpha LowAvg (Squad Avg 950)!
        Assert.Equal(highAvgId, lb.Entries[0].TeamId);
        Assert.Equal(1650, lb.Entries[0].AverageMemberElo);
        Assert.Equal(lowAvgId, lb.Entries[1].TeamId);
        Assert.Equal(950, lb.Entries[1].AverageMemberElo);

        MyTeamDetailsDto myTeam = await teamService.GetMyTeamAsync(capHigh.User.Id);
        Assert.Equal(1650, myTeam.TeamInfo!.AverageMemberElo);
    }
}
