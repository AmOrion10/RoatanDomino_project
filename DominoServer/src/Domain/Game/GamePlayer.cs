namespace Domain.Game;

public class GamePlayer(User user) : User(user)
{
    public bool IsReady { get; set; } = false;
    public List<Domino> Hand { get; } = [];
    public int Bet { get; set; }
    public int Gain { get; set; }
    public int TeamId { get; set; } = -1;
    public int MatchScore { get; set; }
    public int ConsecutiveAutoPlays { get; set; }
    public bool IsBotControlled { get; set; }

    public void AddDominoToHand(Domino domino)
    {
        Hand.Add(domino);
    }

    public int HandPipSum()
    {
        return Hand.Sum(domino => domino.SumOfSides());
    }

    public void RecordManualAction()
    {
        ConsecutiveAutoPlays = 0;
        IsBotControlled = false;
    }

    public bool RecordTimeoutAutoPlay(int strikeLimit)
    {
        ConsecutiveAutoPlays++;
        if (ConsecutiveAutoPlays >= strikeLimit)
        {
            IsBotControlled = true;
        }
        return IsBotControlled;
    }

    public void ReclaimManualControl()
    {
        IsBotControlled = false;
        ConsecutiveAutoPlays = 0;
    }
}
