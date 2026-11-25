using Application;
using Application.Common;
using Application.Exceptions;
using Application.Exceptions.Connection;
using Application.Exceptions.Lobby;
using Application.Interfaces;
using Application.Interfaces.Managers;
using Application.Messages.Commands.Lobby;
using Application.Messages.Commands.User;

using CommonTests.Fixtures;

using Domain;
using Domain.Game;

using DominoCommon.com.domino.common.Scripts.Enums;
using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;
using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame;

using Infrastructure;
using Infrastructure.Database;
using Infrastructure.Services;
using Infrastructure.Services.FrameHandlers;
using Infrastructure.Services.Managers;
using Infrastructure.Services.Repositories;
using Infrastructure.Services.ServerUpdate;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTests;

public class RoatanMultiplayerTableAndAuthTests
{
    private static ClientMock CreateLoggedInClient(int clientId, long userId, string username, int score = 1000)
    {
        return new ClientMock
        {
            Id = clientId,
            IsLoggedIn = true,
            User = new User($"{username.ToLowerInvariant()}@test.com", username)
            {
                Id = userId,
                Score = score
            }
        };
    }

    private static LobbyManager CreateLobbyManager()
    {
        return new LobbyManager(new TimerManager(), new CodeGenerator(new RandomGenerator()), new ChatManager());
    }

    private static ApplicationDbContext CreateInMemoryDb(string name)
    {
        DbContextOptions<ApplicationDbContext> opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new ApplicationDbContext(opts);
    }

    [Fact]
    public void HouseTable_IsMaintainedOnStartup_AndRespawnsWhenClosed()
    {
        LobbyManager lobbyManager = CreateLobbyManager();
        lobbyManager.MaintainHouseTable = true;
        Lobby house = lobbyManager.EnsureHouseTable();

        Assert.Equal("Roatan House Table", house.Name);
        Assert.Equal("House", house.Creator.Login);
        Assert.Equal(MatchFormat.SIX_LOV, house.MatchFormat);
        Assert.Equal(TableStyle.DRAW, house.TableStyle);
        Assert.Equal(4, house.MaxNumberOfPlayer);
        Assert.True(house.IsPublic);
        Assert.True(house.IsHouseTable);

        // Player joins and leaves -> House table stays open and resets
        ClientMock p1 = CreateLoggedInClient(1, 101, "10squidy");
        lobbyManager.AddPlayerToLobby(p1, house.Id);
        Assert.Single(house.PlayersInLobby);

        lobbyManager.RemovePlayerFromLobby(p1, house.Id);
        Assert.True(lobbyManager.ExistById(house.Id));
        Assert.Empty(house.PlayersInLobby);

        // If house table launches & is deleted, a new persistent house table spawns automatically
        lobbyManager.DeleteLobby(house.Id);
        List<Lobby> publicTables = lobbyManager.GetAllPublicLobbies();
        Assert.Single(publicTables);
        Assert.Equal("Roatan House Table", publicTables[0].Name);
        Assert.Equal("House", publicTables[0].Creator.Login);
    }

    [Fact]
    public async Task TwoPlayerTable_StrictlyCapsAtTwoSeats_AndRejectsThirdPlayerWithFriendlyMessage()
    {
        LobbyManager lobbyManager = CreateLobbyManager();
        ClientMock p1 = CreateLoggedInClient(1, 10, "10squidy");
        ClientMock p2 = CreateLoggedInClient(2, 11, "PadnaTwo");
        ClientMock p3 = CreateLoggedInClient(3, 12, "ThirdWheel");

        Lobby table = lobbyManager.CreateLobby(p1, 2, true, "1v1 Roatan", tableStyle: TableStyle.BLOCK, matchFormat: MatchFormat.TWO_BEST);
        Assert.Equal(2, table.MaxNumberOfPlayer);

        JoinLobbyCommandHandler joinHandler = new(lobbyManager);
        await joinHandler.Handle(new JoinLobbyCommand(p2, table.Id), CancellationToken.None);
        Assert.Equal(2, table.PlayersInLobby.Count);
        Assert.True(table.IsFull());

        MaxNumberOfPlayerReachedException ex = await Assert.ThrowsAsync<MaxNumberOfPlayerReachedException>(
            () => joinHandler.Handle(new JoinLobbyCommand(p3, table.Id), CancellationToken.None));
        Assert.Equal("This table is full (2/2 players).", ex.Message);

        // Verify DTO carries host, mode, seated count, and seated player names
        var lobbiesDto = DtoBuilder.GetLobbiesInfoDto(lobbyManager.GetAllPublicLobbies());
        LobbyInfoDto info = lobbiesDto.Object.Lobbies.Single();
        Assert.Equal("10squidy", info.Creator);
        Assert.Equal(2, info.NumberOfPlayer);
        Assert.Equal(2, info.MaxNumberOfPlayer);
        Assert.Equal(["10squidy", "PadnaTwo"], info.SeatedPlayers);
    }

