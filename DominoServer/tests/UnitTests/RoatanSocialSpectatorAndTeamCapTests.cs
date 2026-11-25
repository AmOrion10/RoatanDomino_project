using Application;
using Application.Common;
using Application.Interfaces;
using Application.Interfaces.Managers;
using Application.Messages.Queries.Chat;

using CommonTests.Fixtures;

using Domain;
using Domain.Game;

using DominoCommon.com.domino.common.Scripts.Enums;
using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Chat;
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
using Infrastructure.Services.ServerUpdate;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTests;

public class RoatanSocialSpectatorAndTeamCapTests
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

    private sealed class LegacyChatDispatcher(List<IClient> roomClients) : IMessageDispatcher
    {
        public Task<TResponse> Dispatch<TResponse>(ICommand<TResponse> command)
            => Task.FromResult(default(TResponse))!;

        public Task<TResponse> Dispatch<TResponse>(IQuery<TResponse> query)
        {
            if (query is GetAllClientsForChatQuery)
            {
                return Task.FromResult((TResponse)(object)roomClients);
            }
            return Task.FromResult(default(TResponse))!;
        }
    }

    private static int nextClientId = 500;

    private static ApplicationDbContext CreateInMemoryDb(string name)
    {
        DbContextOptions<ApplicationDbContext> opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"{name}-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(opts);
    }

    private static async Task<UserEntity> SeedUserAsync(ApplicationDbContext db, string login, int elo = 1000)
    {
        UserEntity user = new()
        {
            Login = login,
            Email = $"{login.ToLowerInvariant()}@roatan.test",
            Password = "Password1!",
            Birthdate = new DateOnly(1995, 6, 15),
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

    [Fact]
    public async Task TeamCapOf6_EnforcedAcrossCreateJoinRequestApproveAndInviteAccept()
    {
        using ApplicationDbContext db = CreateInMemoryDb(nameof(TeamCapOf6_EnforcedAcrossCreateJoinRequestApproveAndInviteAccept));
        ClientManagerMock clientManager = new();
        TeamService teamService = new(db, clientManager);

        Assert.Equal(6, TeamInfoDto.DefaultMaxMembers);
        Assert.Equal(6, TeamService.MaxTeamMembers);

        List<UserEntity> users = [];
        for (int i = 1; i <= 9; i++)
        {
            users.Add(await SeedUserAsync(db, $"CrewPlayer{i}"));
        }

        // Create team (ApprovalRequired) -> starts 1/6
        TeamActionResult<MyTeamDetailsDto> created = await teamService.CreateTeamAsync(users[0].Id, new CreateTeamDto
        {
            Name = "Six Cap Crew",
            Tag = "SIXC",
            JoinPolicy = TeamJoinPolicy.ApprovalRequired,
            MinElo = 100,
            ActivityRequirement = TeamActivityRequirement.Any
        });
        Assert.True(created.Ok);
        Assert.Equal(6, created.Data!.TeamInfo!.MaxMembers);
        long teamId = created.Data.TeamInfo.Id;

        // Queue a pending join request (user 7) and a pending invite (user 8) BEFORE filling to 6/6
        TeamActionResult<MyTeamDetailsDto> pendingReqRes = await teamService.JoinOrRequestTeamAsync(users[6].Id, teamId);
        Assert.True(pendingReqRes.Ok);
        TeamActionResult<MyTeamDetailsDto> pendingInvRes = await teamService.InviteToTeamAsync(users[0].Id, new InviteToTeamDto
        {
            TargetUserId = users[7].Id
        });
        Assert.True(pendingInvRes.Ok);

        long pendingRequestId = (await db.TeamJoinRequests.FirstAsync(r => r.UserId == users[6].Id)).Id;
        long pendingInviteId = (await db.TeamInvites.FirstAsync(i => i.InvitedUserId == users[7].Id)).Id;

        // Switch to Open temporarily and fill seats 2..6 (total = 6 members)
        Assert.True((await teamService.UpdateTeamSettingsAsync(users[0].Id, new UpdateTeamSettingsDto
        {
            Name = "Six Cap Crew",
            Tag = "SIXC",
            JoinPolicy = TeamJoinPolicy.Open,
            MinElo = 100,
            ActivityRequirement = TeamActivityRequirement.Any
        })).Ok);

        for (int i = 1; i <= 5; i++)
        {
            TeamActionResult<MyTeamDetailsDto> joined = await teamService.JoinOrRequestTeamAsync(users[i].Id, teamId);
            Assert.True(joined.Ok);
        }

        MyTeamDetailsDto fullTeam = await teamService.GetMyTeamAsync(users[0].Id);
        Assert.Equal(6, fullTeam.TeamInfo!.MemberCount);
        Assert.Equal(6, fullTeam.TeamInfo.MaxMembers);

        // Even if a legacy DB row had max_members = 12, cap of 6 must still be enforced!
        TeamEntity teamRow = await db.Teams.FirstAsync(t => t.Id == teamId);
        teamRow.MaxMembers = 12;
        await db.SaveChangesAsync();

        // 1. Direct Open join by 9th player -> blocked with exact 6/6 message
        TeamActionResult<MyTeamDetailsDto> openJoinBlocked = await teamService.JoinOrRequestTeamAsync(users[8].Id, teamId);
        Assert.False(openJoinBlocked.Ok);
        Assert.Equal("This team is full (6/6 members).", openJoinBlocked.ErrorMessage);

        // 2. Approving earlier pending join request -> blocked with exact 6/6 message
        TeamActionResult<MyTeamDetailsDto> approveBlocked = await teamService.RespondTeamJoinRequestAsync(users[0].Id, pendingRequestId, approve: true);
        Assert.False(approveBlocked.Ok);
        Assert.Equal("This team is full (6/6 members).", approveBlocked.ErrorMessage);

        // 3. Accepting earlier pending invite -> blocked with exact 6/6 message
        TeamActionResult<MyTeamDetailsDto> inviteAcceptBlocked = await teamService.RespondTeamInviteAsync(users[7].Id, pendingInviteId, accept: true);
        Assert.False(inviteAcceptBlocked.Ok);
        Assert.Equal("This team is full (6/6 members).", inviteAcceptBlocked.ErrorMessage);

        // 4. Sending new invite when full -> blocked with exact 6/6 message
        TeamActionResult<MyTeamDetailsDto> newInviteBlocked = await teamService.InviteToTeamAsync(users[0].Id, new InviteToTeamDto
        {
            TargetUserId = users[8].Id
        });
        Assert.False(newInviteBlocked.Ok);
        Assert.Equal("This team is full (6/6 members).", newInviteBlocked.ErrorMessage);
    }

    [Fact]
    public async Task LiveMatchSpectating_ProtectsHandPrivacy_UpdatesLiveViewerCount_AndCleansUpOnDisconnect()
    {
        using ApplicationDbContext db = CreateInMemoryDb(nameof(LiveMatchSpectating_ProtectsHandPrivacy_UpdatesLiveViewerCount_AndCleansUpOnDisconnect));
        ClientManagerMock clientManager = new();
        ChatManager chatManager = new();
        LobbyManager lobbyManager = new(new TimerManager(), new CodeGenerator(new RandomGenerator()), chatManager);
        GameManager gameManager = new(chatManager);
        TokenManager tokenManager = new();

        UserEntity p1Entity = await SeedUserAsync(db, "LivePlayerOne");
        UserEntity p2Entity = await SeedUserAsync(db, "LivePlayerTwo");
        UserEntity s1Entity = await SeedUserAsync(db, "SpectatorOne");
        UserEntity s2Entity = await SeedUserAsync(db, "SpectatorTwo");

        RecordingClient p1 = ConnectUser(clientManager, p1Entity);
        RecordingClient p2 = ConnectUser(clientManager, p2Entity);
        RecordingClient s1 = ConnectUser(clientManager, s1Entity);
        RecordingClient s2 = ConnectUser(clientManager, s2Entity);

        Lobby lobby = lobbyManager.CreateLobby(p1, 2, true, "West Bay Live Match", tableStyle: TableStyle.DRAW, matchFormat: MatchFormat.TWO_BEST);
        lobbyManager.AddPlayerToLobby(p2, lobby.Id);
        gameManager.CreateGameFromLobby(lobby, seed: 77);
        lobbyManager.DeleteLobby(lobby.Id);

        Game game = gameManager.GetById(lobby.Id)!;
        game.DistributeDominoes();
        game.PlaceFirstDomino();

        // Active games appear in LobbiesInfoDto with IsInProgress = true and SpectatorCount = 0
        ResponseObjectDto<LobbiesInfoDto> initialTables = DtoBuilder.GetLobbiesInfoDto(lobbyManager.GetAllPublicLobbies(), gameManager.GetAllActiveGames());
        LobbyInfoDto? liveEntry = initialTables.Object.Lobbies.FirstOrDefault(t => t.Id == game.Id);
        Assert.NotNull(liveEntry);
        Assert.True(liveEntry!.IsInProgress);
        Assert.Equal(0, liveEntry.SpectatorCount);

        ServiceProvider sp = new ServiceCollection().BuildServiceProvider();
        GameServerUpdater updater = new(sp, NullLogger<GameServerUpdater>.Instance);
        GameFrameHandlers gameHandlers = new(new MessageDispatcherMock(), gameManager, tokenManager, chatManager, updater, clientManager);
        MessageFrameHandler frameHandler = new(NullLogger<MessageFrameHandler>.Instance);
        gameHandlers.RegisterHandlers(frameHandler);

        // Active player cannot spectate their own match
        await frameHandler.HandleAsync(MessageFrameBuilder.JoinAsSpectatorFrame(new JoinSpectatorDto { GameId = game.Id }), p1);
        MessageFrame p1Resp = p1.SentFrames.First(f => f.CommandType == FrameCommandType.Response && f.SubCommandType == FrameCommandType.JoinAsSpectator);
        Assert.False(p1Resp.GetObjectFromMessage<ResponseObjectDto<SpectatorMatchSnapshotDto>>().OkResult);

        // Spectator 1 joins -> receives public board snapshot (NO unplayed hand tiles!) and SpectatorCount = 1
        p1.SentFrames.Clear();
        p2.SentFrames.Clear();
        s1.SentFrames.Clear();

        await frameHandler.HandleAsync(MessageFrameBuilder.JoinAsSpectatorFrame(new JoinSpectatorDto { GameId = game.Id }), s1);
        MessageFrame s1JoinFrame = s1.SentFrames.First(f => f.CommandType == FrameCommandType.Response && f.SubCommandType == FrameCommandType.JoinAsSpectator);
        ResponseObjectDto<SpectatorMatchSnapshotDto> s1SnapResp = s1JoinFrame.GetObjectFromMessage<ResponseObjectDto<SpectatorMatchSnapshotDto>>();
        Assert.True(s1SnapResp.OkResult);
        Assert.Equal(game.Id, s1SnapResp.Object.GameId);
        Assert.Equal(1, s1SnapResp.Object.SpectatorCount);
        Assert.Single(s1SnapResp.Object.BoardMoves);
        Assert.Equal(2, s1SnapResp.Object.NumberOfDominoPerPlayer.Count);
        // Verify spectator NEVER receives hand distribution or possible moves
        Assert.DoesNotContain(s1.SentFrames, f => f.CommandType == FrameCommandType.DominoDistribution);
        Assert.DoesNotContain(s1.SentFrames, f => f.CommandType == FrameCommandType.PossibleMoves);

        // Both seated players receive live SpectatorCountUpdated = 1
        Assert.Contains(p1.SentFrames, f => f.CommandType == FrameCommandType.SpectatorCountUpdated &&
            f.GetObjectFromMessage<SpectatorCountUpdateDto>().SpectatorCount == 1);
        Assert.Contains(p2.SentFrames, f => f.CommandType == FrameCommandType.SpectatorCountUpdated &&
            f.GetObjectFromMessage<SpectatorCountUpdateDto>().SpectatorCount == 1);

        // Spectator 2 joins -> count becomes 2
        p1.SentFrames.Clear();
        await frameHandler.HandleAsync(MessageFrameBuilder.JoinAsSpectatorFrame(new JoinSpectatorDto { GameId = game.Id }), s2);
        Assert.Equal(2, game.SpectatorCount);
        Assert.Contains(p1.SentFrames, f => f.CommandType == FrameCommandType.SpectatorCountUpdated &&
            f.GetObjectFromMessage<SpectatorCountUpdateDto>().SpectatorCount == 2);

        // Spectator 1 leaves explicitly -> count drops to 1
        p1.SentFrames.Clear();
        await frameHandler.HandleAsync(MessageFrameBuilder.LeaveSpectatorFrame(new LeaveSpectatorDto { GameId = game.Id }), s1);
        Assert.Equal(1, game.SpectatorCount);
        Assert.Contains(p1.SentFrames, f => f.CommandType == FrameCommandType.SpectatorCountUpdated &&
            f.GetObjectFromMessage<SpectatorCountUpdateDto>().SpectatorCount == 1);

        // Spectator 2 disconnects unexpectedly -> ClientDisconnectionManager removes spectator and broadcasts count 0
        p1.SentFrames.Clear();
        UserRepository userRepo = new(db, new NationalityRepository(db));
        PresenceService presence = new(userRepo, clientManager, lobbyManager, gameManager, db);
        ClientDisconnectionManager disconnectMgr = new(new MessageDispatcherMock(), presence, gameManager, clientManager);
        await disconnectMgr.ProcessDisconnection(new DisconnectionResult(s2.User));

        Assert.Equal(0, game.SpectatorCount);
        Assert.Contains(p1.SentFrames, f => f.CommandType == FrameCommandType.SpectatorCountUpdated &&
            f.GetObjectFromMessage<SpectatorCountUpdateDto>().SpectatorCount == 0);
    }

    [Fact]
    public async Task DirectPlayerChallenge_AcceptCreatesPrivate2SeatTable_AndIgnoreDeclinesCleanly()
    {
        using ApplicationDbContext db = CreateInMemoryDb(nameof(DirectPlayerChallenge_AcceptCreatesPrivate2SeatTable_AndIgnoreDeclinesCleanly));
        ClientManagerMock clientManager = new();
        ChatManager chatManager = new();
        LobbyManager lobbyManager = new(new TimerManager(), new CodeGenerator(new RandomGenerator()), chatManager);
        DirectChallengeService dcService = new(clientManager, lobbyManager);

        UserEntity u1 = await SeedUserAsync(db, "ChallengerOne", elo: 1250);
        UserEntity u2 = await SeedUserAsync(db, "TargetTwo", elo: 1180);
        UserEntity u3 = await SeedUserAsync(db, "BystanderThree", elo: 1000);

        RecordingClient c1 = ConnectUser(clientManager, u1);
        RecordingClient c2 = ConnectUser(clientManager, u2);
        RecordingClient c3 = ConnectUser(clientManager, u3);

        DirectChallengeFrameHandlers dcHandlers = new(dcService);
        MessageFrameHandler frameHandler = new(NullLogger<MessageFrameHandler>.Instance);
        dcHandlers.RegisterHandlers(frameHandler);

        // Cannot challenge self
        DirectChallengeResult selfRes = await dcService.SendDirectChallengeAsync(c1, new SendDirectChallengeDto { TargetUserId = u1.Id });
        Assert.False(selfRes.Ok);

        // Send Two-Best 1v1 Block challenge via WebSocket frame
        await frameHandler.HandleAsync(MessageFrameBuilder.SendDirectChallengeFrame(new SendDirectChallengeDto
        {
            TargetUserId = u2.Id,
            GameMode = MatchFormat.TWO_BEST,
            Variant = TableStyle.BLOCK
        }), c1);

        MessageFrame notifFrame = c2.SentFrames.First(f => f.CommandType == FrameCommandType.DirectChallengeNotification);
        DirectChallengeDto incoming = notifFrame.GetObjectFromMessage<DirectChallengeDto>();
        Assert.Equal(DirectChallengeStatus.Pending, incoming.Status);
        Assert.Equal(MatchFormat.TWO_BEST, incoming.GameMode);
        Assert.Equal(TableStyle.BLOCK, incoming.Variant);
        Assert.Equal(u1.Id, incoming.ChallengerUserId);

        // Bystander cannot respond to someone else's challenge
        DirectChallengeResult wrongResponder = await dcService.RespondDirectChallengeAsync(c3, new RespondDirectChallengeDto
        {
            ChallengeId = incoming.ChallengeId,
            Accept = true
        });
        Assert.False(wrongResponder.Ok);

        // Target accepts -> creates private 2-seat table with BOTH players seated immediately!
        c1.SentFrames.Clear();
        c2.SentFrames.Clear();
        await frameHandler.HandleAsync(MessageFrameBuilder.RespondDirectChallengeFrame(new RespondDirectChallengeDto
        {
            ChallengeId = incoming.ChallengeId,
            Accept = true
        }), c2);

        MessageFrame acceptRespFrame = c2.SentFrames.First(f => f.CommandType == FrameCommandType.Response && f.SubCommandType == FrameCommandType.RespondDirectChallenge);
        ResponseObjectDto<DirectChallengeActionResponseDto> acceptResp = acceptRespFrame.GetObjectFromMessage<ResponseObjectDto<DirectChallengeActionResponseDto>>();
        Assert.True(acceptResp.OkResult);
        Assert.NotNull(acceptResp.Object.Table);
        Assert.False(acceptResp.Object.Table!.IsPublic);
        Assert.Equal(2, acceptResp.Object.Table.MaxNumberOfPlayer);
        Assert.Equal(2, acceptResp.Object.Table.Players.Count);
        Assert.Equal(MatchFormat.TWO_BEST, acceptResp.Object.Table.MatchFormat);
        Assert.Equal(TableStyle.BLOCK, acceptResp.Object.Table.TableStyle);
        Assert.True(lobbyManager.PlayerIsInLobby(c1, acceptResp.Object.Table.Id));
        Assert.True(lobbyManager.PlayerIsInLobby(c2, acceptResp.Object.Table.Id));

        // Challenger receives TableStateUpdated + Accepted notification
        Assert.Contains(c1.SentFrames, f => f.CommandType == FrameCommandType.TableStateUpdated);
        Assert.Contains(c1.SentFrames, f => f.CommandType == FrameCommandType.DirectChallengeNotification &&
            f.GetObjectFromMessage<DirectChallengeDto>().Status == DirectChallengeStatus.Accepted);

        // Second challenge (Cut-Throat 1v1 Draw) ignored -> no table created
        DirectChallengeResult sent2 = await dcService.SendDirectChallengeAsync(c1, new SendDirectChallengeDto
        {
            TargetUserId = u3.Id,
            GameMode = MatchFormat.CUT_THROAT,
            Variant = TableStyle.DRAW
        });
        Assert.True(sent2.Ok);
        DirectChallengeResult ignored = await dcService.RespondDirectChallengeAsync(c3, new RespondDirectChallengeDto
        {
            ChallengeId = sent2.Data!.Challenge.ChallengeId,
            Accept = false
        });
        Assert.True(ignored.Ok);
        Assert.Equal(DirectChallengeStatus.Ignored, ignored.Data!.Challenge.Status);
        Assert.Null(ignored.Data.Table);
    }

    [Fact]
    public async Task SocialChatService_DeliversTeamAndDirectMessages_PreservesPrivacy_AndKeepsLegacyRoomChatWorking()
    {
        using ApplicationDbContext db = CreateInMemoryDb(nameof(SocialChatService_DeliversTeamAndDirectMessages_PreservesPrivacy_AndKeepsLegacyRoomChatWorking));
        ClientManagerMock clientManager = new();
        TeamService teamService = new(db, clientManager);
        ChatService chatService = new(db, clientManager);

        UserEntity capEntity = await SeedUserAsync(db, "ChatCaptain");
        UserEntity mateEntity = await SeedUserAsync(db, "ChatTeammate");
        UserEntity outsiderEntity = await SeedUserAsync(db, "ChatOutsider");

        RecordingClient cap = ConnectUser(clientManager, capEntity);
        RecordingClient mate = ConnectUser(clientManager, mateEntity);
        RecordingClient outsider = ConnectUser(clientManager, outsiderEntity);

        TeamActionResult<MyTeamDetailsDto> created = await teamService.CreateTeamAsync(capEntity.Id, new CreateTeamDto
        {
            Name = "Chat Crew",
            Tag = "CHAT",
            JoinPolicy = TeamJoinPolicy.Open,
            MinElo = 100,
            ActivityRequirement = TeamActivityRequirement.Any
        });
        Assert.True(created.Ok);
        long teamId = created.Data!.TeamInfo!.Id;
        Assert.True((await teamService.JoinOrRequestTeamAsync(mateEntity.Id, teamId)).Ok);

        ChatFrameHandlers chatHandlers = new(new LegacyChatDispatcher([cap, outsider]), chatService);
        MessageFrameHandler frameHandler = new(NullLogger<MessageFrameHandler>.Instance);
        chatHandlers.RegisterHandlers(frameHandler);

        // 1. Outsider cannot send Team Chat message
        ChatActionResult<ChatMessageDto> outsiderTeamFail = await chatService.SendMessageAsync(outsiderEntity.Id, new ChatMessageDto
        {
            ChannelType = SocialChatChannelType.Team,
            TeamId = teamId,
            Body = "Trying to sneak in!"
        });
        Assert.False(outsiderTeamFail.Ok);

        // 2. Captain sends Team Chat via SendChatMessage frame -> Captain & Teammate receive it, Outsider does NOT
        cap.SentFrames.Clear();
        mate.SentFrames.Clear();
        outsider.SentFrames.Clear();

        await frameHandler.HandleAsync(MessageFrameBuilder.SendSocialChatMessageFrame(new ChatMessageDto
        {
            ChannelType = SocialChatChannelType.Team,
            TeamId = teamId,
            Body = "Crew rally at 8pm!"
        }), cap);

        Assert.Contains(cap.SentFrames, f => f.CommandType == FrameCommandType.ChatMessageReceived &&
            f.GetObjectFromMessage<ChatMessageDto>().Body == "Crew rally at 8pm!");
        Assert.Contains(mate.SentFrames, f => f.CommandType == FrameCommandType.ChatMessageReceived &&
            f.GetObjectFromMessage<ChatMessageDto>().Body == "Crew rally at 8pm!");
        Assert.DoesNotContain(outsider.SentFrames, f => f.CommandType == FrameCommandType.ChatMessageReceived);

        // 3. Captain sends 1-on-1 Direct Message to Outsider -> Captain & Outsider receive it, Teammate does NOT
        cap.SentFrames.Clear();
        mate.SentFrames.Clear();
        outsider.SentFrames.Clear();

        await frameHandler.HandleAsync(MessageFrameBuilder.SendSocialChatMessageFrame(new ChatMessageDto
        {
            ChannelType = SocialChatChannelType.Direct,
            RecipientUserId = outsiderEntity.Id,
            Body = "Good match earlier, want a 1v1?"
        }), cap);

        Assert.Contains(cap.SentFrames, f => f.CommandType == FrameCommandType.ChatMessageReceived &&
            f.GetObjectFromMessage<ChatMessageDto>().ChannelType == SocialChatChannelType.Direct);
        Assert.Contains(outsider.SentFrames, f => f.CommandType == FrameCommandType.ChatMessageReceived &&
            f.GetObjectFromMessage<ChatMessageDto>().Body == "Good match earlier, want a 1v1?");
        Assert.DoesNotContain(mate.SentFrames, f => f.CommandType == FrameCommandType.ChatMessageReceived);

        // 4. GetChatHistory returns accurate Team history and Direct history
        mate.SentFrames.Clear();
        await frameHandler.HandleAsync(MessageFrameBuilder.GetChatHistoryFrame(new GetChatHistoryDto
        {
            ChannelType = SocialChatChannelType.Team,
            Limit = 20
        }), mate);
        MessageFrame teamHistFrame = mate.SentFrames.First(f => f.CommandType == FrameCommandType.Response && f.SubCommandType == FrameCommandType.GetChatHistory);
        ChatHistoryDto teamHist = teamHistFrame.GetObjectFromMessage<ResponseObjectDto<ChatHistoryDto>>().Object;
        Assert.Single(teamHist.Messages);
        Assert.Equal("Crew rally at 8pm!", teamHist.Messages[0].Body);

        outsider.SentFrames.Clear();
        await frameHandler.HandleAsync(MessageFrameBuilder.GetChatHistoryFrame(new GetChatHistoryDto
        {
            ChannelType = SocialChatChannelType.Direct,
            OtherUserId = capEntity.Id,
            Limit = 20
        }), outsider);
        MessageFrame dmHistFrame = outsider.SentFrames.First(f => f.CommandType == FrameCommandType.Response && f.SubCommandType == FrameCommandType.GetChatHistory);
        ChatHistoryDto dmHist = dmHistFrame.GetObjectFromMessage<ResponseObjectDto<ChatHistoryDto>>().Object;
        Assert.Single(dmHist.Messages);
        Assert.Equal("Good match earlier, want a 1v1?", dmHist.Messages[0].Body);

        // 5. Legacy room SendChatMessageDto still works untouched
        cap.SentFrames.Clear();
        outsider.SentFrames.Clear();
        await frameHandler.HandleAsync(MessageFrameBuilder.SendMessageFrame(new SendChatMessageDto
        {
            ChatId = 1,
            Message = "Legacy global hello"
        }), cap);
        Assert.Contains(outsider.SentFrames, f => f.CommandType == FrameCommandType.ReceiveChatMessage &&
            f.GetObjectFromMessage<ReceiveChatMessageDto>().Message == "Legacy global hello");
    }
}
