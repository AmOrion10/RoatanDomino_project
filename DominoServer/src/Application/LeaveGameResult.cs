using Application.Interfaces;

using Domain;
using Domain.Game;

namespace Application;

public class LeaveGameResult
{
    public Game? Game { get; set; }
    public List<IClient> ClientsInGame { get; set; } = [];
    public Chat Chat { get; set; }
    public bool PlayerTurnChanged { get; set; }
}