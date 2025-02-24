using Application.Interfaces;
using Application.Interfaces.Managers;

using Infrastructure.Exceptions;

namespace Infrastructure.Services.Managers;

public class TokenManager : ITokenManager
{
    private readonly Dictionary<IClient, Guid> tokens = [];

    public Guid GenerateTokenForClient(IClient client)
    {
        lock (tokens)
        {
            Guid guid = Guid.NewGuid();

            if (!tokens.TryAdd(client, guid))
            {
                throw new ClientAlreadyHaveTokenException($"The client {client.Id} has already a token");
            }

            return guid;
        }
    }

    public Guid GetTokenForClient(IClient client)
    {
        lock (tokens)
        {
            if (tokens.TryGetValue(client, out Guid value))
            {
                return value;
            }
            return Guid.Empty;
        }
    }

    public void RemoveTokenForClientIfPresent(IClient client)
    {
        lock (tokens)
        {
            tokens.Remove(client);
        }
    }
}
