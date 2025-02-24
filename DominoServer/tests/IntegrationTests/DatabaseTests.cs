using Domain;
using Domain.Game;

using DominoCommon.com.domino.common.Scripts.Enums;

using Infrastructure.Database;
using Infrastructure.Database.Entities;
using Infrastructure.Services.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NFluent;

namespace IntegrationTests;

public class DatabaseTests : IDisposable
{
    private readonly ApplicationDbContext dbContext;

    private const string UserLogin = "Login";
    private const string UserLogin1 = "Login1";
    private const string UserLogin2 = "Login2";
    private const string UserLogin3 = "Login3";
    private const string NationalityName = "GuillaumeLand";
    private long GameId;

    public DatabaseTests()
    {
        IServiceCollection services = Web.Program.ConfigureServices(new ConfigurationManager());
        IServiceProvider provider = services.BuildServiceProvider();
        dbContext = provider.GetRequiredService<ApplicationDbContext>();
    }

    [Fact]
    public async Task CanQueryDatabase()
    {
        if (!await dbContext.Users.AnyAsync(u => u.Login.Equals(UserLogin)))
        {
            UserEntity userEntity = User(UserLogin, "Password8", "email@gmail.com", DateOnly.MinValue);
            await dbContext.Users.AddAsync(userEntity);
            await dbContext.SaveChangesAsync();
        }

        UserEntity? user = await dbContext.Users.FirstOrDefaultAsync(u => u.Login.Equals(UserLogin));
        Check.That(user).IsNotNull();
        Check.That(user!.DateSignup).IsNotEqualTo(DateTime.MinValue);
        Check.That(user.Birthdate).IsEqualTo(DateOnly.MinValue);
    }

    [Fact]
    public async Task CanGetUserInfoFromDatabase()
    {
        if (!await dbContext.Nationalities.AnyAsync(u => u.Name.Equals(NationalityName)))
        {
            NationalityEntity natioEntity = new()
            {
               Name = NationalityName
            };
            await dbContext.Nationalities.AddAsync(natioEntity);
            await dbContext.SaveChangesAsync();
        }

        long nationalityId = (await dbContext.Nationalities.FirstAsync(n => n.Name.Equals(NationalityName))).Id;

        if (!await dbContext.Users.AnyAsync(u => u.Login.Equals(UserLogin3)))
        {
            UserEntity uEntity = User(UserLogin3, "password8", "email3@gmail.com", DateOnly.MinValue, nationalityId);
            await dbContext.Users.AddAsync(uEntity);
            await dbContext.SaveChangesAsync();
        }

        UserEntity? userEntity = await dbContext.Users.FirstOrDefaultAsync(u => u.Login.Equals(UserLogin3));
        Check.That(userEntity).IsNotNull();
        Check.That(userEntity!.DateSignup).IsNotEqualTo(DateTime.MinValue);
        Check.That(userEntity.Birthdate).IsEqualTo(DateOnly.MinValue);
        User user = userEntity.ToDomainEntity();
        Check.That(user.Nationality).IsEqualTo("GuillaumeLand");
    }

    [Fact]
    public async Task SuccessSaveGame()
    {
        if (!await dbContext.Users.AnyAsync(u => u.Login.Equals(UserLogin1) || u.Login.Equals(UserLogin2)))
        {
            List<UserEntity> userEntities = Users();
            await dbContext.Users.AddRangeAsync(userEntities);
            await dbContext.SaveChangesAsync();
        }

        List<LobbyPlayer> lobbyPlayers = dbContext.Users
            .Where(u => u.Login.Equals(UserLogin1) || u.Login.Equals(UserLogin2))
            .Select(u => new LobbyPlayer(u.ToDomainEntity()))
            .ToList();

        List<GamePlayer> gamePlayers = lobbyPlayers
            .Select(lobbyPlayer => new GamePlayer(lobbyPlayer))
            .ToList();

        GameRepository gameRepository = new(dbContext, new MoveRepository(dbContext, new DominoRepository(dbContext)), new UserRepository(dbContext, new NationalityRepository(dbContext)));

        Game game = new(0, gamePlayers, GameMode.UnRanked, 2002);

        game.DistributeDominoes();
        game.PlaceFirstDomino();
        game.UpdateClassement();

        GameId = await gameRepository.Save(game);

        GameEntity? gameEntity = await dbContext.Games.Include(g => g.Moves).FirstOrDefaultAsync(g => g.Id == GameId);
        Check.That(gameEntity).IsNotNull();
        Check.That(gameEntity!.Moves.Count).IsStrictlyGreaterThan(0);
    }

    private static UserEntity User(string login, string password, string email, DateOnly birthdate, long? nationalityId = null) => new()
    {
        Login = login,
        Password = password,
        Email = email,
        Birthdate = birthdate,
        NationalityId = nationalityId
    };

    private static List<UserEntity> Users()
    {
        return [
            User(UserLogin1, "Password8", "email1@gmail.com", DateOnly.MinValue),
            User(UserLogin2, "Password8", "email2@gmail.com", DateOnly.MinValue)
        ];
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        CheckAndRemoveUser(UserLogin);
        CheckAndRemoveUser(UserLogin1);
        CheckAndRemoveUser(UserLogin2);
        CheckAndRemoveUser(UserLogin3);

        CheckAndRemoveGame(GameId);

        CheckAndRemoveNationality(NationalityName);
    }

    private void CheckAndRemoveUser(string login)
    {
        UserEntity? user = dbContext.Users.FirstOrDefault(u => u.Login.Equals(login));
        if (user is not null)
        {
            dbContext.Users.Remove(user);
            dbContext.SaveChanges();
        }
    }

    private void CheckAndRemoveGame(long gameId)
    {
        GameEntity? game = dbContext.Games.FirstOrDefault(g => g.Id == gameId);
        if (game is not null)
        {
            dbContext.Games.Remove(game);
            dbContext.SaveChanges();
        }
    }

    private void CheckAndRemoveNationality(string nationalityName)
    {
        NationalityEntity? nationality = dbContext.Nationalities.FirstOrDefault(n => n.Name.Equals(nationalityName));
        if (nationality is not null)
        {
            dbContext.Nationalities.Remove(nationality);
            dbContext.SaveChanges();
        }
    }
}