    [Fact]
    public void SeatSwitching_AndHostBotControls_PairFriendsAsPartnersWithTwoBotsInSixLov()
    {
        LobbyManager lobbyManager = CreateLobbyManager();
        ClientMock host = CreateLoggedInClient(1, 20, "HostFriend");
        ClientMock friend = CreateLoggedInClient(2, 21, "SecondFriend");

        Lobby table = lobbyManager.CreateLobby(host, 4, true, "Island Padnas", tableStyle: TableStyle.DRAW, matchFormat: MatchFormat.SIX_LOV);
        lobbyManager.AddPlayerToLobby(friend, table.Id); // Auto-seated at Seat 1 (Opponent)

        Assert.Equal(0, table.PlayersInLobby.First(p => p.Id == host.User!.Id).SeatIndex);
        Assert.Equal(1, table.PlayersInLobby.First(p => p.Id == friend.User!.Id).SeatIndex);

        // Friend switches from Seat 1 to Seat 2 (across from Host = Partner / Padna!)
        Assert.True(lobbyManager.SwitchSeat(friend, table.Id, 2));
        Assert.Equal(2, table.PlayersInLobby.First(p => p.Id == friend.User!.Id).SeatIndex);

        // Non-host cannot add bots
        Assert.Null(lobbyManager.AddBotToLobby(friend, table.Id, 1, "Pro"));

        // Host adds Master bot to Seat 1 and Novice bot to Seat 3
        LobbyPlayer? bot1 = lobbyManager.AddBotToLobby(host, table.Id, 1, "Master");
        LobbyPlayer? bot2 = lobbyManager.AddBotToLobby(host, table.Id, 3, "Novice");
        Assert.NotNull(bot1);
        Assert.NotNull(bot2);
        Assert.Equal(4, table.PlayersInLobby.Count);
        Assert.True(table.IsFull());

        // Host can remove and re-add bot cleanly
        Assert.True(lobbyManager.RemoveBotFromLobby(host, table.Id, 3));
        Assert.Equal(3, table.PlayersInLobby.Count);
        LobbyPlayer? bot2Replacement = lobbyManager.AddBotToLobby(host, table.Id, 3, "Pro");
        Assert.NotNull(bot2Replacement);

        // Launch Game from Lobby and verify 2 Friends are Team 0 (Partners) & 2 Bots are Team 1 (Ready & BotControlled)
        GameManager gameManager = new(new ChatManager());
        gameManager.CreateGameFromLobby(table, seed: 42);
        Game game = gameManager.GetById(table.Id)!;

        Assert.Equal(4, game.Players.Count);
        Assert.True(game.IsTeam2v2);
        Assert.Equal(host.User!.Id, game.Players[0].Id);
        Assert.Equal(friend.User!.Id, game.Players[2].Id);
        Assert.Equal(game.Players[0].TeamId, game.Players[2].TeamId); // Partners!
        Assert.True(game.Players[1].IsPermanentlyBot);
        Assert.True(game.Players[1].IsReady);
        Assert.True(game.Players[3].IsPermanentlyBot);
        Assert.True(game.Players[3].IsReady);
        Assert.Equal("Master", game.Players[1].BotDifficulty);
        Assert.Equal("Pro", game.Players[3].BotDifficulty);
    }

    [Fact]
    public async Task AuthValidation_EnforcesPlainEnglishRules_AndAcceptsMmDdYyyyAndSpecialChars()
    {
        await using ApplicationDbContext db = CreateInMemoryDb(nameof(AuthValidation_EnforcesPlainEnglishRules_AndAcceptsMmDdYyyyAndSpecialChars));
        UserRepository repo = new(db, new NationalityRepository(db));
        CreateUserCommandHandler handler = new(repo);

        // Invalid email
        InvalidEmailFormatException emailEx = await Assert.ThrowsAsync<InvalidEmailFormatException>(() =>
            handler.Handle(new CreateUserCommand { Email = "bad-email", Login = "Roatan1", Password = "Password!1", Birthdate = "05/21/1995" }, CancellationToken.None));
        Assert.Equal("Please enter a valid email address.", emailEx.Message);

        // Invalid username (too short)
        LoginTooLongException userEx = await Assert.ThrowsAsync<LoginTooLongException>(() =>
            handler.Handle(new CreateUserCommand { Email = "good@roatan.com", Login = "ab", Password = "Password!1", Birthdate = "05/21/1995" }, CancellationToken.None));
        Assert.Equal("Username must be 3–16 letters or numbers.", userEx.Message);

        // Missing capital letter
        InvalidPasswordFormatException passEx = await Assert.ThrowsAsync<InvalidPasswordFormatException>(() =>
            handler.Handle(new CreateUserCommand { Email = "good@roatan.com", Login = "Roatan1", Password = "password123", Birthdate = "05/21/1995" }, CancellationToken.None));
        Assert.Contains("capital letter (A-Z)", passEx.Message);

        // Valid with capital + special character + MM/DD/YYYY birthdate
        await handler.Handle(new CreateUserCommand { Email = "good@roatan.com", Login = "Roatan1", Password = "Island!Pass", Birthdate = "05/21/1995" }, CancellationToken.None);
        Assert.True(await repo.ExistsByLogin("Roatan1"));
        Assert.NotNull(await repo.Login("good@roatan.com", "Island!Pass")); // Email login supported too
    }

