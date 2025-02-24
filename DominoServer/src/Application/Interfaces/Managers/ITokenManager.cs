namespace Application.Interfaces.Managers;

public interface ITokenManager
{
    Guid GenerateTokenForClient(IClient client);
    Guid GetTokenForClient(IClient client);
    void RemoveTokenForClientIfPresent(IClient client);
}
