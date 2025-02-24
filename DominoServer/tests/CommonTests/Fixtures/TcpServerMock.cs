using Application.Interfaces;

namespace CommonTests.Fixtures;

public class TcpServerMock : ITcpServer
{
    public const int ThreadSleepTime = 250;

    private bool[] returnsClientWantsToConnect = [false];
    private int returnsClientWantsToConnectCount;
    private ClientMock newClient = new()
    {
        Password = "GoodPassword8"
    };

    public bool ThreadSleepActive { get; set; } = true;
    public bool StartListeningCalled { get; private set; }
    public bool RejectConnectionAsyncCalled { get; private set; }
    public bool AcceptTcpClientAsyncCalled { get; private set; }

    public void StartListening(int port)
    {
        StartListeningCalled = true;
    }

    public bool Running(CancellationToken token)
    {
        if (ThreadSleepActive)
        {
            Thread.Sleep(ThreadSleepTime);
        }
        return !token.IsCancellationRequested;
    }

    public async Task<IClient> AcceptClientAsync()
    {
        AcceptTcpClientAsyncCalled = true;
        await Task.CompletedTask;
        return newClient;
    }

    public bool ClientWantsToConnect()
    {
        bool res = returnsClientWantsToConnect[returnsClientWantsToConnectCount];
        returnsClientWantsToConnectCount = Math.Min(returnsClientWantsToConnectCount + 1, returnsClientWantsToConnect.Length - 1);
        return res;
    }

    public Task RejectConnectionAsync()
    {
        RejectConnectionAsyncCalled = true;
        return Task.CompletedTask;
    }

    public void ClientWantsToConnectReturns(bool[] bools)
    {
        returnsClientWantsToConnect = bools;
    }

    public void SetNewClient(ClientMock client)
    {
        newClient = client;
    }
}
