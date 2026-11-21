using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
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

    private readonly TcpClient tcpClient = new();
    private NetworkStream stream;
    private SslStream sslStream;
    private readonly string host;
    private readonly int port;
    private readonly bool useSsl;
    private readonly string targetHost;

    private readonly Queue<NetworkFrame> queue = new();
    private bool running = true;
    private Task sendingLoopTask;
    private Task receivingLoopTask;

    public Client(int port, string host, bool useSsl, string targetHost)
    {
        this.port = port;
        this.host = host;
        this.useSsl = useSsl;
        this.targetHost = targetHost;
    }

    public void Start()
    {
        Debug.Log($"Client trying to connect to {host}:{port}");
        tcpClient.Connect(IPAddress.Parse(host), port);
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
        sendingLoopTask = Task.Factory.StartNew(async () => await StartSendingLoopAsync(), TaskCreationOptions.LongRunning);
        receivingLoopTask = Task.Factory.StartNew(async () => await StartReceivingLoopAsync(), TaskCreationOptions.LongRunning);
    }

    private async Task StartReceivingLoopAsync()
    {
        try
        {
            while (running)
            {
                if (!tcpClient.IsThereSomethingToRead()) continue;
                NetworkFrame networkFrame = await ReceiveAsync();
                foreach (MessageFrame frame in networkFrame.Frames)
                {
                    Debug.Log($"Received of type : {frame.CommandType} and subtype : {frame.SubCommandType}");
                    CacheResults(frame);
                    MainThread.Instance.AddActionToQueue(() => EventHandlers.MessageFrameReceive(frame));
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error inside receiving loop : {ex}");
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
                NetworkFrame frame;
                lock (queue)
                {
                    if (!queue.Any()) continue;
                    frame = queue.Dequeue();
                }

                List<string> commandTypes = frame.Frames.Select(f => f.CommandType.ToString()).ToList();
                Debug.Log($"Need to send [{string.Join(", " , commandTypes)}]");

                await SendAsync(frame);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error inside sending loop : {ex}");
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
        sendingLoopTask.Wait();
        receivingLoopTask.Wait();
        if (useSsl) sslStream.Close();
        else stream.Close();
        tcpClient.Close();
        Debug.Log("Client disconnected!");
    }

    public async Task<NetworkFrame> ReceiveAsync()
    {
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
