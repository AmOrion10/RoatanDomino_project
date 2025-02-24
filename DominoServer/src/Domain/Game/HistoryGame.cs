using DominoCommon.com.domino.common.Scripts.Enums;

namespace Domain.Game;

public class HistoryGame(long id, DateTime startTime, DateTime endTime, GameMode mode, int bet, long? winnerId, int gameDuration, int turnDuration, List<GameMove> moves, List<HistoryGamePlayer> players)
{
    public long Id { get; } = id;
    public DateTime StartTime { get; set; } = startTime;
    public DateTime EndTime { get; set; } = endTime;
    public GameMode Mode { get; set; } = mode;
    public int TotalBet { get; set; } = bet;
    public long? WinnerId { get; set; } = winnerId;
    public int GeneralTimer { get; set; } = gameDuration;
    public int PlayerTurnTimer { get; set; } = turnDuration;
    public List<GameMove> Moves { get; } = moves;
    public List<HistoryGamePlayer> Players { get; } = players;
}