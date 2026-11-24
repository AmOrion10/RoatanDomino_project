using System.Net.WebSockets;

using Application.Interfaces;

using Domain;

using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame;

using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class WebSocketClient : IClient
{
    private static int nextClientId = 10000;

    public int Id { get; }
    public bool IsLoggedIn { get; set; } = false;
    public User? User { get; set; } = null;

    /// <summary>
    /// Completes once the WebSocket session terminates so ASP.NET Core keeps the HTTP request open
    /// for the entire lifetime of the WebSocket connection.
    /// </summary>
    public Task ClosedTask => closedTcs.Task;

    private readonly WebSocket webSocket;
    private readonly ILogger logger;
    private readonly SemaphoreSlim sendLock = new(1, 1);
    private readonly TaskCompletionSource closedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int closed;

    public WebSocketClient(WebSocket webSocket, ILogger logger)
    {
        Id = Interlocked.Increment(ref nextClientId);
        this.webSocket = webSocket;
        this.logger = logger;
    }

    public bool IsConnected()
    {
        return Volatile.Read(ref closed) == 0 && webSocket.State == WebSocketState.Open;
    }

    public void CloseConnection()
    {
        if (Interlocked.Exchange(ref closed, 1) != 0)
        {
            return;
        }

        try
        {
            if (webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                using CancellationTokenSource cts = new(TimeSpan.FromSeconds(2));
                webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cts.Token).GetAwaiter().GetResult();
            }
            else if (webSocket.State != WebSocketState.Closed && webSocket.State != WebSocketState.Aborted)
            {
                webSocket.Abort();
            }
        }
        catch
        {
            try { webSocket.Abort(); } catch { }
        }
        finally
        {
            closedTcs.TrySetResult();
        }
    }

    public async Task<NetworkFrame> ReceiveAsync()
    {
        byte[] chunk = new byte[NetworkFrame.BufferSize];
        using MemoryStream ms = new();

        while (true)
        {
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(chunk), CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                CloseConnection();
                return new NetworkFrame(new List<MessageFrame>());
            }

            if (result.Count > 0)
            {
                ms.Write(chunk, 0, result.Count);
            }

            if (result.EndOfMessage)
            {
                break;
            }
        }

        byte[] payload = ms.ToArray();
        if (payload.Length < NetworkFrame.IndexFirstFrame)
        {
            return new NetworkFrame(new List<MessageFrame>());
        }

        return NetworkFrameProcess.Decode(payload);
    }

    public async Task SendAsync(NetworkFrame frame)
    {
        if (!IsConnected())
        {
            return;
        }

        foreach (MessageFrame messageFrame in frame.Frames)
        {
            logger.LogInformation("Sending [{commandType}] to ws client {clientId} : {message}", messageFrame.CommandType, Id, messageFrame.Message);
        }

        byte[] buffer = NetworkFrameProcess.Encode(frame);

        await sendLock.WaitAsync();
        try
        {
            if (!IsConnected())
            {
                return;
            }

            await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Binary, endOfMessage: true, CancellationToken.None);
        }
        finally
        {
            sendLock.Release();
        }
    }
}
