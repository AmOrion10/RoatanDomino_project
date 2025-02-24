using System.Net.Sockets;

namespace Infrastructure.ExtensionMethods;

public static class TcpClientExtensionMethods
{
    public static bool IsConnected(this TcpClient tcpClient)
    {
        return !(tcpClient.Client.Poll(1000, SelectMode.SelectRead) && tcpClient.Client.Available == 0);
    }
}
