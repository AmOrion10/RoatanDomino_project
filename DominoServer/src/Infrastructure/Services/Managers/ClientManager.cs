using System.Net.WebSockets;

using Application.Common;
using Application.Interfaces;
using Application.Interfaces.Managers;
using Application.Messages.Commands.User;
using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame;
using DominoCommon.com.domino.common.Scripts.Networking;
using DominoCommon.com.domino.common.Scripts.Networking.Frame;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Managers;

public class ClientManager(IServiceScopeFactory serviceScopeFactory, ITokenManager tokenManager) : IClientManager
{
    public int MaxClients { get; private set; }
    private readonly List<IClient> clients = [];

    public void ConfigureMaxClients(int maxClients)
    {
        MaxClients = maxClients;
    }

    public bool HasEnoughSpaceForAnotherClient()
    {
        lock (clients)
        {
            return clients.Count < MaxClients;
        }
    }

    public void HandleNewClient(IClient newClient)
    {
        lock (clients)
        {
            clients.Add(newClient);
        }
        Task.Factory.StartNew(async() => await HandleClientAsync(newClient), TaskCreationOptions.LongRunning);
    }

    public List<IClient> GetAllLoggedInClients()
    {
        lock (clients)
        {
            return clients.Where(c => c.IsLoggedIn).ToList();
        }
    }

    public List<IClient> GetAllClients()
    {
        lock (clients)
        {
            return clients;
        }
    }

    private async Task HandleClientAsync(IClient client)
    {
        using IServiceScope scopeClient = serviceScopeFactory.CreateScope();
        ILogger<ClientManager> logger = scopeClient.ServiceProvider.GetRequiredService<ILogger<ClientManager>>();
        IMessageDispatcher dispatcher = scopeClient.ServiceProvider.GetRequiredService<IMessageDispatcher>();
        ClientDisconnectionManager clientDisconnectionManager = scopeClient.ServiceProvider.GetRequiredService<ClientDisconnectionManager>();

        logger.LogInformation("Thread created for new client {id}", client.Id);

        while (client.IsConnected())
        {
            try
            {
                using IServiceScope scopeLocal = serviceScopeFactory.CreateScope();
                IMessageFrameHandler messageFrameHandlerLocal = scopeLocal.ServiceProvider.GetRequiredService<IMessageFrameHandler>();
                scopeLocal.ServiceProvider.GetRequiredService<MessageFrameHandlers>().RegisterHandlers();

                NetworkFrame networkFrame = await client.ReceiveAsync();

                // It is possible that when the client forcibly disconnects, it sends few bytes which are not valid for a frame.
                // Comparing the NumberOfFrame with 0 helps us to determine whether this is the case.
                if (networkFrame.NumberOfFrames == 0)
                {
                    client.CloseConnection();
                    break;
                }

                if (networkFrame.TokenClient != tokenManager.GetTokenForClient(client))
                {
                    ResponseDto dto = new()
                    {
                        OkResult = false,
                        ErrorType = ErrorTypes.InvalidTokenClient
                    };
                    await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(dto, FrameCommandType.Response)));
                    continue;
                }

                foreach (MessageFrame messageFrame in networkFrame.Frames)
                {
                    logger.LogInformation("Message from client {id} of type {cmd}", client.Id,
                        messageFrame.CommandType);
                    await messageFrameHandlerLocal.HandleAsync(messageFrame, client);
                }
            }
            catch (IOException ex)
            {
                logger.LogError("IOException occurs for client {id} : {ex}", client.Id, ex);
                break;
            }
            catch (WebSocketException ex)
            {
                logger.LogInformation("WebSocket disconnected for client {id}: {message}", client.Id, ex.Message);
                break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError("Uncatched exception occurs for client {id} : {ex}", client.Id, ex);
            }
        }

        try
        {
            client.CloseConnection();
        }
        catch
        {
            // Ignore close errors during cleanup
        }

        await clientDisconnectionManager.ProcessDisconnection(await dispatcher.Dispatch(new LogoutUserCommand(client, isSocketDrop: true)));

        lock (clients)
        {
            clients.Remove(client);
        }
        logger.LogInformation("Client {id} removed from clients", client.Id);
    }
}
