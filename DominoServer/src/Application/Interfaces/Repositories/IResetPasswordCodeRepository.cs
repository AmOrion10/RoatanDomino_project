namespace Application.Interfaces.Repositories;

public interface IResetPasswordCodeRepository
{
    Task Create(string code, string email);
    Task<string?> GetLastForEmail(string email);
}