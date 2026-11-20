using System;
using System.Collections.Generic;
using System.Linq;

using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;

public class DominoBotAI
{
    private readonly Random rng;
    private readonly Dictionary<long, HashSet<int>> knownPlayerVoids = new Dictionary<long, HashSet<int>>();

    public DominoBotAI(int? seed = null)
    {
        rng = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public void RecordPlayerBlockedOnEnds(long playerId, List<int> openEnds)
    {
        if (!knownPlayerVoids.TryGetValue(playerId, out HashSet<int> voids))
        {
            voids = new HashSet<int>();
            knownPlayerVoids[playerId] = voids;
        }

        foreach (int pip in openEnds)
        {
            voids.Add(pip);
        }
    }

    public void ClearPlayerVoidsOnDraw(long playerId)
    {
        // When a player draws from the boneyard they might have picked up a previously void pip
        if (knownPlayerVoids.ContainsKey(playerId))
        {
            knownPlayerVoids[playerId].Clear();
        }
    }

    public MoveDto ChooseMove(
        OfflineGameEngine engine,
        OfflineGameEngine.OfflinePlayer bot,
        List<MoveDto> legalMoves,
        BotDifficulty difficulty)
    {
        if (legalMoves == null || legalMoves.Count == 0)
        {
            return null;
        }

        if (legalMoves.Count == 1)
        {
            return legalMoves[0];
        }

        switch (difficulty)
        {
            case BotDifficulty.Beginner:
                return ChooseBeginnerMove(legalMoves);
            case BotDifficulty.Novice:
                return ChooseNoviceMove(engine, bot, legalMoves);
            case BotDifficulty.Pro:
                return ChooseProMove(engine, bot, legalMoves);
            default:
                return legalMoves[0];
        }
    }

    private MoveDto ChooseBeginnerMove(List<MoveDto> legalMoves)
    {
        // Beginner plays mostly randomly, with a gentle bias toward lighter tiles so it is forgiving
        if (rng.NextDouble() < 0.70)
        {
            return legalMoves[rng.Next(legalMoves.Count)];
        }

        return legalMoves
            .OrderBy(m => m.Domino.Side1 + m.Domino.Side2)
            .ThenBy(_ => rng.Next())
            .First();
    }

    private MoveDto ChooseNoviceMove(
        OfflineGameEngine engine,
        OfflineGameEngine.OfflinePlayer bot,
        List<MoveDto> legalMoves)
    {
        List<KeyValuePair<MoveDto, double>> scored = new List<KeyValuePair<MoveDto, double>>();

        foreach (MoveDto move in legalMoves)
        {
            double score = 0.0;
            int pipSum = move.Domino.Side1 + move.Domino.Side2;

            // Shed heavier dominoes and doubles
            score += pipSum * 2.2;
            if (move.Domino.IsDouble())
            {
                score += 4.0;
            }

            // Prefer leaving an open end we still hold matching dominoes for
            int exposedPip = engine.GetExposedPipAfterMove(move);
            int remainingMatches = CountMatchingTilesInRemainingHand(bot.Hand, move.Domino, exposedPip);
            score += remainingMatches * 2.8;

            // Small human-like variation
            score += (rng.NextDouble() * 3.0) - 1.5;

            scored.Add(new KeyValuePair<MoveDto, double>(move, score));
        }

        scored.Sort((a, b) => b.Value.CompareTo(a.Value));
        return scored[0].Key;
    }

    private MoveDto ChooseProMove(
        OfflineGameEngine engine,
        OfflineGameEngine.OfflinePlayer bot,
        List<MoveDto> legalMoves)
    {
        OfflineGameEngine.OfflinePlayer nextPlayer = engine.PeekNextPlayer();
        bool anyOpponentLow = engine.Players.Any(p => p.Info.Id != bot.Info.Id && p.Hand.Count <= 2);
        double blockWeight = anyOpponentLow ? 1.5 : 1.0;

        // Precompute played counts per pip (each pip 0..6 appears on 7 dominoes total)
        int[] playedPerPip = new int[7];
        foreach (DominoDto played in engine.PlayedDominoes)
        {
            playedPerPip[played.Side1]++;
            if (!played.IsDouble())
            {
                playedPerPip[played.Side2]++;
            }
        }

        MoveDto bestMove = legalMoves[0];
        double bestScore = double.NegativeInfinity;

        foreach (MoveDto move in legalMoves)
        {
            double score = 0.0;
            DominoDto domino = move.Domino;
            int pipSum = domino.Side1 + domino.Side2;
            int exposedPip = engine.GetExposedPipAfterMove(move);
            List<int> resultingEnds = engine.GetOpenEndsAfterMove(move);

            // 1. Shed high pips early so blocked games favor the bot
            score += pipSum * 2.4;

            // 2. Manage doubles: uncovered doubles are dangerous liabilities
            if (domino.IsDouble())
            {
                int coverCount = CountMatchingTilesInRemainingHand(bot.Hand, domino, domino.Side1);
                score += coverCount == 0 ? 6.5 : 3.5;
            }

            // 3. Self-continuity: how playable is our remaining hand on resulting open ends?
            int playableAfter = CountPlayableRemainingDominoes(bot.Hand, domino, resultingEnds);
            score += playableAfter * 3.2;

            int ownOfExposed = CountMatchingTilesInRemainingHand(bot.Hand, domino, exposedPip);
            score += ownOfExposed * 2.0;

            // Penalty if this move leaves us with zero matching tiles on BOTH open ends (unless last tile)
            if (bot.Hand.Count > 1 && playableAfter == 0)
            {
                score -= 7.5;
            }

            // 4. Exploit inferred opponent voids (card-counting pass/draw history)
            foreach (OfflineGameEngine.OfflinePlayer opp in engine.Players)
            {
                if (opp.Info.Id == bot.Info.Id) continue;
                if (!knownPlayerVoids.TryGetValue(opp.Info.Id, out HashSet<int> oppVoids) || oppVoids.Count == 0)
                {
                    continue;
                }

                bool isImmediateNext = opp.Info.Id == nextPlayer.Info.Id;
                if (oppVoids.Contains(exposedPip))
                {
                    score += (isImmediateNext ? 11.0 : 5.5) * blockWeight;
                }

                // Huge bonus if BOTH open ends are known voids for the next player!
                if (isImmediateNext && resultingEnds.Count >= 2 && resultingEnds.All(e => oppVoids.Contains(e)))
                {
                    score += 12.0 * blockWeight;
                }
            }

            // 5. Suit scarcity / lock control: count how many unseen tiles of exposedPip remain outside our hand
            int ownCountAll = CountPipInHand(bot.Hand, exposedPip);
            int unseenOfSuit = Math.Max(0, 7 - playedPerPip[exposedPip] - ownCountAll);
            if (unseenOfSuit == 0 && engine.BoneyardCount == 0)
            {
                // Nobody else holds this pip at all!
                int remainingPipSum = bot.HandPipSum() - pipSum;
                score += remainingPipSum <= 18 ? 10.0 : 3.0;
            }
            else if (unseenOfSuit <= 2 && ownOfExposed > 0)
            {
                // Bot controls a scarce suit
                score += 4.0;
            }

            // 6. Keep suit diversity in remaining hand so bot stays flexible
            int distinctPipsLeft = CountDistinctPipsRemaining(bot.Hand, domino);
            score += distinctPipsLeft * 1.1;

            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }
        }

        return bestMove;
    }

