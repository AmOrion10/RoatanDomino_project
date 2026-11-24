using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

using DominoCommon.com.domino.common.Scripts.ExtensionMethods;
using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Friends;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;
using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame;

using UnityEngine;

public class Client : IClient
{
    public MessageFrameEventHandler EventHandlers { get; } = new();

    public UserDto User { get; private set; }
    public List<FriendDto> Friends { get; private set; } = new();
    public List<FriendRequestDto> FriendRequests { get; private set; } = new();

    private Guid userToken;

    // Transport mode: WebSocket (ws:// / wss://) or legacy raw TCP
    private readonly bool useWebSocket;
    private readonly Uri webSocketUri;
    private ClientWebSocket webSocket;
    private CancellationTokenSource wsCts;

    private readonly TcpClient tcpClient = new();
    private NetworkStream stream;
    private SslStream sslStream;
    private readonly string host;
    private readonly int port;
    private readonly bool useSsl;
    private readonly string targetHost;

    private readonly Queue<NetworkFrame> queue = new();
    private volatile bool running;
    private Task sendingLoopTask;
    private Task receivingLoopTask;

    public Client(int port, string host, bool useSsl, string targetHost)
    {
        this.useWebSocket = false;
        this.port = port;
        this.host = host;
        this.useSsl = useSsl;
        this.targetHost = targetHost;
    }

    public Client(Uri webSocketUri)
    {
        this.useWebSocket = true;
        this.webSocketUri = webSocketUri ?? throw new ArgumentNullException(nameof(webSocketUri));
        this.host = webSocketUri.Host;
        this.port = webSocketUri.Port;
        this.useSsl = string.Equals(webSocketUri.Scheme, "wss", StringComparison.OrdinalIgnoreCase);
        this.targetHost = webSocketUri.Host;
    }

    public void Start()
    {
        running = true;

        if (useWebSocket)
        {
            StartWebSocket();
        }
        else
        {
            StartTcp();
        }

        sendingLoopTask = Task.Run(StartSendingLoopAsync);
        receivingLoopTask = Task.Run(StartReceivingLoopAsync);
    }

    private void StartWebSocket()
    {
        Debug.Log($"Client trying to connect via WebSocket to {webSocketUri}");
        wsCts = new CancellationTokenSource();
        webSocket = new ClientWebSocket();
        webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);

        using CancellationTokenSource connectTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(wsCts.Token);
        connectTimeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

