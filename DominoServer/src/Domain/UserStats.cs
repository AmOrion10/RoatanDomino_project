namespace Domain;

public class UserStats(int numberGamePlayed, int numberGameWon, int numberGameLost)
{
    public int NumberOfGamePlayed { get; } = numberGamePlayed;
    public int NumberOfGameWon { get; } = numberGameWon;
    public int NumberOfGameLost { get; } = numberGameLost;
}
