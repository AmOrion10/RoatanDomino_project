using Domain;

namespace Application;

public class DisconnectionResult(User? user)
{
    public int? LobbyId { get; set; }
    public User? UserConcerned { get; } = user;
    public LeaveGameResult? LeaveGameResult { get; set; } 
    public Chat GlobalChat { get; set; }
}
