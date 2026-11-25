using Domain;
using Domain.Game;

namespace Application.Interfaces.Repositories;

public interface IUserRepository
{
    Task<User> Create(User user, string password, DateOnly birthdate);
    Task<bool> ExistsByEmail(string email);
    Task<bool> ExistsByLogin(string login);
    Task<User?> Login(string login, string password);
    Task<User> GetById(long id);
    Task<UserStats> GetUserStats(long id);
    Task<User> UpdateProfile(User user);
    Task UpdatePassword(User user, string actualPassword, string newPassword);
    Task<User> GetByLogin(string login);
    Task UpdateScore(User user, int newScore);
    Task<List<HistoryGame>> GetAllHistoryGames(long id);
    Task<HistoryGame> GetHistoryGame(long id);
    Task<int> GetNumberOfGamePlayedByUser(long id);
    Task<int> GetNumberOfGameWonByUser(long id);
    Task<int> GetNumberOfGameLostByUser(long id);
    Task ChangePassword(long id, string newPassword);
    Task<User?> GetByEmail(string email);
    Task<List<User>> GetAllUsers();
    Task<User> FindOrCreateSocialUser(string provider, string providerUserId, string? email, string? displayName, string? avatarUrl = null, string? avatarBase64 = null);
    Task<User> UpdateAvatar(long userId, string? avatarBase64, string? avatarUrl);
    Task<bool> DeleteUser(long userId);
}