    [Fact]
    public async Task SocialAuth_AndPresenceRoster_WorkEndToEnd()
    {
        await using ApplicationDbContext db = CreateInMemoryDb(nameof(SocialAuth_AndPresenceRoster_WorkEndToEnd));
        UserRepository userRepo = new(db, new NationalityRepository(db));
        ClientManagerMock clientManager = new();
        TokenManager tokenManager = new();
        ChatManager chatManager = new();
        LobbyManager lobbyManager = CreateLobbyManager();
        GameManager gameManager = new(chatManager);

        OAuthStateStore oauthStore = new();
        SocialAuthService socialAuth = new(userRepo, clientManager, tokenManager, chatManager, oauthStore, NullLogger<SocialAuthService>.Instance);

        // Create one offline registered user
        await userRepo.Create(new User("offline@roatan.com", "OfflineUser"), "ValidPass!1", new DateOnly(1998, 4, 10));

        // Sign in via Google OAuth one-time code
        OAuthPendingSession pending1 = oauthStore.CreatePending("Google");
        Assert.True(oauthStore.TryComplete(
            pending1.State,
            providerUserId: "goog_abc123",
            email: "island.google@example.com",
            displayName: "IslandGoogle",
            avatarUrl: "https://lh3.googleusercontent.com/island-avatar=s128-c",
            avatarBase64: null,
            out string code1));

        ClientMock googleClient = new() { Id = 50 };
        clientManager.HandleNewClient(googleClient);
        var (gUser, gToken) = await socialAuth.SignInClientAsync(new SocialAuthDto
        {
            Provider = "Google",
            OneTimeCode = code1
        }, googleClient);

        Assert.NotEqual(Guid.Empty, gToken);
        Assert.True(googleClient.IsLoggedIn);
        Assert.Equal("IslandGoogle", gUser.Login);

        // Repeat OAuth lookup links same user cleanly
        OAuthPendingSession pending2 = oauthStore.CreatePending("Google");
        Assert.True(oauthStore.TryComplete(
            pending2.State,
            providerUserId: "goog_abc123",
            email: "island.google@example.com",
            displayName: "IslandGoogle",
            avatarUrl: null,
            avatarBase64: null,
            out string code2));
        User linkedAgain = await socialAuth.AuthenticateProfileAsync(new SocialAuthDto
        {
            Provider = "Google",
            OneTimeCode = code2
        });
        Assert.Equal(gUser.Id, linkedAgain.Id);

        // Put Google user at a table and verify PresenceService reports Online (At Table) & Offline user!
        Lobby table = lobbyManager.CreateLobby(googleClient, 4, true, "West Bay Table", tableStyle: TableStyle.DRAW, matchFormat: MatchFormat.SIX_LOV);
        PresenceService presence = new(userRepo, clientManager, lobbyManager, gameManager);
        PlayerRosterDto roster = await presence.BuildRosterAsync();

        PlayerPresenceDto onlineEntry = roster.Players.Single(p => p.Username == "IslandGoogle");
        PlayerPresenceDto offlineEntry = roster.Players.Single(p => p.Username == "OfflineUser");

        Assert.True(onlineEntry.IsOnline);
        Assert.Equal("At Table", onlineEntry.Status);
        Assert.Equal(table.Name, onlineEntry.TableName);

        Assert.False(offlineEntry.IsOnline);
        Assert.Equal("Offline", offlineEntry.Status);
    }

    [Fact]
    public async Task DuplicateEmailAndUsername_AreRejectedCaseInsensitively_WithExactMessages()
    {
        await using ApplicationDbContext db = CreateInMemoryDb(nameof(DuplicateEmailAndUsername_AreRejectedCaseInsensitively_WithExactMessages));
        UserRepository repo = new(db, new NationalityRepository(db));
        CreateUserCommandHandler handler = new(repo);

        await handler.Handle(new CreateUserCommand
        {
            Email = "IslandKing@Roatan.com",
            Login = "RoatanKing",
            Password = "Password!1",
            Birthdate = "04/15/1994"
        }, CancellationToken.None);

        // Case-insensitive duplicate email must be rejected
        UserAlreadyExistException emailEx = await Assert.ThrowsAsync<UserAlreadyExistException>(() =>
            handler.Handle(new CreateUserCommand
            {
                Email = "islandking@roatan.com",
                Login = "DifferentName",
                Password = "Password!1",
                Birthdate = "04/15/1994"
            }, CancellationToken.None));
        Assert.Equal("An account with this email address already exists. Please sign in instead.", emailEx.Message);

        // Case-insensitive duplicate username must be rejected
        UserAlreadyExistException userEx = await Assert.ThrowsAsync<UserAlreadyExistException>(() =>
            handler.Handle(new CreateUserCommand
            {
                Email = "another@roatan.com",
                Login = "roatanking",
                Password = "Password!1",
                Birthdate = "04/15/1994"
            }, CancellationToken.None));
        Assert.Equal("That username is already taken. Please choose another username.", userEx.Message);
    }

