using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;

namespace Application.Interfaces;

public interface ITeamChallengeEloUpdater
{
    Task<TeamEloBreakdownDto?> ApplyTeamChallengeMatchResultAsync(
        long? challengeId,
        long teamAId,
        long teamBId,
        int winningTeamSlot,
        bool isDraw,
        bool isKeyWin,
        bool isGameBrukWin);

    Task<TeamEloBreakdownDto?> ApplyTeamChallengeAbandonmentPenaltyAsync(
        long? challengeId,
        long teamAId,
        long teamBId,
        long abandoningTeamId);
}
