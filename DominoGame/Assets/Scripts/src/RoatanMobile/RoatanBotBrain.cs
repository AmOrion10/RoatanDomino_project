using System;
using System.Collections.Generic;
using System.Linq;

using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;

public class RoatanBotBrain
{
    private readonly Random rng;
    private readonly Dictionary<int, HashSet<int>> playerVoids = new Dictionary<int, HashSet<int>>();

    public RoatanBotBrain(int? seed = null)
    {
        rng = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public void ResetForRound()
    {
        playerVoids.Clear();
    }

    public void RecordBlocked(int playerId, int leftEnd, int rightEnd)
    {
        if (!playerVoids.TryGetValue(playerId, out HashSet<int> voids))
        {
            voids = new HashSet<int>();
            playerVoids[playerId] = voids;
        }

        if (leftEnd >= 0) voids.Add(leftEnd);
        if (rightEnd >= 0) voids.Add(rightEnd);
    }

    public void ClearVoidsAfterDraw(int playerId)
    {
        if (playerVoids.ContainsKey(playerId))
        {
            playerVoids[playerId].Clear();
        }
    }

    public RoatanMatchEngine.LegalMove ChooseMove(
        RoatanMatchEngine engine,
        RoatanMatchEngine.MatchPlayer bot,
        List<RoatanMatchEngine.LegalMove> legalMoves,
        BotDifficulty difficulty)
    {
        if (legalMoves == null || legalMoves.Count == 0) return null;
        if (legalMoves.Count == 1) return legalMoves[0];

        switch (difficulty)
        {
            case BotDifficulty.Beginner:
                return ChooseBeginner(legalMoves);
            case BotDifficulty.Novice:
                return ChooseNovice(bot, legalMoves);
            case BotDifficulty.Pro:
                return ChoosePro(engine, bot, legalMoves);
            default:
                return legalMoves[0];
        }
    }

    private RoatanMatchEngine.LegalMove ChooseBeginner(List<RoatanMatchEngine.LegalMove> moves)
    {
        // Beginner plays mostly randomly with slight bias toward smaller tiles
        if (rng.NextDouble() < 0.72)
        {
            return moves[rng.Next(moves.Count)];
        }

        return moves
            .OrderBy(m => m.Domino.Side1 + m.Domino.Side2)
            .ThenBy(_ => rng.Next())
            .First();
    }

    private RoatanMatchEngine.LegalMove ChooseNovice(
        RoatanMatchEngine.MatchPlayer bot,
        List<RoatanMatchEngine.LegalMove> moves)
    {
        RoatanMatchEngine.LegalMove best = moves[0];
        double bestScore = double.NegativeInfinity;

        foreach (RoatanMatchEngine.LegalMove move in moves)
        {
            int pipSum = move.Domino.Side1 + move.Domino.Side2;
            double score = pipSum * 2.2;

            if (move.Domino.IsDouble())
            {
                score += 4.2;
            }

            int matchingInHand = CountPipRemaining(bot.Hand, move.Domino, move.ExposedPip);
            score += matchingInHand * 2.9;

            // Slight natural variation
            score += (rng.NextDouble() * 2.8) - 1.4;

            if (score > bestScore)
            {
                bestScore = score;
                best = move;
            }
        }

        return best;
    }

    private RoatanMatchEngine.LegalMove ChoosePro(
        RoatanMatchEngine engine,
        RoatanMatchEngine.MatchPlayer bot,
        List<RoatanMatchEngine.LegalMove> moves)
    {
        RoatanMatchEngine.MatchPlayer nextPlayer = engine.PeekNextPlayer();
        bool anyOppLow = engine.Players.Any(p => p.Id != bot.Id && p.Hand.Count <= 2);
        double blockWeight = anyOppLow ? 1.55 : 1.0;

        int[] playedPerPip = new int[7];
        foreach (DominoDto played in engine.PlayedDominoes)
        {
            playedPerPip[played.Side1]++;
            if (!played.IsDouble())
            {
                playedPerPip[played.Side2]++;
            }
        }

        RoatanMatchEngine.LegalMove best = moves[0];
        double bestScore = double.NegativeInfinity;

        foreach (RoatanMatchEngine.LegalMove move in moves)
        {
            int pipSum = move.Domino.Side1 + move.Domino.Side2;
            int otherEnd = move.End == BoardEnd.Left ? engine.RightEndPip : engine.LeftEndPip;
            int exposed = move.ExposedPip;

            double score = pipSum * 2.4;

            // Uncovered doubles are risky liabilities — shed them smartly
            if (move.Domino.IsDouble())
            {
                int cover = CountPipRemaining(bot.Hand, move.Domino, move.Domino.Side1);
                score += cover == 0 ? 7.0 : 3.5;
            }

            // Self-continuity on the resulting two ends
            int playableNext = CountPlayableRemaining(bot.Hand, move.Domino, exposed, otherEnd);
            score += playableNext * 3.3;

            int ownExposed = CountPipRemaining(bot.Hand, move.Domino, exposed);
            score += ownExposed * 2.1;

            if (bot.Hand.Count > 1 && playableNext == 0)
            {
                score -= 8.0;
            }

            // Exploit inferred opponent voids (passes / draws)
            foreach (RoatanMatchEngine.MatchPlayer opp in engine.Players)
            {
                if (opp.Id == bot.Id) continue;
                if (!playerVoids.TryGetValue(opp.Id, out HashSet<int> voids) || voids.Count == 0) continue;

                bool isNext = opp.Id == nextPlayer.Id;
                if (voids.Contains(exposed))
                {
                    score += (isNext ? 11.5 : 5.5) * blockWeight;
                }

                if (isNext && voids.Contains(exposed) && voids.Contains(otherEnd))
                {
                    score += 13.0 * blockWeight;
                }
            }

            // Card-counting lock / suit control
            int ownAllExposed = CountPipTotal(bot.Hand, exposed);
            int unseenOfSuit = Math.Max(0, 7 - playedPerPip[exposed] - ownAllExposed);
            if (unseenOfSuit == 0 && engine.BoneyardCount == 0)
            {
                int remainingPips = bot.HandPipSum() - pipSum;
                // Locking the board when we hold fewer pips wins blocked (Cerrado) games!
                score += remainingPips <= 16 ? 11.0 : 2.5;
            }
            else if (unseenOfSuit <= 2 && ownExposed > 0)
            {
                score += 4.2;
            }

            // Keep suit diversity
            score += CountDistinctPipsRemaining(bot.Hand, move.Domino) * 1.1;

            if (score > bestScore)
            {
                bestScore = score;
                best = move;
            }
        }

        return best;
    }

    private static int CountPipRemaining(List<DominoDto> hand, DominoDto played, int pip)
    {
        int count = 0;
        bool skipped = false;
        foreach (DominoDto d in hand)
        {
            if (!skipped && d.Side1 == played.Side1 && d.Side2 == played.Side2)
            {
                skipped = true;
                continue;
            }
            if (d.Side1 == pip || d.Side2 == pip) count++;
        }
        return count;
    }

    private static int CountPlayableRemaining(List<DominoDto> hand, DominoDto played, int endA, int endB)
    {
        int count = 0;
        bool skipped = false;
        foreach (DominoDto d in hand)
        {
            if (!skipped && d.Side1 == played.Side1 && d.Side2 == played.Side2)
            {
                skipped = true;
                continue;
            }
            if (d.Side1 == endA || d.Side2 == endA || d.Side1 == endB || d.Side2 == endB)
            {
                count++;
            }
        }
        return count;
    }

    private static int CountPipTotal(List<DominoDto> hand, int pip)
    {
        int count = 0;
        foreach (DominoDto d in hand)
        {
            if (d.Side1 == pip || d.Side2 == pip) count++;
        }
        return count;
    }

    private static int CountDistinctPipsRemaining(List<DominoDto> hand, DominoDto played)
    {
        HashSet<int> pips = new HashSet<int>();
        bool skipped = false;
        foreach (DominoDto d in hand)
        {
            if (!skipped && d.Side1 == played.Side1 && d.Side2 == played.Side2)
            {
                skipped = true;
                continue;
            }
            pips.Add(d.Side1);
            pips.Add(d.Side2);
        }
        return pips.Count;
    }
}
