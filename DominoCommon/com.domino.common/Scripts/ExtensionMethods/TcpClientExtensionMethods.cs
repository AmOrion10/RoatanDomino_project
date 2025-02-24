using System.Net.Sockets;

namespace DominoCommon.com.domino.common.Scripts.ExtensionMethods
{
    public static class TcpClientExtensionMethods
    {
        public static bool IsThereSomethingToRead(this TcpClient tcpClient)
        {
            return tcpClient.Client.Available > 0;
        }
    }
}
