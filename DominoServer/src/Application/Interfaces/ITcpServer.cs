namespace Application.Interfaces;

public interface ITcpServer
{
    void StartListening(int port);
    bool Running(CancellationToken token);
    bool ClientWantsToConnect();
    Task<IClient> AcceptClientAsync();
    Task RejectConnectionAsync();
}