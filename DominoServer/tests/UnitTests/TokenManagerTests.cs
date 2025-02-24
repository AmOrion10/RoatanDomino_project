using CommonTests.Fixtures;

using Infrastructure.Exceptions;
using Infrastructure.Services.Managers;

namespace UnitTests;

public class TokenManagerTests
{
    [Fact]
    public void ValidGeneration()
    {
        TokenManager tm = new();
        ClientMock client1 = new();
        ClientMock client2 = new();

        Guid guid1 = tm.GenerateTokenForClient(client1);
        Guid guid2 = tm.GenerateTokenForClient(client2);

        Check.That(guid1).IsNotEqualTo(guid2);
    }

    [Fact]
    public void ValidGet()
    {
        TokenManager tm = new();
        ClientMock client = new();

        Guid guidGenerated = tm.GenerateTokenForClient(client);
        Guid guidGet = tm.GetTokenForClient(client);

        Check.That(guidGenerated).IsEqualTo(guidGet);
    }

    [Fact]
    public void ValidRemoveToken()
    {
        TokenManager tm = new();
        ClientMock client = new();

        tm.GenerateTokenForClient(client);
        Check.ThatCode(() => tm.RemoveTokenForClientIfPresent(client)).DoesNotThrow();
        Guid token = tm.GetTokenForClient(client);
        Check.That(token).IsEqualTo(Guid.Empty);
    }

    [Fact]
    public void ClientAlreadyHasToken()
    {
        TokenManager tm = new();
        ClientMock client = new();

        tm.GenerateTokenForClient(client);

        Check.ThatCode(() => tm.GenerateTokenForClient(client)).Throws<ClientAlreadyHaveTokenException>();
    }
}
