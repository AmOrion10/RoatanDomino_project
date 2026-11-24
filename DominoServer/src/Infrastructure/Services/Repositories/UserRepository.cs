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
    private const string PasswordPattern = @"^(?=.*[A-Z])(?=.*[\d\W_]).{8,}$";
    private const string PasswordErrorMessage = "Password must be at least 8 characters and include at least one capital letter (A-Z) and one number or special character (!@#$%).";
    private const string EmailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
    private const string EmailErrorMessage = "Please enter a valid email address.";
    private const string UsernamePattern = @"^[A-Za-z0-9_]{3,16}$";
    private const string UsernameErrorMessage = "Username must be 3–16 letters or numbers.";

    public async Task<User> Create(User user, string password, DateOnly birthdate)
    {
        if (string.IsNullOrWhiteSpace(user.Email) || !Regex.IsMatch(user.Email.Trim(), EmailPattern))
        {
            throw new InvalidEmailFormatException(EmailErrorMessage);
        }

        if (string.IsNullOrWhiteSpace(user.Login) || !Regex.IsMatch(user.Login.Trim(), UsernamePattern))
        {
            throw new LoginTooLongException(UsernameErrorMessage);
        }

        if (string.IsNullOrEmpty(password) || !Regex.IsMatch(password, PasswordPattern))
        {
            throw new InvalidPasswordFormatException(PasswordErrorMessage);
        }

        UserEntity entity = new()
        {
            Email = user.Email.Trim(),
            Login = user.Login.Trim(),
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
        UserEntity? user = await dbContext.Users.FirstOrDefaultAsync(u => u.Login.Equals(login) || u.Email.Equals(login));
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
        if (string.IsNullOrWhiteSpace(user.Login) || !Regex.IsMatch(user.Login.Trim(), UsernamePattern))
        {
            throw new LoginTooLongException(UsernameErrorMessage);
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

        if (string.IsNullOrEmpty(newPassword) || !Regex.IsMatch(newPassword, PasswordPattern))
        {
            throw new InvalidPasswordFormatException(PasswordErrorMessage);
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
        if (string.IsNullOrEmpty(newPassword) || !Regex.IsMatch(newPassword, PasswordPattern))
        {
            throw new InvalidPasswordFormatException(PasswordErrorMessage);
        }

        UserEntity entity = await dbContext.Users.FirstAsync(u => u.Id == id);
        entity.Password = PasswordHasher.Hash(newPassword);
        await dbContext.SaveChangesAsync();
    }

    public async Task<User?> GetByEmail(string email)
    {
        return (await dbContext.Users.FirstOrDefaultAsync(u => u.Email.Equals(email)))?.ToDomainEntity();
    }

    public async Task<List<User>> GetAllUsers()
    {
        List<UserEntity> entities = await dbContext.Users
            .Include(u => u.Nationality)
            .OrderBy(u => u.Login)
            .ToListAsync();
        return entities.Select(e => e.ToDomainEntity()).ToList();
    }

    public async Task<User> FindOrCreateSocialUser(string provider, string providerUserId, string? email, string? displayName)
    {
        string cleanProvider = string.Equals(provider, "Facebook", StringComparison.OrdinalIgnoreCase) ? "Facebook" : "Google";
        string safeId = Regex.Replace(providerUserId ?? string.Empty, @"[^A-Za-z0-9]", string.Empty);
        if (string.IsNullOrEmpty(safeId))
        {
            safeId = Guid.NewGuid().ToString("N")[..8];
        }

        string resolvedEmail = !string.IsNullOrWhiteSpace(email) && Regex.IsMatch(email.Trim(), EmailPattern)
            ? email.Trim()
            : $"{cleanProvider.ToLowerInvariant()}.{safeId.ToLowerInvariant()}@social.roatandomino.local";

        UserEntity? existing = await dbContext.Users.FirstOrDefaultAsync(u => u.Email.Equals(resolvedEmail));
        if (existing is not null)
        {
            return existing.ToDomainEntity();
        }

        string baseLogin = Regex.Replace(displayName ?? string.Empty, @"[^A-Za-z0-9_]", string.Empty);
        if (baseLogin.Length < 3)
        {
            string prefix = cleanProvider == "Google" ? "GPlayer" : "FBPlayer";
            string suffix = safeId.Length >= 4 ? safeId[..4] : safeId;
            baseLogin = $"{prefix}{suffix}";
        }
        if (baseLogin.Length > 14)
        {
            baseLogin = baseLogin[..14];
        }

        string candidateLogin = baseLogin;
        int attempt = 1;
        while (await dbContext.Users.AnyAsync(u => u.Login.Equals(candidateLogin)))
        {
            string numStr = attempt.ToString();
            int maxPrefix = Math.Max(3, 16 - numStr.Length);
            candidateLogin = $"{baseLogin[..Math.Min(baseLogin.Length, maxPrefix)]}{numStr}";
            attempt++;
        }

        string generatedSecret = $"Soc!{Guid.NewGuid():N}9A";
        UserEntity created = new()
        {
            Email = resolvedEmail,
            Login = candidateLogin,
            Password = PasswordHasher.Hash(generatedSecret),
            Birthdate = new DateOnly(2000, 1, 1),
            Score = 1000
        };
        await dbContext.Users.AddAsync(created);
        await dbContext.SaveChangesAsync();
        return created.ToDomainEntity();
    }
}
