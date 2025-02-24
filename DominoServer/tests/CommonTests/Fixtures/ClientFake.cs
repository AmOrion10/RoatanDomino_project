using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

using Infrastructure.ExtensionMethods;

namespace CommonTests.Fixtures;

public class ClientFake : IDisposable
{
    public readonly TcpClient TcpClient = new();

    private static bool ValidateServerCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
    {
        return true;
    }

    public async Task ConnectToServerAsync(string host, int port)
    {
        IPEndPoint endpoint = new(IPAddress.Parse(host), port);
        await TcpClient.ConnectAsync(endpoint);

        string? certFilePath = Environment.GetEnvironmentVariable("SslCertPemFilePath");
        string? keyFilePath = Environment.GetEnvironmentVariable("SslKeyPemFilePath");
        string? keyFilePassword = Environment.GetEnvironmentVariable("SslKeyPemFilePassword");
        if (!string.IsNullOrWhiteSpace(certFilePath) && !string.IsNullOrWhiteSpace(keyFilePath) && !string.IsNullOrWhiteSpace(keyFilePassword))
        {
            await Task.Delay(500);
            if (!IsConnected()) return;

            SslStream sslStream = new(TcpClient.GetStream(), false, ValidateServerCertificate, null);

            await sslStream.AuthenticateAsClientAsync("domino6b");
        }
    }

    public bool IsConnected()
    {
        return TcpClient.IsConnected();
    }

    public void Dispose()
    {
        if (TcpClient.Connected) TcpClient.Close();
        GC.SuppressFinalize(this);
    }
}