    [Fact]
    public async Task ProfileAvatar_PersistsUnder32KB_RejectsOversized_AndPropagatesToTableAndRoster()
    {
        await using ApplicationDbContext db = CreateInMemoryDb(nameof(ProfileAvatar_PersistsUnder32KB_RejectsOversized_AndPropagatesToTableAndRoster));
        UserRepository userRepo = new(db, new NationalityRepository(db));
        ClientManagerMock clientManager = new();
        LobbyManager lobbyManager = CreateLobbyManager();
        GameManager gameManager = new(new ChatManager());

        await userRepo.Create(new User("avatar@roatan.com", "AvatarPro"), "ValidPass!1", new DateOnly(1992, 6, 12));
        User created = (await userRepo.GetByEmail("avatar@roatan.com"))!;

        // Valid compressed thumbnail (2 KB <= 32 KB)
        byte[] smallBytes = Enumerable.Range(0, 2048).Select(i => (byte)(i % 251)).ToArray();
        string smallBase64 = Convert.ToBase64String(smallBytes);

        User updated = await userRepo.UpdateAvatar(created.Id, smallBase64, "https://lh3.googleusercontent.com/photo.jpg");
        Assert.Equal(smallBase64, updated.AvatarBase64);
        Assert.Equal("https://lh3.googleusercontent.com/photo.jpg", updated.AvatarUrl);

        // Reload from DB and verify persistence
        User reloaded = (await userRepo.GetById(created.Id))!;
        Assert.Equal(smallBase64, reloaded.AvatarBase64);
        Assert.Equal("https://lh3.googleusercontent.com/photo.jpg", reloaded.AvatarUrl);

        // Oversized (> 32 KB) must be rejected
        byte[] tooBigBytes = new byte[40 * 1024];
        string tooBigBase64 = Convert.ToBase64String(tooBigBytes);
        await Assert.ThrowsAnyAsync<BaseApplicationException>(() =>
            userRepo.UpdateAvatar(created.Id, tooBigBase64, null));

        // Verify avatar propagates to UserDto, LobbyDto seat, and PlayerPresenceDto
        UserDto userDto = DtoBuilder.GetUserDto(reloaded);
        Assert.Equal(smallBase64, userDto.AvatarBase64);
        Assert.Equal("https://lh3.googleusercontent.com/photo.jpg", userDto.AvatarUrl);

        ClientMock client = new() { Id = 77, IsLoggedIn = true, User = reloaded };
        clientManager.HandleNewClient(client);
        Lobby table = lobbyManager.CreateLobby(client, 4, true, "Avatar Table");
        LobbyDto tableDto = DtoBuilder.GetLobbyDto(table).Object;
        Assert.Equal(smallBase64, tableDto.Seats[0].Player.AvatarBase64);

        PresenceService presence = new(userRepo, clientManager, lobbyManager, gameManager);
        PlayerRosterDto roster = await presence.BuildRosterAsync();
        Assert.Equal(smallBase64, roster.Players.Single(p => p.UserId == reloaded.Id).AvatarBase64);
    }

    [Fact]
    public async Task DeleteAccount_DeletesFromDatabase_RemovesFromActiveTable_AndClearsRoster()
    {
        await using ApplicationDbContext db = CreateInMemoryDb(nameof(DeleteAccount_DeletesFromDatabase_RemovesFromActiveTable_AndClearsRoster));
        UserRepository userRepo = new(db, new NationalityRepository(db));
        ClientManagerMock clientManager = new();
        LobbyManager lobbyManager = CreateLobbyManager();
        GameManager gameManager = new(new ChatManager());
        TokenManager tokenManager = new();
        ChatManager chatManager = new();

        await userRepo.Create(new User("delete.me@roatan.com", "DeleteMe"), "ValidPass!1", new DateOnly(1995, 2, 14));
        User user = (await userRepo.GetByEmail("delete.me@roatan.com"))!;

        ClientMock client = new() { Id = 88, IsLoggedIn = true, User = user };
        clientManager.HandleNewClient(client);
        tokenManager.GenerateTokenForClient(client);
        Lobby table = lobbyManager.CreateLobby(client, 4, true, "Temp Table");
        Assert.NotNull(lobbyManager.GetLobbyForClient(client));

        // Delete user cleanly (mirroring DeleteAccountCallback + DELETE /auth/account)
        lobbyManager.RemovePlayerFromLobby(client, table.Id);
        tokenManager.RemoveTokenForClientIfPresent(client);
        client.IsLoggedIn = false;
        client.User = null;

        bool deleted = await userRepo.DeleteUser(user.Id);
        Assert.True(deleted);
        Assert.False(await userRepo.ExistsByEmail("delete.me@roatan.com"));
        Assert.False(await userRepo.ExistsByLogin("DeleteMe"));
        Assert.Null(lobbyManager.GetLobbyForClient(client));

        PresenceService presence = new(userRepo, clientManager, lobbyManager, gameManager);
        PlayerRosterDto roster = await presence.BuildRosterAsync();
        Assert.DoesNotContain(roster.Players, p => p.UserId == user.Id);
    }

