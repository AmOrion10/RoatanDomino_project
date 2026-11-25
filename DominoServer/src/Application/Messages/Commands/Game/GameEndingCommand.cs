using Application.Common;
using Application.Exceptions.Game;
using Application.Interfaces;
using Application.Interfaces.Managers;
using Application.Interfaces.Repositories;
using Application.Messages.Queries.Game;

using DominoCommon.com.domino.common.Scripts.Enums;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;

namespace Application.Messages.Commands.Game;

using Domain.Game;

public class GameEndingCommand(int gameId) : ICommand<Game>
{
    public int GameId { get; set; } = gameId;
}

public class GameEndingCommandHandler(
    IGameManager gameManager,
    IChatManager chatManager,
    IMessageDispatcher dispatcher,
    IUserRepository userRepository,
    ITeamChallengeEloUpdater? teamChallengeEloUpdater = null) : ICommandHandler<GameEndingCommand, Game>
{
    public async Task<Game> Handle(GameEndingCommand request, CancellationToken cancellationToken)
    {
        Game game = gameManager.GetById(request.GameId) ?? throw new GameNotFoundException($"Game with id {request.GameId} not found");
        game.GeneralTimer.HasStarted = false;
        game.PlayerTurnTimer.HasStarted = false;

        // Legacy Ranked score pot distribution (kept intact for legacy ranked mode)
        if (game.Mode == GameMode.Ranked)
        {
            game.DistributeScore();
            foreach (GamePlayer player in game.Players)
            {
                if (player.Id <= 0) continue;
                int newPlayerScore = player.Score + player.Gain;
                await userRepository.UpdateScore(player, newPlayerScore);
                player.Score = newPlayerScore;
            }
        }

        List<IClient> clientsInGame = await dispatcher.Dispatch(new GetAllClientsForGameQuery(game.Id));

        // Online-only 7-Tier Chess-style Domino Elo update (strictly requires online match with >= 2 real human players)
        bool isMatchCompleted =
            game.IsLegacyDrawAdvance ||
            game.IsMatchOver ||
            game.LastHandResolution is null ||
            game.LastHandResolution.MatchOver ||
            game.LastHandResolution.BreakTriggered;

        if (isMatchCompleted && !game.EloAppliedForCurrentMatch)
        {
            MatchEloBreakdownDto breakdown = RoatanEloCalculator.CalculateFromGame(game);
            game.LastMatchEloBreakdown = breakdown;
            game.EloAppliedForCurrentMatch = true;

            if (breakdown.IsRankedMatch)
            {
                DateTime completedUtc = DateTime.UtcNow;
                foreach (PlayerEloBreakdownDto entry in breakdown.Players)
                {
                    GamePlayer? player = game.Players.FirstOrDefault(p => p.Id == entry.PlayerId);
                    if (player is null || player.Id <= 0) continue;

                    RoatanEloCalculator.ApplyMatchUpdateToUser(player, entry, completedUtc);
                    await userRepository.UpdateRankedStats(player);

                    IClient? connectedClient = clientsInGame.FirstOrDefault(c => c.User is not null && c.User.Id == player.Id);
                    if (connectedClient?.User is not null)
                    {
                        connectedClient.User.Elo = player.Elo;
                        connectedClient.User.Wins = player.Wins;
                        connectedClient.User.Losses = player.Losses;
                        connectedClient.User.WinAssists = player.WinAssists;
                        connectedClient.User.TotalMoves = player.TotalMoves;
                        connectedClient.User.OptimalMoves = player.OptimalMoves;
                        connectedClient.User.AverageTurnSeconds = player.AverageTurnSeconds;
                        connectedClient.User.OnlineMatchesLast7Days = player.OnlineMatchesLast7Days;
                        connectedClient.User.LastOnlineMatchUtc = player.LastOnlineMatchUtc;
                    }
                }

                // Update Team Elo & Team W/L when a Team Challenge (Crew vs. Crew) match completes
                if (game.IsTeamChallenge &&
                    game.TeamAId.HasValue &&
                    game.TeamBId.HasValue &&
                    teamChallengeEloUpdater is not null)
                {
                    HandResolution? res = game.LastHandResolution;
                    bool isDraw = res is not null && res.IsDraw;
                    bool isKeyWin = res is not null && res.IsKeyWin;
                    bool isBreakWin = res is not null && res.BreakTriggered;

                    int winningSlot = -1;
                    if (!isDraw)
                    {
                        if (game.MatchWinningTeamId is 0 or 1)
                        {
                            winningSlot = game.MatchWinningTeamId;
                        }
                        else if (res is not null && res.WinningTeamId is 0 or 1)
                        {
                            winningSlot = res.WinningTeamId;
                        }
                        else if (game.MatchWinner is not null)
                        {
                            winningSlot = game.MatchWinner.TeamId;
                        }
                        else if (res?.Winner is not null)
                        {
                            winningSlot = res.Winner.TeamId;
                        }
                        else if (game.Classement.Count > 0)
                        {
                            winningSlot = game.Classement[0].TeamId;
                        }
                    }

                    TeamEloBreakdownDto? teamBreakdown = await teamChallengeEloUpdater.ApplyTeamChallengeMatchResultAsync(
                        game.TeamChallengeId,
                        game.TeamAId.Value,
                        game.TeamBId.Value,
                        winningSlot,
                        isDraw,
                        isKeyWin,
                        isBreakWin);
                    if (teamBreakdown is not null)
                    {
                        breakdown.TeamBreakdown = teamBreakdown;
                    }
                }
            }
        }

        foreach (IClient client in clientsInGame)
        {
            chatManager.ClientLeaveChat(client, game.ChatId);
        }

        return game;
    }
}
