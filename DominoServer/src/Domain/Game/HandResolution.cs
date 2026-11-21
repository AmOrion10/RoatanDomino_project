using DominoCommon.com.domino.common.Scripts.Enums;

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
    public bool MatchOver { get; init; }
    public GamePlayer? MatchWinner { get; init; }
    public int MatchWinningTeamId { get; init; } = -1;
    public bool RequiresChoosePoser { get; init; }
    public long? PoserChooserId { get; init; }
    public long? PartnerCandidateId { get; init; }
    public long? NextPoserId { get; init; }
    public bool CanPoserPlayAnyTile { get; init; }
    public List<long> CutThroatedPlayerIds { get; init; } = [];
    public List<PostHandAction> AvailablePostHandActions { get; init; } = [];
    public Dictionary<long, int> IndividualPipCounts { get; init; } = [];
    public Dictionary<int, int> TeamScoresSnapshot { get; init; } = [];
    public Dictionary<long, int> PlayerScoresSnapshot { get; init; } = [];
}
