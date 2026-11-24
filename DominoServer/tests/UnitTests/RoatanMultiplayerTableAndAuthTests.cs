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
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;

using Infrastructure.Database;
using Infrastructure.Services;
using Infrastructure.Services.Managers;
using Infrastructure.Services.Repositories;

using Microsoft.EntityFrameworkCore;
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

        // Ensure no OAuth env vars interfere with local/tunnel test mode
        string? prevGoogle = Environment.GetEnvironmentVariable("GOOGLE_OAUTH_CLIENT_ID");
        string? prevFb = Environment.GetEnvironmentVariable("FACEBOOK_APP_ID");
        Environment.SetEnvironmentVariable("GOOGLE_OAUTH_CLIENT_ID", null);
        Environment.SetEnvironmentVariable("FACEBOOK_APP_ID", null);

        try
        {
            SocialAuthService socialAuth = new(userRepo, clientManager, tokenManager, chatManager, NullLogger<SocialAuthService>.Instance);

            // Create one offline registered user
            await userRepo.Create(new User("offline@roatan.com", "OfflineUser"), "ValidPass!1", new DateOnly(1998, 4, 10));

            // Sign in via Google social auth
            ClientMock googleClient = new() { Id = 50 };
            clientManager.HandleNewClient(googleClient);
            var (gUser, gToken) = await socialAuth.SignInClientAsync(new SocialAuthDto
            {
                Provider = "Google",
                ProviderUserId = "goog_abc123",
                Email = "island.google@example.com",
                DisplayName = "IslandGoogle"
            }, googleClient);

            Assert.NotEqual(Guid.Empty, gToken);
            Assert.True(googleClient.IsLoggedIn);
            Assert.Equal("IslandGoogle", gUser.Login);

            // Repeat lookup links same user cleanly
            User linkedAgain = await socialAuth.AuthenticateProfileAsync(new SocialAuthDto
            {
                Provider = "Google",
                ProviderUserId = "goog_abc123",
                Email = "island.google@example.com"
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
        finally
        {
            Environment.SetEnvironmentVariable("GOOGLE_OAUTH_CLIENT_ID", prevGoogle);
            Environment.SetEnvironmentVariable("FACEBOOK_APP_ID", prevFb);
        }
    }
}
