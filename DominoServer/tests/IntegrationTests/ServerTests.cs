using CommonTests.Fixtures;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NFluent;

namespace IntegrationTests;

public class ServerTests : IDisposable
{
    private readonly IServiceProvider provider = Web.Program.ConfigureServices(new ConfigurationManager()).BuildServiceProvider();
    private readonly CancellationTokenSource tokenSource = new();
    private readonly ClientFake clientMock = new();

    private const string Localhost = "127.0.0.1";

    [Fact]
    public void ClientConnectToServer()
    {
        const int port = 50005;
        tokenSource.CancelAfter(new TimeSpan(0, 0, 2));
        Task task = Web.Program.StartServerAsync(provider, port, 1, tokenSource.Token);
        Check.ThatCode(async () => await clientMock.ConnectToServerAsync(Localhost, port)).DoesNotThrow();
        Check.That(clientMock.TcpClient.Connected).IsTrue();
        Check.ThatCode(async () => await task).DoesNotThrow();
    }

    [Fact]
    public async Task ClientRejectedFromServer()
    {
        const int port = 50001;
        tokenSource.CancelAfter(new TimeSpan(0, 0, 2));
        Task task = Web.Program.StartServerAsync(provider, port, 0, tokenSource.Token);
        Check.ThatCode(async () => await clientMock.ConnectToServerAsync(Localhost, port)).DoesNotThrow();
        await Task.Delay(500);
        Check.That(clientMock.IsConnected()).IsFalse();
        Check.ThatCode(async () => await task).DoesNotThrow();
    }

    public void Dispose()
    {
        if (!tokenSource.IsCancellationRequested) tokenSource.Cancel();
        GC.SuppressFinalize(this);
    }
}
