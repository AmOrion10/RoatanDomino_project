namespace Domain.Game;

public class HistoryGamePlayer(User user) : User(user)
{
    public int Bet { get; set; }
    public int Gain { get; set; }
}
