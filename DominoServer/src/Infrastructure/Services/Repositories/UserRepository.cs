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
    public const int MaxAvatarBytes = 32 * 1024; // 32 KB compressed 128x128 thumbnail cap

    private const string PasswordPattern = @"^(?=.*[A-Z])(?=.*[\d\W_]).{8,}$";
    private const string PasswordErrorMessage = "Password must be at least 8 characters and include at least one capital letter (A-Z) and one number or special character (!@#$%).";
    private const string EmailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
    private const string EmailErrorMessage = "Please enter a valid email address.";
    private const string UsernamePattern = @"^[A-Za-z0-9_]{3,16}$";
    private const string UsernameErrorMessage = "Username must be 3–16 letters or numbers.";
    private const string DuplicateEmailMessage = "An account with this email address already exists. Please sign in instead.";
    private const string DuplicateUsernameMessage = "That username is already taken. Please choose another username.";

    public async Task<User> Create(User user, string password, DateOnly birthdate)
    {
        string trimmedEmail = user.Email?.Trim() ?? string.Empty;
        string trimmedLogin = user.Login?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(trimmedEmail) || !Regex.IsMatch(trimmedEmail, EmailPattern))
        {
            throw new InvalidEmailFormatException(EmailErrorMessage);
        }

        if (string.IsNullOrWhiteSpace(trimmedLogin) || !Regex.IsMatch(trimmedLogin, UsernamePattern))
        {
            throw new LoginTooLongException(UsernameErrorMessage);
        }

        if (string.IsNullOrEmpty(password) || !Regex.IsMatch(password, PasswordPattern))
        {
            throw new InvalidPasswordFormatException(PasswordErrorMessage);
        }

        if (await ExistsByEmail(trimmedEmail))
        {
            throw new UserAlreadyExistException(DuplicateEmailMessage);
        }

        if (await ExistsByLogin(trimmedLogin))
        {
            throw new UserAlreadyExistException(DuplicateUsernameMessage);
        }

        UserEntity entity = new()
        {
            Email = trimmedEmail,
            Login = trimmedLogin,
            Password = PasswordHasher.Hash(password),
            Birthdate = birthdate,
            AuthProvider = string.IsNullOrWhiteSpace(user.AuthProvider) ? "Email" : user.AuthProvider
        };
        await dbContext.Users.AddAsync(entity);
        await dbContext.SaveChangesAsync();
        return entity.ToDomainEntity();
    }

    public async Task<bool> ExistsByEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        string normalized = email.Trim().ToLowerInvariant();
        return await dbContext.Users.AnyAsync(u => u.Email.ToLower() == normalized);
    }

    public async Task<bool> ExistsByLogin(string login)
    {
        if (string.IsNullOrWhiteSpace(login)) return false;
        string normalized = login.Trim().ToLowerInvariant();
        return await dbContext.Users.AnyAsync(u => u.Login.ToLower() == normalized);
    }

    public async Task<User?> Login(string login, string password)
    {
        if (string.IsNullOrWhiteSpace(login)) return null;
        string normalized = login.Trim().ToLowerInvariant();
        UserEntity? user = await dbContext.Users.FirstOrDefaultAsync(u => u.Login.ToLower() == normalized || u.Email.ToLower() == normalized);
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
        string trimmedLogin = user.Login?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedLogin) || !Regex.IsMatch(trimmedLogin, UsernamePattern))
        {
            throw new LoginTooLongException(UsernameErrorMessage);
        }

        string normalizedLogin = trimmedLogin.ToLowerInvariant();
        bool loginTaken = await dbContext.Users.AnyAsync(u => u.Id != user.Id && u.Login.ToLower() == normalizedLogin);
        if (loginTaken)
        {
            throw new UserAlreadyExistException(DuplicateUsernameMessage);
        }

        UserEntity entity = await dbContext.Users.FirstAsync(u => u.Id == user.Id);

        entity.Login = trimmedLogin;
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
        string normalized = login.Trim().ToLowerInvariant();
        return (await dbContext.Users.FirstAsync(u => u.Login.ToLower() == normalized)).ToDomainEntity();
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
        if (string.IsNullOrWhiteSpace(email)) return null;
        string normalized = email.Trim().ToLowerInvariant();
        return (await dbContext.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalized))?.ToDomainEntity();
    }

    public async Task<List<User>> GetAllUsers()
    {
        List<UserEntity> entities = await dbContext.Users
            .Include(u => u.Nationality)
            .Include(u => u.Avatar)
            .OrderBy(u => u.Login)
            .ToListAsync();
        return entities.Select(e => e.ToDomainEntity()).ToList();
    }

    public async Task<User> FindOrCreateSocialUser(
        string provider,
        string providerUserId,
        string? email,
        string? displayName,
        string? avatarUrl = null,
        string? avatarBase64 = null)
    {
        string cleanProvider = string.Equals(provider, "Facebook", StringComparison.OrdinalIgnoreCase) ? "Facebook" : "Google";
        string trimmedEmail = email?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedEmail) || !Regex.IsMatch(trimmedEmail, EmailPattern))
        {
            throw new InvalidEmailFormatException(EmailErrorMessage);
        }

        byte[]? decodedAvatar = DecodeAndValidateAvatar(avatarBase64);

        string normalizedEmail = trimmedEmail.ToLowerInvariant();
        UserEntity? existing = await dbContext.Users
            .Include(u => u.Avatar)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

        if (existing is not null)
        {
            bool changed = false;
            if (!string.IsNullOrWhiteSpace(avatarUrl) && !string.Equals(existing.AvatarUrl, avatarUrl.Trim(), StringComparison.Ordinal))
            {
                existing.AvatarUrl = avatarUrl.Trim();
                changed = true;
            }
            if (decodedAvatar is not null && existing.Avatar is null)
            {
                AvatarEntity avatarEntity = new() { Image = decodedAvatar };
                await dbContext.Avatars.AddAsync(avatarEntity);
                await dbContext.SaveChangesAsync();
                existing.AvatarId = avatarEntity.Id;
                existing.Avatar = avatarEntity;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(existing.AuthProvider))
            {
                existing.AuthProvider = cleanProvider;
                changed = true;
            }
            if (changed)
            {
                await dbContext.SaveChangesAsync();
            }
            return existing.ToDomainEntity();
        }

        string requestedName = displayName?.Trim() ?? string.Empty;
        string sanitizedLogin = Regex.Replace(requestedName, @"[^A-Za-z0-9_]", string.Empty);
        if (sanitizedLogin.Length < 3)
        {
            string emailLocal = Regex.Replace(trimmedEmail.Split('@')[0], @"[^A-Za-z0-9_]", string.Empty);
            sanitizedLogin = emailLocal.Length >= 3 ? emailLocal : $"Player{Guid.NewGuid():N}"[..10];
        }
        if (sanitizedLogin.Length > 16)
        {
            sanitizedLogin = sanitizedLogin[..16];
        }

        // If user explicitly chose a username in the profile-completion prompt and it is already taken, reject with clear message
        if (!string.IsNullOrWhiteSpace(requestedName) &&
            Regex.IsMatch(requestedName, UsernamePattern) &&
            string.IsNullOrWhiteSpace(providerUserId))
        {
            if (await ExistsByLogin(requestedName))
            {
                throw new UserAlreadyExistException(DuplicateUsernameMessage);
            }
        }

        string candidateLogin = sanitizedLogin;
        int attempt = 1;
        while (await ExistsByLogin(candidateLogin))
        {
            string numStr = attempt.ToString();
            int maxPrefix = Math.Max(3, 16 - numStr.Length);
            candidateLogin = $"{sanitizedLogin[..Math.Min(sanitizedLogin.Length, maxPrefix)]}{numStr}";
            attempt++;
        }

        AvatarEntity? newAvatar = null;
        if (decodedAvatar is not null)
        {
            newAvatar = new AvatarEntity { Image = decodedAvatar };
            await dbContext.Avatars.AddAsync(newAvatar);
            await dbContext.SaveChangesAsync();
        }

        string generatedSecret = $"Soc!{Guid.NewGuid():N}9A";
        UserEntity created = new()
        {
            Email = trimmedEmail,
            Login = candidateLogin,
            Password = PasswordHasher.Hash(generatedSecret),
            Birthdate = new DateOnly(2000, 1, 1),
            Score = 1000,
            AvatarId = newAvatar?.Id,
            Avatar = newAvatar,
            AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl.Trim(),
            AuthProvider = cleanProvider
        };
        await dbContext.Users.AddAsync(created);
        await dbContext.SaveChangesAsync();
        return created.ToDomainEntity();
    }

    public async Task<User> UpdateAvatar(long userId, string? avatarBase64, string? avatarUrl)
    {
        UserEntity entity = await dbContext.Users
            .Include(u => u.Avatar)
            .FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new UserNotExistingException("Account not found.");

        byte[]? decodedBytes = DecodeAndValidateAvatar(avatarBase64);
        if (decodedBytes is not null)
        {
            if (entity.Avatar is not null)
            {
                entity.Avatar.Image = decodedBytes;
            }
            else
            {
                AvatarEntity avatar = new() { Image = decodedBytes };
                await dbContext.Avatars.AddAsync(avatar);
                await dbContext.SaveChangesAsync();
                entity.AvatarId = avatar.Id;
                entity.Avatar = avatar;
            }
        }

        if (avatarUrl is not null)
        {
            entity.AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl.Trim();
        }

        await dbContext.SaveChangesAsync();
        return entity.ToDomainEntity();
    }

    public async Task<bool> DeleteUser(long userId)
    {
        UserEntity? entity = await dbContext.Users
            .Include(u => u.Avatar)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (entity is null) return false;

        List<FriendRequestEntity> friendRequests = await dbContext.FriendRequests
            .Where(fr => fr.UserIdApplicant == userId || fr.UserIdAsked == userId)
            .ToListAsync();
        if (friendRequests.Count > 0) dbContext.FriendRequests.RemoveRange(friendRequests);

        List<FriendshipEntity> friendships = await dbContext.Friendships
            .Where(f => f.UserId == userId || f.FriendId == userId)
            .ToListAsync();
        if (friendships.Count > 0) dbContext.Friendships.RemoveRange(friendships);

        List<UserGameEntity> userGames = await dbContext.UserGames
            .Where(ug => ug.UserId == userId)
            .ToListAsync();
        if (userGames.Count > 0) dbContext.UserGames.RemoveRange(userGames);

        List<GameEntity> gamesWon = await dbContext.Games
            .Where(g => g.WinnerId == userId)
            .ToListAsync();
        foreach (GameEntity g in gamesWon) g.WinnerId = null;

        List<MoveEntity> moves = await dbContext.Moves
            .Where(m => m.UserId == userId)
            .ToListAsync();
        if (moves.Count > 0) dbContext.Moves.RemoveRange(moves);

        List<ResetPasswordCodeEntity> codes = await dbContext.ResetPasswordCode
            .Where(c => c.EmailAddress.ToLower() == entity.Email.ToLower())
            .ToListAsync();
        if (codes.Count > 0) dbContext.ResetPasswordCode.RemoveRange(codes);

        AvatarEntity? avatarToRemove = entity.Avatar;
        entity.AvatarId = null;
        entity.Avatar = null;
        if (avatarToRemove is not null)
        {
            dbContext.Avatars.Remove(avatarToRemove);
        }

        dbContext.Users.Remove(entity);
        await dbContext.SaveChangesAsync();
        return true;
    }

    private static byte[]? DecodeAndValidateAvatar(string? avatarBase64)
    {
        if (string.IsNullOrWhiteSpace(avatarBase64)) return null;

        string cleaned = avatarBase64.Trim();
        int commaIdx = cleaned.IndexOf(',');
        if (cleaned.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIdx >= 0)
        {
            cleaned = cleaned[(commaIdx + 1)..];
        }

        try
        {
            byte[] bytes = Convert.FromBase64String(cleaned);
            if (bytes.Length == 0) return null;
            if (bytes.Length > MaxAvatarBytes)
            {
                throw new InvalidPasswordFormatException($"Avatar image must be {MaxAvatarBytes / 1024} KB or smaller.");
            }
            return bytes;
        }
        catch (FormatException)
        {
            throw new InvalidPasswordFormatException("Avatar image data is invalid.");
        }
    }
}
