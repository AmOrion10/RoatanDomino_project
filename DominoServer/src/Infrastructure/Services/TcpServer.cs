using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

using Application.Interfaces;

using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class TcpServer(ILogger<ITcpServer> logger) : ITcpServer
{
    private static int id = 1; // TODO: Autre manière de gérer les id des clients ?

    private TcpListener? tcpListener;
    private X509Certificate2? serverCertificat;

    public void StartListening(int port)
    {
        string? certFilePath = Environment.GetEnvironmentVariable("SslCertPemFilePath");
        string? keyFilePath = Environment.GetEnvironmentVariable("SslKeyPemFilePath");
        string? keyFilePassword = Environment.GetEnvironmentVariable("SslKeyPemFilePassword");
        if (!string.IsNullOrWhiteSpace(certFilePath) && !string.IsNullOrWhiteSpace(keyFilePath) && !string.IsNullOrWhiteSpace(keyFilePassword))
        {
            serverCertificat = X509Certificate2.CreateFromEncryptedPemFile(certFilePath, keyFilePassword, keyFilePath);
            serverCertificat = new X509Certificate2(serverCertificat.Export(X509ContentType.Pfx)); // https://stackoverflow.com/a/72101855
        }

        IPEndPoint endpoint = new(IPAddress.Any, port);
        tcpListener = new TcpListener(endpoint);
        tcpListener.Start();
    }

    public bool Running(CancellationToken token)
    {
        return !token.IsCancellationRequested;
    }

    public async Task<IClient> AcceptClientAsync()
    {
        TcpClient tcpClient = await tcpListener!.AcceptTcpClientAsync();
        return new Client(tcpClient, id++, logger, serverCertificat);
    }

    public async Task RejectConnectionAsync()
    {
        TcpClient client = await tcpListener!.AcceptTcpClientAsync();
        client.Close();
    }

    public bool ClientWantsToConnect()
    {
        return tcpListener!.Pending();
    }
}