    [Fact]
    public async Task OAuthOneTimeCode_RedeemsRealProfileWithAvatar_AndRejectsUnconfiguredManualStubLogin()
    {
        await using ApplicationDbContext db = CreateInMemoryDb(nameof(OAuthOneTimeCode_RedeemsRealProfileWithAvatar_AndRejectsUnconfiguredManualStubLogin));
        UserRepository userRepo = new(db, new NationalityRepository(db));
        ClientManagerMock clientManager = new();
        TokenManager tokenManager = new();
        ChatManager chatManager = new();
        OAuthStateStore oauthStore = new();

        string? prevGoogleId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
        string? prevGoogleLegacy = Environment.GetEnvironmentVariable("GOOGLE_OAUTH_CLIENT_ID");
        string? prevGoogleSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET");
        string? prevFbId = Environment.GetEnvironmentVariable("FACEBOOK_APP_ID");
        string? prevFbSecret = Environment.GetEnvironmentVariable("FACEBOOK_APP_SECRET");

        Environment.SetEnvironmentVariable("GOOGLE_CLIENT_ID", null);
        Environment.SetEnvironmentVariable("GOOGLE_OAUTH_CLIENT_ID", null);
        Environment.SetEnvironmentVariable("GOOGLE_CLIENT_SECRET", null);
        Environment.SetEnvironmentVariable("FACEBOOK_APP_ID", null);
        Environment.SetEnvironmentVariable("FACEBOOK_APP_SECRET", null);

        try
        {
            SocialAuthService socialAuth = new(userRepo, clientManager, tokenManager, chatManager, oauthStore, NullLogger<SocialAuthService>.Instance);

            // Unconfigured OAuth start returns clear setup guidance (no manual email fallback)
            OAuthStartResult missingGoogle = socialAuth.StartOAuthFlow("Google", "https://domino.roatandomino.com");
            Assert.False(missingGoogle.Configured);
            Assert.Equal("Google Sign-In is not configured on this server yet — add GOOGLE_CLIENT_ID to .env", missingGoogle.Message);

            OAuthStartResult missingFb = socialAuth.StartOAuthFlow("Facebook", "https://domino.roatandomino.com");
            Assert.False(missingFb.Configured);
            Assert.Equal("Facebook Sign-In is not configured on this server yet — add FACEBOOK_APP_ID to .env", missingFb.Message);

            // Unverified manual email/stub social login must be rejected with clear setup error
            WrongPasswordException stubEx = await Assert.ThrowsAsync<WrongPasswordException>(() =>
                socialAuth.AuthenticateProfileAsync(new SocialAuthDto
                {
                    Provider = "Google",
                    Email = "typed@gmail.com",
                    DisplayName = "GooglePlayer"
                }));
            Assert.Equal("Google Sign-In is not configured on this server yet — add GOOGLE_CLIENT_ID to .env", stubEx.Message);

            // Real OAuth one-time code flow creates user with real email, username, AuthProvider, and AvatarUrl
            OAuthPendingSession pending = oauthStore.CreatePending("Google");
            bool completedOk = oauthStore.TryComplete(
                pending.State,
                providerUserId: "google-sub-999",
                email: "real.player@gmail.com",
                displayName: "RealIslandStar",
                avatarUrl: "https://lh3.googleusercontent.com/real-avatar.jpg",
                avatarBase64: null,
                out string oneTimeCode);
            Assert.True(completedOk);
            Assert.False(string.IsNullOrWhiteSpace(oneTimeCode));

            User authed = await socialAuth.AuthenticateProfileAsync(new SocialAuthDto
            {
                Provider = "Google",
                OneTimeCode = oneTimeCode
            });
            Assert.Equal("real.player@gmail.com", authed.Email);
            Assert.Equal("RealIslandStar", authed.Login);
            Assert.Equal("Google", authed.AuthProvider);
            Assert.Equal("https://lh3.googleusercontent.com/real-avatar.jpg", authed.AvatarUrl);

            // Reusing a single-use OAuth code must fail
            await Assert.ThrowsAnyAsync<BaseApplicationException>(() =>
                socialAuth.AuthenticateProfileAsync(new SocialAuthDto
                {
                    Provider = "Google",
                    OneTimeCode = oneTimeCode
                }));
        }
        finally
        {
            Environment.SetEnvironmentVariable("GOOGLE_CLIENT_ID", prevGoogleId);
            Environment.SetEnvironmentVariable("GOOGLE_OAUTH_CLIENT_ID", prevGoogleLegacy);
            Environment.SetEnvironmentVariable("GOOGLE_CLIENT_SECRET", prevGoogleSecret);
            Environment.SetEnvironmentVariable("FACEBOOK_APP_ID", prevFbId);
            Environment.SetEnvironmentVariable("FACEBOOK_APP_SECRET", prevFbSecret);
        }
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request));
        }
    }

    [Fact]
    public async Task GoogleIdToken_AndFacebookOAuthCallback_VerifyProfile_AndAutoImportCompressedAvatar()
    {
        await using ApplicationDbContext db = CreateInMemoryDb(nameof(GoogleIdToken_AndFacebookOAuthCallback_VerifyProfile_AndAutoImportCompressedAvatar));
        UserRepository userRepo = new(db, new NationalityRepository(db));
        ClientManagerMock clientManager = new();
        TokenManager tokenManager = new();
        ChatManager chatManager = new();
        OAuthStateStore oauthStore = new();

        string? prevGoogleId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
        string? prevGoogleSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET");
        string? prevFbId = Environment.GetEnvironmentVariable("FACEBOOK_APP_ID");
        string? prevFbSecret = Environment.GetEnvironmentVariable("FACEBOOK_APP_SECRET");
        string? prevFbScopes = Environment.GetEnvironmentVariable("FACEBOOK_SCOPES");

        Environment.SetEnvironmentVariable("GOOGLE_CLIENT_ID", "test-google-client.apps.googleusercontent.com");
        Environment.SetEnvironmentVariable("GOOGLE_CLIENT_SECRET", "test-google-secret");
        Environment.SetEnvironmentVariable("FACEBOOK_APP_ID", "1234567890");
        Environment.SetEnvironmentVariable("FACEBOOK_APP_SECRET", "fb-secret-xyz");
        Environment.SetEnvironmentVariable("FACEBOOK_SCOPES", null);

        try
        {
            // Synthetic JPEG with oversized 36 KB EXIF APP1 block + tiny 64-byte image scan -> strips cleanly under 32 KB!
            List<byte> fakeJpeg = [0xFF, 0xD8, 0xFF, 0xE1, 0x90, 0x02]; // SOI + APP1 length 0x9002 (36,866 bytes)
            fakeJpeg.AddRange(new byte[0x9000]);
            fakeJpeg.AddRange([0xFF, 0xDA, 0x00, 0x08, 0x01, 0x02, 0x03, 0x04, 0xFF, 0xD9]);
            byte[] oversizedExifJpeg = fakeJpeg.ToArray();
            Assert.True(oversizedExifJpeg.Length > UserRepository.MaxAvatarBytes);

            byte[] smallFbPng = Enumerable.Range(0, 1500).Select(i => (byte)(i % 251)).ToArray();

            using HttpClient mockHttp = new(new StubHttpMessageHandler(req =>
            {
                string url = req.RequestUri!.ToString();
                if (url.StartsWith("https://oauth2.googleapis.com/tokeninfo", StringComparison.Ordinal))
                {
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new StringContent("""
                        {
                          "aud": "test-google-client.apps.googleusercontent.com",
                          "sub": "google-uid-42",
                          "email": "island.queen@gmail.com",
                          "name": "IslandQueen",
                          "picture": "https://lh3.googleusercontent.com/a/avatar-photo=s400-c",
                          "exp": "4102444800"
                        }
                        """, System.Text.Encoding.UTF8, "application/json")
                    };
                }
                if (url.Contains("lh3.googleusercontent.com/a/avatar-photo=s128-c", StringComparison.Ordinal))
                {
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(oversizedExifJpeg)
                    };
                }
                if (url.StartsWith("https://graph.facebook.com/v19.0/oauth/access_token", StringComparison.Ordinal))
                {
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new StringContent("""{"access_token":"fb-live-token-77"}""", System.Text.Encoding.UTF8, "application/json")
                    };
                }
                if (url.StartsWith("https://graph.facebook.com/v19.0/debug_token", StringComparison.Ordinal))
                {
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new StringContent("""{"data":{"is_valid":true}}""", System.Text.Encoding.UTF8, "application/json")
                    };
                }
                if (url.Contains("graph.facebook.com/v19.0/me?fields=id,name,email,picture.type(large)", StringComparison.Ordinal))
                {
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new StringContent("""
                        {
                          "id": "fb-uid-99",
                          "name": "Roatan Captain",
                          "picture": { "data": { "url": "https://platform-lookaside.fbsbx.com/fb-large.jpg" } }
                        }
                        """, System.Text.Encoding.UTF8, "application/json")
                    };
                }
                if (url == "https://platform-lookaside.fbsbx.com/fb-large.jpg")
                {
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(smallFbPng)
                    };
                }
                return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
            }));

            SocialAuthService socialAuth = new(userRepo, clientManager, tokenManager, chatManager, oauthStore, NullLogger<SocialAuthService>.Instance, mockHttp);

            // 1. Google Start URL includes prompt=select_account and optional login_hint
            OAuthStartResult googleStart = socialAuth.StartOAuthFlow("Google", "https://domino.roatandomino.com", loginHint: "island.queen@gmail.com");
            Assert.True(googleStart.Configured);
            Assert.Contains("prompt=select_account", googleStart.AuthorizeUrl);
            Assert.Contains("login_hint=island.queen%40gmail.com", googleStart.AuthorizeUrl);

            // 2. Google native ID token endpoint verifies token, strips EXIF to fit <= 32KB, persists user & issues one-time code
            var (googleUser, googleCode) = await socialAuth.VerifyGoogleIdTokenAndCreateSessionAsync("valid-google-id-token");
            Assert.Equal("island.queen@gmail.com", googleUser.Email);
            Assert.Equal("IslandQueen", googleUser.Login);
            Assert.Equal("Google", googleUser.AuthProvider);
            Assert.False(string.IsNullOrWhiteSpace(googleUser.AvatarBase64));
            Assert.True(Convert.FromBase64String(googleUser.AvatarBase64!).Length <= UserRepository.MaxAvatarBytes);
            Assert.False(string.IsNullOrWhiteSpace(googleCode));

            ClientMock gClient = new() { Id = 601 };
            clientManager.HandleNewClient(gClient);
            var (signedInGoogle, gTok) = await socialAuth.SignInClientAsync(new SocialAuthDto { Provider = "Google", OneTimeCode = googleCode }, gClient);
            Assert.NotEqual(Guid.Empty, gTok);
            Assert.Equal(googleUser.Id, signedInGoogle.Id);

            // 3. Facebook Start URL defaults to scope=public_profile (avoids Invalid Scopes: email) and honors optional FACEBOOK_SCOPES override
            OAuthStartResult fbStart = socialAuth.StartOAuthFlow("Facebook", "https://domino.roatandomino.com");
            Assert.True(fbStart.Configured);
            Assert.Contains("scope=public_profile", fbStart.AuthorizeUrl);
            Assert.DoesNotContain("email", fbStart.AuthorizeUrl);
            Assert.Contains("display=popup", fbStart.AuthorizeUrl);
            Assert.Contains("auth_type=reauthenticate", fbStart.AuthorizeUrl);

            Environment.SetEnvironmentVariable("FACEBOOK_SCOPES", "public_profile,email");
            OAuthStartResult fbStartOverride = socialAuth.StartOAuthFlow("Facebook", "https://domino.roatandomino.com");
            Assert.True(fbStartOverride.Configured);
            Assert.Contains("scope=public_profile%2Cemail", fbStartOverride.AuthorizeUrl);
            Environment.SetEnvironmentVariable("FACEBOOK_SCOPES", null);

            // 4. Facebook Callback exchanges code, synthesizes fb_{id}@facebook.local when email is absent, imports picture.type(large) into AvatarBase64, and completes login
            var (fbOk, fbCode, fbProvider, fbErr) = await socialAuth.CompleteOAuthCallbackAsync("Facebook", "fb-auth-code", fbStart.State, "https://domino.roatandomino.com");
            Assert.True(fbOk, fbErr);
            Assert.Equal("Facebook", fbProvider);
            Assert.False(string.IsNullOrWhiteSpace(fbCode));

            User fbUser = await socialAuth.AuthenticateProfileAsync(new SocialAuthDto { Provider = "Facebook", OneTimeCode = fbCode });
            Assert.Equal("fb_fb-uid-99@facebook.local", fbUser.Email);
            Assert.Equal("RoatanCaptain", fbUser.Login);
            Assert.Equal("Facebook", fbUser.AuthProvider);
            Assert.Equal(Convert.ToBase64String(smallFbPng), fbUser.AvatarBase64);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GOOGLE_CLIENT_ID", prevGoogleId);
            Environment.SetEnvironmentVariable("GOOGLE_CLIENT_SECRET", prevGoogleSecret);
            Environment.SetEnvironmentVariable("FACEBOOK_APP_ID", prevFbId);
            Environment.SetEnvironmentVariable("FACEBOOK_APP_SECRET", prevFbSecret);
            Environment.SetEnvironmentVariable("FACEBOOK_SCOPES", prevFbScopes);
        }
    }

    [Fact]
    public async Task OnlineTableStart_RequiresMinimumTwoRealHumans_AndImmediatelyDealsToHumansAndBots()
    {
        ServiceCollection services = [];
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = $"InMemory:{nameof(OnlineTableStart_RequiresMinimumTwoRealHumans_AndImmediatelyDealsToHumansAndBots)}"
            })
            .Build();
        services.AddLogging();
        services.AddScoped<IMessageDispatcher, MessageDispatcher>();
        services.AddInfrastructure(config);
        services.AddApplication();

        using ServiceProvider provider = services.BuildServiceProvider();
        ILobbyManager lobbyManager = provider.GetRequiredService<ILobbyManager>();
        IGameManager gameManager = provider.GetRequiredService<IGameManager>();
        IClientManager clientManager = provider.GetRequiredService<IClientManager>();
        IMessageFrameHandler frameHandler = provider.GetRequiredService<IMessageFrameHandler>();

        ClientMock host = CreateLoggedInClient(1, 501, "HostHuman");
        ClientMock friend = CreateLoggedInClient(2, 502, "FriendHuman");
        clientManager.HandleNewClient(host);
        clientManager.HandleNewClient(friend);

        // 1 Human + 3 Bots -> MUST be rejected (< 2 real humans)
        Lobby table = lobbyManager.CreateLobby(host, 4, true, "Two Human Rule Table", tableStyle: TableStyle.DRAW, matchFormat: MatchFormat.SIX_LOV);
        Assert.NotNull(lobbyManager.AddBotToLobby(host, table.Id, 1, "Pro"));
        Assert.NotNull(lobbyManager.AddBotToLobby(host, table.Id, 2, "Pro"));
        Assert.NotNull(lobbyManager.AddBotToLobby(host, table.Id, 3, "Pro"));
        Assert.True(table.IsFull());

        using (IServiceScope scope = provider.CreateScope())
        {
            LobbyFrameHandlers lobbyHandlers = scope.ServiceProvider.GetRequiredService<LobbyFrameHandlers>();
            GameFrameHandlers gameHandlers = scope.ServiceProvider.GetRequiredService<GameFrameHandlers>();
            lobbyHandlers.RegisterHandlers(frameHandler);
            gameHandlers.RegisterHandlers(frameHandler);

            await frameHandler.HandleAsync(MessageFrameBuilder.StartTableMatchFrame(), host);

            ResponseDto soloReject = host.FramesSended
                .SelectMany(nf => nf.Frames)
                .Where(f => f.SubCommandType == FrameCommandType.StartTableMatch)
                .Select(f => f.GetObjectFromMessage<ResponseDto>())
                .Last();
            Assert.False(soloReject.OkResult);
            Assert.Contains("Need at least 2 real players to start online (1/2 humans seated)", soloReject.ErrorMessage);
            Assert.Null(gameManager.GetById(table.Id));

            // Now remove 1 bot and seat 2nd real human (2 Humans + 2 Bots)
            Assert.True(lobbyManager.RemoveBotFromLobby(host, table.Id, 2));
            lobbyManager.AddPlayerToLobby(friend, table.Id);
            Assert.Equal(2, table.GetHumanPlayers().Count);
            Assert.True(table.IsFull());

            host.FramesSended.Clear();
            friend.FramesSended.Clear();

            // Host starts match -> game must exist BEFORE LobbyTimerExpired and immediately deal once both humans send ReadyInGame
            await frameHandler.HandleAsync(MessageFrameBuilder.StartTableMatchFrame(), host);

            Assert.NotNull(gameManager.GetById(table.Id));
            Assert.Contains(host.FramesSended.SelectMany(nf => nf.Frames), f => f.CommandType == FrameCommandType.LobbyTimerExpired);
            Assert.Contains(friend.FramesSended.SelectMany(nf => nf.Frames), f => f.CommandType == FrameCommandType.LobbyTimerExpired);

            // Both humans send ReadyInGame -> server immediately deals hands without hanging!
            await frameHandler.HandleAsync(MessageFrameBuilder.ReadyInGameFrame(), host);
            await frameHandler.HandleAsync(MessageFrameBuilder.ReadyInGameFrame(), friend);
        }

        List<MessageFrame> hostFrames = host.FramesSended.SelectMany(nf => nf.Frames).ToList();
        List<MessageFrame> friendFrames = friend.FramesSended.SelectMany(nf => nf.Frames).ToList();
        MessageFrame? hostHandFrame = hostFrames.FirstOrDefault(f => f.CommandType == FrameCommandType.DominoDistribution);
        MessageFrame? friendHandFrame = friendFrames.FirstOrDefault(f => f.CommandType == FrameCommandType.DominoDistribution);
        Assert.NotNull(hostHandFrame);
        Assert.NotNull(friendHandFrame);
        Assert.InRange(hostHandFrame!.GetObjectFromMessage<HandDto>().Dominoes.Count, 6, 7);
        Assert.InRange(friendHandFrame!.GetObjectFromMessage<HandDto>().Dominoes.Count, 6, 7);
        Assert.Contains(hostFrames, f => f.CommandType == FrameCommandType.PlayerPlays);
        Assert.Contains(hostFrames, f => f.CommandType == FrameCommandType.ChangePlayerTurn);
    }
}
