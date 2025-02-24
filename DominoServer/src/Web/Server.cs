using Application.Interfaces;
using Application.Interfaces.Managers;

using Infrastructure.Services.ServerUpdate;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Web;

public class Server
{
    private readonly int port;
    private readonly ILogger<Server> logger;
    private readonly ITcpServer tcpServer;
    private readonly IClientManager clientManager;
    private readonly IChronometer chronometer;
    private readonly ServerUpdater serverUpdater;

    public Server(IServiceProvider provider, int port, int maxClients)
    {
        this.port = port;
        tcpServer = provider.GetRequiredService<ITcpServer>();
        logger = provider.GetRequiredService<ILogger<Server>>();
        clientManager = provider.GetRequiredService<IClientManager>();
        chronometer = provider.GetRequiredService<IChronometer>();
        serverUpdater = provider.GetRequiredService<ServerUpdater>();
        clientManager.ConfigureMaxClients(maxClients);
    }

    public async Task StartAsync(CancellationToken token)
    {
        tcpServer.StartListening(port);
        logger.LogInformation("Server listening on {port}", port);

        while (tcpServer.Running(token))
        {
            TimeSpan deltaTime = chronometer.GetTimedElapsed();
            chronometer.StartFrame();

            await CheckForNewClientsAsync();
            await serverUpdater.UpdateAsync(deltaTime);
            
            chronometer.EndFrame();
        }
    }

    private async Task CheckForNewClientsAsync()
    {
        await Task.Delay(1); // Magic but needed for a test

        if (!tcpServer.ClientWantsToConnect()) return;

        if (clientManager.HasEnoughSpaceForAnotherClient())
        {
            clientManager.HandleNewClient(await tcpServer.AcceptClientAsync());
            logger.LogInformation("New client connected");
        }
        else
        {
            await tcpServer.RejectConnectionAsync();
        }
    }
}
