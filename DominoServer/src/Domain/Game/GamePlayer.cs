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
    public bool IsDisconnected { get; set; }
    public bool IsPermanentlyBot { get; set; }
    public bool IsHumanSeat { get; set; } = user.Id > 0;
    public string? BotDifficulty { get; set; }
    public double DisconnectGraceRemainingSeconds { get; set; }
    public string ReconnectToken { get; set; } = string.Empty;
    public DateTime? DisconnectedAtUtc { get; set; }

    // Per-match telemetry for Elo performance modifiers
    public int MatchMovesCount { get; set; }
    public int MatchOptimalMovesCount { get; set; }
    public int MatchForcedPasses { get; set; }
    public int MatchTrappedDoubles { get; set; }
    public int MatchWinAssists { get; set; }
    public double MatchTotalTurnSeconds { get; set; }
    public int MatchTimedTurns { get; set; }
    public double MatchAverageTurnSeconds => MatchTimedTurns > 0 ? Math.Round(MatchTotalTurnSeconds / MatchTimedTurns, 2) : 0.0;

    public void ResetMatchTelemetry()
    {
        MatchMovesCount = 0;
        MatchOptimalMovesCount = 0;
        MatchForcedPasses = 0;
        MatchTrappedDoubles = 0;
        MatchWinAssists = 0;
        MatchTotalTurnSeconds = 0.0;
        MatchTimedTurns = 0;
    }

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
        if (!IsPermanentlyBot)
        {
            IsBotControlled = false;
        }
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
        if (IsPermanentlyBot) return;
        IsBotControlled = false;
        ConsecutiveAutoPlays = 0;
    }

    public void MarkDisconnected(double graceSeconds)
    {
        if (IsPermanentlyBot) return;
        IsDisconnected = true;
        DisconnectGraceRemainingSeconds = graceSeconds;
        DisconnectedAtUtc = DateTime.UtcNow;
    }

    public void ConvertToPermanentBot()
    {
        IsPermanentlyBot = true;
        IsBotControlled = true;
        IsDisconnected = true;
        DisconnectGraceRemainingSeconds = 0.0;
        ReconnectToken = string.Empty;
    }

    public void RestoreFromReconnect()
    {
        IsDisconnected = false;
        IsBotControlled = false;
        ConsecutiveAutoPlays = 0;
        DisconnectGraceRemainingSeconds = 0.0;
        DisconnectedAtUtc = null;
    }
}
