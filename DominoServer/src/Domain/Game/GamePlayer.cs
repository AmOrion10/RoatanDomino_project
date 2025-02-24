namespace Domain.Game;

public class GamePlayer(User user) : User(user)
{
    public bool IsReady { get; set; } = false;
    public List<Domino> Hand { get; } = [];
    public int Bet { get; set; }
    public int Gain { get; set; }

    public void AddDominoToHand(Domino domino)
    {
        Hand.Add(domino);
    }
}
