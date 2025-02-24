namespace Domain.Game;

public class MoveRecorder
{
    public List<GameMove> MoveHistory { get; } = [];

    public void AddToHistory(GameMove move)
    {
        MoveHistory.Add(move);
    }

    public GameMove LastMove()
    {
        return MoveHistory.Last();
    }
}
