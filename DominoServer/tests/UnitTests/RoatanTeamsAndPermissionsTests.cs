using CommonTests.Fixtures;

using Domain;

using DominoCommon.com.domino.common.Scripts.Enums;
using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Teams;
using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;

using Infrastructure.Database;
using Infrastructure.Database.Entities;
using Infrastructure.Services;
using Infrastructure.Services.FrameHandlers;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTests;

public class RoatanTeamsAndPermissionsTests
{
    private static ApplicationDbContext CreateInMemoryDb(string name)
    {
        DbContextOptions<ApplicationDbContext> opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new ApplicationDbContext(opts);
    }

    private static async Task<UserEntity> SeedUserAsync(
        ApplicationDbContext db,
        long id,
        string login,
        int elo = 1000,
        int onlineMatchesLast7Days = 0,
        DateTime? lastOnlineMatchUtc = null)
    {
        UserEntity user = new()
        {
            Id = id,
            Login = login,
            Email = $"{login.ToLowerInvariant()}@roatan.test",
            Password = "hashed-password",
            Elo = elo,
            OnlineMatchesLast7Days = onlineMatchesLast7Days,
            LastOnlineMatchUtc = lastOnlineMatchUtc,
            Birthdate = new DateOnly(1995, 1, 1)
        };
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task CreateTeam_AssignsCaptainAndDefault1000Elo_AndEnforcesOneTeamPerPlayer()
    {
        using ApplicationDbContext db = CreateInMemoryDb($"teams-create-{Guid.NewGuid():N}");
        await SeedUserAsync(db, 1, "CapMorgan", elo: 1400);
        await SeedUserAsync(db, 2, "OtherSkipper", elo: 1200);

        TeamService service = new(db, new ClientManagerMock());

        var created = await service.CreateTeamAsync(1, new CreateTeamDto
        {
            Name = "West End Sharks",
            Tag = "wes",
            JoinPolicy = TeamJoinPolicy.Open,
            MinElo = 900,
            ActivityRequirement = TeamActivityRequirement.Any
        });

        Assert.True(created.Ok);
        Assert.NotNull(created.Data?.TeamInfo);
        Assert.Equal("West End Sharks", created.Data!.TeamInfo.Name);
        Assert.Equal("WES", created.Data.TeamInfo.Tag);
        Assert.Equal(1000, created.Data.TeamInfo.TeamElo);
        Assert.Equal(RoatanRankTier.Sailor, created.Data.TeamInfo.TeamRankTier);
        Assert.Equal(12, created.Data.TeamInfo.MaxMembers);
        Assert.Equal(TeamRole.Captain, created.Data.MyRole);
        Assert.Single(created.Data.Members);
        Assert.Equal(TeamRole.Captain, created.Data.Members[0].Role);

        // One team per player: creator cannot create a second team
        var secondAttempt = await service.CreateTeamAsync(1, new CreateTeamDto
        {
            Name = "Second Crew",
            Tag = "SEC"
        });
        Assert.False(secondAttempt.Ok);
        Assert.Contains("already in a team", secondAttempt.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Duplicate tag rejected for another player
        var dupTag = await service.CreateTeamAsync(2, new CreateTeamDto
        {
            Name = "Another Reef Crew",
            Tag = "wes"
        });
        Assert.False(dupTag.Ok);
        Assert.Contains("already taken", dupTag.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EntryConditions_EnforcesMinElo_TwiceWeekly_AndDailyActivityWithPlainEnglishReason()
    {
        using ApplicationDbContext db = CreateInMemoryDb($"teams-eligibility-{Guid.NewGuid():N}");
        DateTime now = DateTime.UtcNow;

        await SeedUserAsync(db, 1, "EliteCap", elo: 1600, onlineMatchesLast7Days: 5, lastOnlineMatchUtc: now.AddHours(-1));
        await SeedUserAsync(db, 2, "LowAndInactive", elo: 1000, onlineMatchesLast7Days: 0, lastOnlineMatchUtc: null);
        await SeedUserAsync(db, 3, "HighEloStale", elo: 1350, onlineMatchesLast7Days: 1, lastOnlineMatchUtc: now.AddHours(-30));
        await SeedUserAsync(db, 4, "QualifiedDaily", elo: 1250, onlineMatchesLast7Days: 1, lastOnlineMatchUtc: now.AddHours(-3));
        await SeedUserAsync(db, 5, "TwiceWeeklyCap", elo: 1100, onlineMatchesLast7Days: 4, lastOnlineMatchUtc: now.AddHours(-10));
        await SeedUserAsync(db, 6, "QualifiedWeekly", elo: 1050, onlineMatchesLast7Days: 2, lastOnlineMatchUtc: now.AddDays(-2));

        TeamService service = new(db, new ClientManagerMock());

        // Team 1: Navigator (1200+ Elo) + Daily activity
        var dailyTeamRes = await service.CreateTeamAsync(1, new CreateTeamDto
        {
            Name = "Reef Navigators",
            Tag = "NAV",
            JoinPolicy = TeamJoinPolicy.Open,
            MinElo = 1200,
            ActivityRequirement = TeamActivityRequirement.Daily
        });
        long dailyTeamId = dailyTeamRes.Data!.TeamInfo.Id;

        // Team 2: TwiceWeekly activity (2+ matches in 7 days)
        var weeklyTeamRes = await service.CreateTeamAsync(5, new CreateTeamDto
        {
            Name = "Island Regulars",
            Tag = "REG",
            JoinPolicy = TeamJoinPolicy.Open,
            MinElo = 900,
            ActivityRequirement = TeamActivityRequirement.TwiceWeekly
        });
        long weeklyTeamId = weeklyTeamRes.Data!.TeamInfo.Id;

        // User 2 fails both Elo and Daily activity
        TeamsListDto browseForLow = await service.GetTeamsAsync(2);
        TeamInfoDto navForLow = browseForLow.Teams.First(t => t.Id == dailyTeamId);
        Assert.False(navForLow.MeetsRequirements);
        Assert.Equal("Requires Navigator rank (1200+ Elo) and Daily activity.", navForLow.DisqualifyReason);

        var joinFailBoth = await service.JoinOrRequestTeamAsync(2, dailyTeamId);
        Assert.False(joinFailBoth.Ok);
        Assert.Equal("Requires Navigator rank (1200+ Elo) and Daily activity.", joinFailBoth.ErrorMessage);

        // User 3 meets Elo (1350 >= 1200) but fails Daily (30h ago) and TwiceWeekly (only 1 match in 7d)
        var joinFailDaily = await service.JoinOrRequestTeamAsync(3, dailyTeamId);
        Assert.False(joinFailDaily.Ok);
        Assert.Equal("Requires Daily activity.", joinFailDaily.ErrorMessage);

        var joinFailWeekly = await service.JoinOrRequestTeamAsync(3, weeklyTeamId);
        Assert.False(joinFailWeekly.Ok);
        Assert.Contains("Twice-Weekly", joinFailWeekly.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // User 4 meets Navigator (1250) + Daily (3h ago) -> joins Open team immediately!
        var joinDailyOk = await service.JoinOrRequestTeamAsync(4, dailyTeamId);
        Assert.True(joinDailyOk.Ok);
        Assert.Equal(dailyTeamId, joinDailyOk.Data!.TeamInfo.Id);
        Assert.Equal(TeamRole.Teammate, joinDailyOk.Data.MyRole);

        // User 6 meets Sailor (1050) + TwiceWeekly (2 matches in 7d) -> joins Weekly team!
        var joinWeeklyOk = await service.JoinOrRequestTeamAsync(6, weeklyTeamId);
        Assert.True(joinWeeklyOk.Ok);
        Assert.Equal(weeklyTeamId, joinWeeklyOk.Data!.TeamInfo.Id);
    }

    [Fact]
    public async Task RolePermissions_StrictlyEnforceCaptain_ViceCaptain_AndTeammateBoundaries()
    {
        using ApplicationDbContext db = CreateInMemoryDb($"teams-roles-{Guid.NewGuid():N}");
        await SeedUserAsync(db, 1, "CaptainA", elo: 1500);
        await SeedUserAsync(db, 2, "ViceB", elo: 1300);
        await SeedUserAsync(db, 3, "ViceC", elo: 1250);
        await SeedUserAsync(db, 4, "MateD", elo: 1100);
        await SeedUserAsync(db, 5, "MateE", elo: 1050);
        await SeedUserAsync(db, 6, "ApplicantF", elo: 1200);
        await SeedUserAsync(db, 7, "OutsideG", elo: 1000);
        await SeedUserAsync(db, 8, "OutsideH", elo: 1000);
        await SeedUserAsync(db, 9, "OutsideI", elo: 1000);

        TeamService service = new(db, new ClientManagerMock());

        var created = await service.CreateTeamAsync(1, new CreateTeamDto
        {
            Name = "Coxen Hole Crew",
            Tag = "CHC",
            JoinPolicy = TeamJoinPolicy.Open,
            MinElo = 100,
            ActivityRequirement = TeamActivityRequirement.Any
        });
        long teamId = created.Data!.TeamInfo.Id;

        Assert.True((await service.JoinOrRequestTeamAsync(2, teamId)).Ok);
        Assert.True((await service.JoinOrRequestTeamAsync(3, teamId)).Ok);
        Assert.True((await service.JoinOrRequestTeamAsync(4, teamId)).Ok);
        Assert.True((await service.JoinOrRequestTeamAsync(5, teamId)).Ok);

        // Captain promotes ViceB and ViceC to ViceCaptain
        Assert.True((await service.PromoteOrDemoteMemberAsync(1, 2, TeamRole.ViceCaptain)).Ok);
        Assert.True((await service.PromoteOrDemoteMemberAsync(1, 3, TeamRole.ViceCaptain)).Ok);

        // Captain updates team settings to ApprovalRequired and renames team
        var capUpdate = await service.UpdateTeamSettingsAsync(1, new UpdateTeamSettingsDto
        {
            Name = "Coxen Hole Legends",
            Tag = "CHL",
            JoinPolicy = TeamJoinPolicy.ApprovalRequired,
            MinElo = 900,
            ActivityRequirement = TeamActivityRequirement.Any
        });
        Assert.True(capUpdate.Ok);
        Assert.Equal("Coxen Hole Legends", capUpdate.Data!.TeamInfo.Name);
        Assert.Equal("CHL", capUpdate.Data.TeamInfo.Tag);

        // ApplicantF requests to join (ApprovalRequired)
        var reqRes = await service.JoinOrRequestTeamAsync(6, teamId);
        Assert.True(reqRes.Ok);
        Assert.Null(reqRes.Data!.TeamInfo); // Still pending approval

        // Teammate (MateD) CANNOT see pending requests, respond to requests, kick, promote/demote, or edit settings
        MyTeamDetailsDto mateView = await service.GetMyTeamAsync(4);
        Assert.Empty(mateView.PendingRequests);

        long pendingRequestId = (await service.GetMyTeamAsync(2)).PendingRequests.Single().RequestId;
        Assert.False((await service.RespondTeamJoinRequestAsync(4, pendingRequestId, approve: true)).Ok);
        Assert.False((await service.KickTeamMemberAsync(4, 5)).Ok);
        Assert.False((await service.PromoteOrDemoteMemberAsync(4, 5, TeamRole.ViceCaptain)).Ok);
        Assert.False((await service.UpdateTeamSettingsAsync(4, new UpdateTeamSettingsDto { Name = "Hacked", Tag = "HCK" })).Ok);

        // Teammate CAN invite outside players
        Assert.True((await service.InviteToTeamAsync(4, new InviteToTeamDto { TargetUsername = "OutsideG" })).Ok);

        // Vice-Captain (ViceB) CANNOT rename/edit settings, promote/demote, kick Captain, or kick another Vice-Captain
        Assert.False((await service.UpdateTeamSettingsAsync(2, new UpdateTeamSettingsDto { Name = "Mutiny", Tag = "MUT" })).Ok);
        Assert.False((await service.PromoteOrDemoteMemberAsync(2, 4, TeamRole.ViceCaptain)).Ok);
        Assert.False((await service.PromoteOrDemoteMemberAsync(2, 3, TeamRole.Teammate)).Ok);
        Assert.False((await service.KickTeamMemberAsync(2, 1)).Ok); // Cannot kick Captain
        Assert.False((await service.KickTeamMemberAsync(2, 3)).Ok); // Cannot kick another Vice-Captain

        // Vice-Captain CAN invite outside players, accept/deny join requests, and kick Teammates
        Assert.True((await service.InviteToTeamAsync(2, new InviteToTeamDto { TargetUsername = "OutsideH" })).Ok);
        Assert.True((await service.RespondTeamJoinRequestAsync(2, pendingRequestId, approve: true)).Ok);
        Assert.True((await service.KickTeamMemberAsync(2, 5)).Ok); // Kicked MateE (Teammate)

        // Verify ApplicantF joined and MateE was removed
        MyTeamDetailsDto afterViceActions = await service.GetMyTeamAsync(1);
        Assert.Contains(afterViceActions.Members, m => m.UserId == 6 && m.Role == TeamRole.Teammate);
        Assert.DoesNotContain(afterViceActions.Members, m => m.UserId == 5);

        // Captain CAN invite, demote Vice-Captain -> Teammate, and kick Vice-Captain
        Assert.True((await service.InviteToTeamAsync(1, new InviteToTeamDto { TargetUsername = "OutsideI" })).Ok);
        var demoteRes = await service.PromoteOrDemoteMemberAsync(1, 3, TeamRole.Teammate);
        Assert.True(demoteRes.Ok);
        Assert.Equal(TeamRole.Teammate, demoteRes.Data!.Members.First(m => m.UserId == 3).Role);

        // Promote ViceC back and have Captain kick ViceC directly
        Assert.True((await service.PromoteOrDemoteMemberAsync(1, 3, TeamRole.ViceCaptain)).Ok);
        var capKickVice = await service.KickTeamMemberAsync(1, 3);
        Assert.True(capKickVice.Ok);
        Assert.DoesNotContain(capKickVice.Data!.Members, m => m.UserId == 3);
    }

    [Fact]
    public async Task Invites_CaptainLeaveTransfer_AndDisbandWhenEmpty_WorkCleanly()
    {
        using ApplicationDbContext db = CreateInMemoryDb($"teams-lifecycle-{Guid.NewGuid():N}");
        await SeedUserAsync(db, 1, "FounderCap");
        await SeedUserAsync(db, 2, "FirstVice");
        await SeedUserAsync(db, 3, "SecondMate");

        TeamService service = new(db, new ClientManagerMock());
        var created = await service.CreateTeamAsync(1, new CreateTeamDto { Name = "Sandy Bay Crew", Tag = "SBC" });
        long teamId = created.Data!.TeamInfo.Id;

        // Invite FirstVice and SecondMate -> both accept
        Assert.True((await service.InviteToTeamAsync(1, new InviteToTeamDto { TargetUserId = 2 })).Ok);
        Assert.True((await service.InviteToTeamAsync(1, new InviteToTeamDto { TargetUserId = 3 })).Ok);

        MyTeamDetailsDto vicePending = await service.GetMyTeamAsync(2);
        Assert.Single(vicePending.PendingInvites);
        Assert.True((await service.RespondTeamInviteAsync(2, vicePending.PendingInvites[0].InviteId, accept: true)).Ok);

        MyTeamDetailsDto matePending = await service.GetMyTeamAsync(3);
        Assert.Single(matePending.PendingInvites);
        Assert.True((await service.RespondTeamInviteAsync(3, matePending.PendingInvites[0].InviteId, accept: true)).Ok);

        // Promote FirstVice to ViceCaptain
        Assert.True((await service.PromoteOrDemoteMemberAsync(1, 2, TeamRole.ViceCaptain)).Ok);

        // FounderCap leaves -> Captain transfers to FirstVice!
        var capLeave = await service.LeaveTeamAsync(1);
        Assert.True(capLeave.Ok);
        Assert.Null(capLeave.Data!.TeamInfo);

        MyTeamDetailsDto afterTransfer = await service.GetMyTeamAsync(2);
        Assert.Equal(TeamRole.Captain, afterTransfer.MyRole);
        Assert.Equal(2, afterTransfer.Members.Count);

        // FirstVice leaves -> Captain transfers to SecondMate!
        Assert.True((await service.LeaveTeamAsync(2)).Ok);
        MyTeamDetailsDto lastStanding = await service.GetMyTeamAsync(3);
        Assert.Equal(TeamRole.Captain, lastStanding.MyRole);
        Assert.Single(lastStanding.Members);

        // SecondMate (last member) leaves -> team is disbanded!
        Assert.True((await service.LeaveTeamAsync(3)).Ok);
        Assert.False(await db.Teams.AnyAsync(t => t.Id == teamId));
    }

    [Fact]
    public async Task TeamFrameHandlers_HandleCreateBrowseInviteAndAcceptOverMessageFrames()
    {
        using ApplicationDbContext db = CreateInMemoryDb($"teams-frames-{Guid.NewGuid():N}");
        await SeedUserAsync(db, 10, "HostCap", elo: 1200);
        await SeedUserAsync(db, 11, "GuestPlayer", elo: 1100);

        ClientManagerMock clientManager = new();
        TeamService service = new(db, clientManager);
        TeamFrameHandlers handlers = new(service);
        MessageFrameHandler frameHandler = new(NullLogger<MessageFrameHandler>.Instance);
        handlers.RegisterHandlers(frameHandler);

        ClientMock capClient = new() { Id = 1, IsLoggedIn = true, User = new User("h@t.com", "HostCap") { Id = 10 } };
        ClientMock guestClient = new() { Id = 2, IsLoggedIn = true, User = new User("g@t.com", "GuestPlayer") { Id = 11 } };

        // Create team via frame
        await frameHandler.HandleAsync(MessageFrameBuilder.CreateTeamFrame(new CreateTeamDto
        {
            Name = "Roatan Reefers",
            Tag = "REEF",
            JoinPolicy = TeamJoinPolicy.ApprovalRequired,
            MinElo = 900,
            ActivityRequirement = TeamActivityRequirement.Any
        }), capClient);

        MessageFrame createRespFrame = capClient.GetLastMessageFrameByCommandType(FrameCommandType.Response);
        Assert.Equal(FrameCommandType.CreateTeam, createRespFrame.SubCommandType);
        ResponseObjectDto<MyTeamDetailsDto> createResp = createRespFrame.GetObjectFromMessage<ResponseObjectDto<MyTeamDetailsDto>>();
        Assert.True(createResp.OkResult);
        Assert.Equal("REEF", createResp.Object.TeamInfo.Tag);

        // Browse teams via frame with search query
        await frameHandler.HandleAsync(MessageFrameBuilder.GetTeamsFrame(new GetTeamsRequestDto { SearchQuery = "reef" }), guestClient);
        MessageFrame browseFrame = guestClient.GetLastMessageFrameByCommandType(FrameCommandType.Response);
        Assert.Equal(FrameCommandType.GetTeams, browseFrame.SubCommandType);
        ResponseObjectDto<TeamsListDto> browseResp = browseFrame.GetObjectFromMessage<ResponseObjectDto<TeamsListDto>>();
        Assert.True(browseResp.OkResult);
        Assert.Single(browseResp.Object.Teams);
        Assert.True(browseResp.Object.Teams[0].MeetsRequirements);

        // Invite guest via frame
        await frameHandler.HandleAsync(MessageFrameBuilder.InviteToTeamFrame(new InviteToTeamDto { TargetUsername = "GuestPlayer" }), capClient);
        MessageFrame inviteRespFrame = capClient.GetLastMessageFrameByCommandType(FrameCommandType.Response);
        Assert.Equal(FrameCommandType.InviteToTeam, inviteRespFrame.SubCommandType);
        Assert.True(inviteRespFrame.GetObjectFromMessage<ResponseObjectDto<MyTeamDetailsDto>>().OkResult);

        // Guest checks GetMyTeam -> sees invite and accepts via frame
        await frameHandler.HandleAsync(MessageFrameBuilder.GetMyTeamFrame(), guestClient);
        ResponseObjectDto<MyTeamDetailsDto> guestMyTeam = guestClient
            .GetLastMessageFrameByCommandType(FrameCommandType.Response)
            .GetObjectFromMessage<ResponseObjectDto<MyTeamDetailsDto>>();
        Assert.Single(guestMyTeam.Object.PendingInvites);

        long inviteId = guestMyTeam.Object.PendingInvites[0].InviteId;
        await frameHandler.HandleAsync(MessageFrameBuilder.RespondTeamInviteFrame(new RespondTeamInviteDto
        {
            InviteId = inviteId,
            Accept = true
        }), guestClient);

        ResponseObjectDto<MyTeamDetailsDto> acceptResp = guestClient
            .GetLastMessageFrameByCommandType(FrameCommandType.Response)
            .GetObjectFromMessage<ResponseObjectDto<MyTeamDetailsDto>>();
        Assert.True(acceptResp.OkResult);
        Assert.Equal("REEF", acceptResp.Object.TeamInfo.Tag);
        Assert.Equal(TeamRole.Teammate, acceptResp.Object.MyRole);
    }
}
