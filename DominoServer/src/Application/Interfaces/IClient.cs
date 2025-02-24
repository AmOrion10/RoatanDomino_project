using Domain;

using DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame;

namespace Application.Interfaces;

public interface IClient
{
    int Id { get; } // Client id
    bool IsLoggedIn { get; set; }
    User? User { get; set; } // Updated when logged in

    bool IsConnected(); // In terms of network connection
    void CloseConnection();
    Task<NetworkFrame> ReceiveAsync();
    Task SendAsync(NetworkFrame frame);
}
