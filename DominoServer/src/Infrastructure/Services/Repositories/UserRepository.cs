using System.Text.RegularExpressions;

using Application.Exceptions.Connection;
using Application.Exceptions.User;
using Application.Interfaces.Repositories;

using Domain;
using Domain.Game;

using Infrastructure.Database;
using Infrastructure.Database.Entities;

using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services.Repositories;

public class UserRepository(ApplicationDbContext dbContext, INationalityRepository nationalityRepository) : IUserRepository
{
    private const string PasswordPattern = @"^(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d]{8,}$"; // at least one letter, one number, and 8 characters
    private const string EmailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

    public async Task<User> Create(User user, string password, DateOnly birthdate)
    {
        if (!Regex.IsMatch(user.Email, EmailPattern))
        {
            throw new InvalidEmailFormatException("Email format is invalid.");
        }

        if (user.Login.Length < 2 || user.Login.Length > 20)
        {
            throw new LoginTooLongException("Login is too long. It must be between 2 and 20 characters");
        }

        if (!Regex.IsMatch(password, PasswordPattern))
        {
            throw new InvalidPasswordFormatException("Password is invalid. It must be at least 8 characters long and contain at least one letter and one number.");
        }

        UserEntity entity = new()
        {
            Email = user.Email,
            Login = user.Login,
            Password = PasswordHasher.Hash(password),
            Birthdate = birthdate
        };
        await dbContext.Users.AddAsync(entity);
        await dbContext.SaveChangesAsync();
        return entity.ToDomainEntity();
    }

    public async Task<bool> ExistsByEmail(string email)
    {
        return await dbContext.Users.AnyAsync(u => u.Email.Equals(email));
    }

    public async Task<bool> ExistsByLogin(string login)
    {
        return await dbContext.Users.AnyAsync(u => u.Login.Equals(login));
    }

    public async Task<User?> Login(string login, string password)
    {
        UserEntity? user = await dbContext.Users.FirstOrDefaultAsync(u => u.Login.Equals(login));
        if (user is null || !VerifyPassword(user, password)) return null;
        return user.ToDomainEntity();
    }

    public static bool VerifyPassword(UserEntity user, string password)
    {
        return PasswordHasher.Verify(password, user.Password);
    }

    public async Task<User> GetById(long id)
    {
        return (await dbContext.Users.FirstAsync(u => u.Id == id)).ToDomainEntity();
    }

    public async Task<UserStats> GetUserStats(long id)
    {
        UserStats userStats = new(await GetNumberOfGamePlayedByUser(id), await GetNumberOfGameWonByUser(id), await GetNumberOfGameLostByUser(id));
        return userStats;
    }

    public async Task<User> UpdateProfile(User user)
    {
        if (user.Login.Length < 2 || user.Login.Length > 20)
        {
            throw new LoginTooLongException("Login is too long. It must be between 2 and 20 characters");
        }

        UserEntity entity = await dbContext.Users.FirstAsync(u => u.Id == user.Id);

        entity.Login = user.Login;
        entity.Birthdate = user.Birthdate;
        if (user.Nationality is not null)
        {
            entity.NationalityId = await nationalityRepository.GetByName(user.Nationality);
            if (entity.NationalityId is null)
            {
                throw new InvalidCountryException($"The {user.Nationality} doesn't exist.");
            }
        }
        await dbContext.SaveChangesAsync();
        return entity.ToDomainEntity();
    }

    public async Task UpdatePassword(User user, string actualPassword, string newPassword)
    {
        UserEntity entity = await dbContext.Users.FirstAsync(u => u.Id == user.Id);

        if (!VerifyPassword(entity, actualPassword))
        {
            throw new WrongPasswordException("The password for the user is not correct");
        }

        if (!Regex.IsMatch(newPassword, PasswordPattern))
        {
            throw new InvalidPasswordFormatException("Password is invalid. It must be at least 8 characters long and contain at least one letter and one number.");
        }

        entity.Password = PasswordHasher.Hash(newPassword);
        await dbContext.SaveChangesAsync();
    }

    public async Task<User> GetByLogin(string login)
    {
        return (await dbContext.Users.FirstAsync(u => u.Login.Equals(login))).ToDomainEntity();
    }

    public async Task UpdateScore(User user, int newScore)
    {
        UserEntity entity = await dbContext.Users.FirstAsync(u => u.Id == user.Id);
        entity.Score = newScore;
        await dbContext.SaveChangesAsync();
    }

    public async Task<List<HistoryGame>> GetAllHistoryGames(long id)
    {
        return (await dbContext.Users
            .Include(u => u.Games)
            .FirstAsync(u => u.Id == id))
            .Games.Select(g => g.ToHistoryDomainEntity())
            .ToList();
    }

    public async Task<HistoryGame> GetHistoryGame(long id)
    {
        return (await dbContext.Games.FirstAsync(g => g.Id == id)).ToHistoryDomainEntity();
    }

    public async Task<int> GetNumberOfGamePlayedByUser(long id)
    {
        return await dbContext.UserGames.CountAsync(ug => ug.UserId == id);
    }

    public async Task<int> GetNumberOfGameWonByUser(long id)
    {
        return await dbContext.UserGames.CountAsync(ug => ug.UserId == id && ug.Game.WinnerId == id);
    }

    public async Task<int> GetNumberOfGameLostByUser(long id)
    {
        return await dbContext.UserGames.CountAsync(ug => ug.UserId == id && ug.Game.WinnerId != id);
    }

    public async Task ChangePassword(long id, string newPassword)
    {
        if (!Regex.IsMatch(newPassword, PasswordPattern))
        {
            throw new InvalidPasswordFormatException("Password is invalid. It must be at least 8 characters long and contain at least one letter and one number.");
        }

        UserEntity entity = await dbContext.Users.FirstAsync(u => u.Id == id);
        entity.Password = PasswordHasher.Hash(newPassword);
        await dbContext.SaveChangesAsync();
    }

    public async Task<User?> GetByEmail(string email)
    {
        return (await dbContext.Users.FirstOrDefaultAsync(u => u.Email.Equals(email)))?.ToDomainEntity();
    }
}