        webSocket.ConnectAsync(webSocketUri, connectTimeoutCts.Token).GetAwaiter().GetResult();
        Debug.Log($"Client connected via WebSocket to {webSocketUri}!");
    }

    private void StartTcp()
    {
        Debug.Log($"Client trying to connect to {host}:{port}");
        if (IPAddress.TryParse(host, out IPAddress ipAddress))
        {
            tcpClient.Connect(ipAddress, port);
        }
        else
        {
            tcpClient.Connect(host, port);
        }

        Debug.Log("Client connected!");
        if (useSsl)
        {
            sslStream = new SslStream(tcpClient.GetStream(), false, ValidateServerCertificate, null);
            sslStream.AuthenticateAsClient(targetHost);
            Debug.Log("Client authenticated!");
        }
        else
        {
            stream = tcpClient.GetStream();
        }
    }

    private async Task StartReceivingLoopAsync()
    {
        try
        {
            while (running)
            {
                if (useWebSocket)
                {
                    if (webSocket == null || webSocket.State != WebSocketState.Open)
                    {
                        break;
                    }

                    NetworkFrame networkFrame = await ReceiveWebSocketAsync();
                    if (networkFrame == null || networkFrame.NumberOfFrames == 0)
                    {
                        break;
                    }

                    DispatchReceivedFrame(networkFrame);
                }
                else
                {
                    if (!tcpClient.IsThereSomethingToRead())
                    {
                        await Task.Delay(5);
                        continue;
                    }

                    NetworkFrame networkFrame = await ReceiveAsync();
                    DispatchReceivedFrame(networkFrame);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            if (running)
            {
                Debug.LogError($"Error inside receiving loop : {ex}");
            }
        }
    }

    private void DispatchReceivedFrame(NetworkFrame networkFrame)
    {
        foreach (MessageFrame frame in networkFrame.Frames)
        {
            Debug.Log($"Received of type : {frame.CommandType} and subtype : {frame.SubCommandType}");
            CacheResults(frame);
            if (MainThread.Instance != null)
            {
                MainThread.Instance.AddActionToQueue(() => EventHandlers.MessageFrameReceive(frame));
            }
            else
            {
                EventHandlers.MessageFrameReceive(frame);
            }
        }
    }

    private void CacheResults(MessageFrame frame)
    {
        switch (frame.SubCommandType)
        {
            case FrameCommandType.Connection:
                SetUser(frame);
                break;
            case FrameCommandType.GetAllFriends:
                ResponseObjectDto<FriendsDto> responseFriends = frame.GetObjectFromMessage<ResponseObjectDto<FriendsDto>>();
                if (responseFriends.OkResult) Friends = responseFriends.Object.Friends;
                break;
            case FrameCommandType.GetAllFriendRequests:
                ResponseObjectDto<FriendRequestsDto> responseFriendRequests = frame.GetObjectFromMessage<ResponseObjectDto<FriendRequestsDto>>();
                if (responseFriendRequests.OkResult) FriendRequests = responseFriendRequests.Object.FriendRequests;
                break;
            case FrameCommandType.Disconnection:
                User = null;
                userToken = Guid.Empty;
                break;
            case FrameCommandType.RejoinMatch:
                ResponseObjectDto<RejoinMatchSnapshotDto> rejoinResp = frame.GetObjectFromMessage<ResponseObjectDto<RejoinMatchSnapshotDto>>();
                if (rejoinResp != null && rejoinResp.OkResult && rejoinResp.Object != null)
                {
                    if (rejoinResp.Object.User != null)
                    {
                        User = rejoinResp.Object.User;
                    }
                    if (!string.IsNullOrEmpty(rejoinResp.Object.AuthToken) && Guid.TryParse(rejoinResp.Object.AuthToken, out Guid parsedToken))
                    {
                        userToken = parsedToken;
                    }
                }
                break;
        }
    }

    private async Task StartSendingLoopAsync()
    {
        try
        {
            while (running)
            {
                NetworkFrame frame = null;
                lock (queue)
                {
                    if (queue.Count > 0)
                    {
                        frame = queue.Dequeue();
                    }
                }

                if (frame == null)
                {
                    if (useWebSocket && wsCts != null)
                    {
                        await Task.Delay(5, wsCts.Token);
                    }
                    else
                    {
                        await Task.Delay(5);
                    }
                    continue;
                }

                List<string> commandTypes = frame.Frames.Select(f => f.CommandType.ToString()).ToList();
                Debug.Log($"Need to send [{string.Join(", ", commandTypes)}]");

                await SendAsync(frame);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            if (running)
            {
                Debug.LogError($"Error inside sending loop : {ex}");
            }
        }
    }

    public void AddToSendQueue(MessageFrame frame)
    {
        lock (queue)
        {
            queue.Enqueue(new NetworkFrame(frame, User?.Id ?? 0, userToken));
        }
    }

    public void AddToSendQueue(List<MessageFrame> frames)
    {
        lock (queue)
        {
            queue.Enqueue(new NetworkFrame(frames, User?.Id ?? 0, userToken));
        }
    }

    public void Stop()
    {
        running = false;

        if (useWebSocket)
        {
            try { wsCts?.Cancel(); } catch { }
            try
            {
                if (webSocket != null && webSocket.State == WebSocketState.Open)
                {
                    using CancellationTokenSource closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                    webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", closeCts.Token).GetAwaiter().GetResult();
                }
            }
            catch
            {
                try { webSocket?.Abort(); } catch { }
            }
            try { webSocket?.Dispose(); } catch { }
            try { wsCts?.Dispose(); } catch { }
        }
        else
        {
            try
            {
                if (useSsl) sslStream?.Close();
                else stream?.Close();
                tcpClient?.Close();
            }
            catch { }
        }

        try { sendingLoopTask?.Wait(500); } catch { }
        try { receivingLoopTask?.Wait(500); } catch { }
        Debug.Log("Client disconnected!");
    }

    private async Task<NetworkFrame> ReceiveWebSocketAsync()
    {
        byte[] chunk = new byte[NetworkFrame.BufferSize];
        using MemoryStream ms = new MemoryStream();

        while (true)
        {
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(chunk),
                wsCts != null ? wsCts.Token : CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                running = false;
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

    public async Task<NetworkFrame> ReceiveAsync()
    {
        if (useWebSocket)
        {
            return await ReceiveWebSocketAsync();
        }

        var buffer = new byte[NetworkFrame.BufferSize];
        if (useSsl)
        {
            _ = await sslStream.ReadAsync(buffer.AsMemory(0, NetworkFrame.BufferSize));
        }
        else
        {
            _ = await stream.ReadAsync(buffer.AsMemory(0, NetworkFrame.BufferSize));
        }
        return NetworkFrameProcess.Decode(buffer);
    }

    public async Task SendAsync(NetworkFrame frame)
    {
        byte[] buffer = NetworkFrameProcess.Encode(frame);
        if (useWebSocket)
        {
            if (webSocket == null || webSocket.State != WebSocketState.Open) return;
            await webSocket.SendAsync(
                new ArraySegment<byte>(buffer),
                WebSocketMessageType.Binary,
                endOfMessage: true,
                wsCts != null ? wsCts.Token : CancellationToken.None);
            return;
        }

        if (useSsl)
        {
            await sslStream.WriteAsync(buffer);
        }
        else
        {
            await stream.WriteAsync(buffer);
        }
    }

    private void SetUser(MessageFrame frame)
    {
        ResponseObjectDto<ConnectionResponseDto> dto = frame.GetObjectFromMessage<ResponseObjectDto<ConnectionResponseDto>>();
        if (!dto.OkResult) return;

        User = dto.Object.User;
        userToken = new Guid(dto.Object.Token);
    }

    private static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
    {
        // Here we check for "RemoteCertificateChainErrors" because we have a self-signed certificate so it could be the error
        // But we only want this error at most, and no others
        return sslPolicyErrors is SslPolicyErrors.None or SslPolicyErrors.RemoteCertificateChainErrors;
    }
}
