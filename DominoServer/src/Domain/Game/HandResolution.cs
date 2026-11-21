namespace Domain.Game;

public class HandResolution
{
    public bool IsOver { get; init; }
    public bool WasBlocked { get; init; }
    public bool IsDraw { get; init; }
    public GamePlayer? Winner { get; init; }
    public int WinningTeamId { get; init; } = -1;
    public int PointsAwarded { get; init; }
    public bool BreakTriggered { get; init; }
    public long? NextPoserId { get; init; }
    public Dictionary<long, int> IndividualPipCounts { get; init; } = [];
}
