using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

using Application.Interfaces;

using Domain;

using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame;

using Infrastructure.ExtensionMethods;

using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class Client : IClient
{
    public int Id { get; }
    public bool IsLoggedIn { get; set; } = false;
    public User? User { get; set; } = null;
    private readonly TcpClient tcpClient;
    private readonly ILogger logger;
    private readonly NetworkStream? stream;
    private readonly SslStream? sslStream;
    private readonly bool useSsl;

    public Client(TcpClient tcpClient, int id, ILogger<ITcpServer> logger, X509Certificate2? serverCertificate)
    {
        Id = id;
        this.tcpClient = tcpClient;
        this.logger = logger;

        if (serverCertificate is null)
        {
            stream = tcpClient.GetStream();
            return;
        }

        useSsl = true;
        sslStream = new SslStream(tcpClient.GetStream(), false);
        sslStream.AuthenticateAsServer(serverCertificate, clientCertificateRequired: false, checkCertificateRevocation: true);
    }

    public bool IsConnected()
    {
        return tcpClient.IsConnected();
    }

    public void CloseConnection()
    {
        if (useSsl)
        {
            sslStream!.Close();
        }
        else
        {
            stream!.Close();
        }
        tcpClient.Close();
    }

    public async Task<NetworkFrame> ReceiveAsync()
    {
        var buffer = new byte[NetworkFrame.BufferSize];

        if (useSsl)
        {
            _ = await sslStream!.ReadAsync(buffer.AsMemory(0, NetworkFrame.BufferSize));
        }
        else
        {
            _ = await stream!.ReadAsync(buffer.AsMemory(0, NetworkFrame.BufferSize));
        }
        return NetworkFrameProcess.Decode(buffer);
    }

    public async Task SendAsync(NetworkFrame frame)
    {
        foreach (MessageFrame messageFrame in frame.Frames)
        {
            logger.LogInformation("Sending [{commandType}] to client {clientId} : {message}", messageFrame.CommandType, Id, messageFrame.Message);
        }

        byte[] buffer = NetworkFrameProcess.Encode(frame);

        if (useSsl)
        {
            await sslStream!.WriteAsync(buffer);
        }
        else
        {
            await stream!.WriteAsync(buffer);
        }
    }
}