    private static int CountMatchingTilesInRemainingHand(List<DominoDto> hand, DominoDto played, int pip)
    {
        int count = 0;
        bool skippedPlayed = false;
        foreach (DominoDto d in hand)
        {
            if (!skippedPlayed && d.Side1 == played.Side1 && d.Side2 == played.Side2)
            {
                skippedPlayed = true;
                continue;
            }
            if (d.Side1 == pip || d.Side2 == pip)
            {
                count++;
            }
        }
        return count;
    }

    private static int CountPlayableRemainingDominoes(List<DominoDto> hand, DominoDto played, List<int> openEnds)
    {
        int count = 0;
        bool skippedPlayed = false;
        foreach (DominoDto d in hand)
        {
            if (!skippedPlayed && d.Side1 == played.Side1 && d.Side2 == played.Side2)
            {
                skippedPlayed = true;
                continue;
            }
            if (openEnds.Contains(d.Side1) || openEnds.Contains(d.Side2))
            {
                count++;
            }
        }
        return count;
    }

    private static int CountPipInHand(List<DominoDto> hand, int pip)
    {
        int count = 0;
        foreach (DominoDto d in hand)
        {
            if (d.Side1 == pip || d.Side2 == pip)
            {
                count++;
            }
        }
        return count;
    }

    private static int CountDistinctPipsRemaining(List<DominoDto> hand, DominoDto played)
    {
        HashSet<int> pips = new HashSet<int>();
        bool skippedPlayed = false;
        foreach (DominoDto d in hand)
        {
            if (!skippedPlayed && d.Side1 == played.Side1 && d.Side2 == played.Side2)
            {
                skippedPlayed = true;
                continue;
            }
            pips.Add(d.Side1);
            pips.Add(d.Side2);
        }
        return pips.Count;
    }
}
