using Application.Common;
using Application.Interfaces.Managers;

using DominoCommon.com.domino.common.Scripts.Enums;

namespace Application.Messages.Commands.Game;

using Application.Exceptions.Game;
using Application.Interfaces.Repositories;

using Domain;
using Domain.Game;

public class GameStartingCommand(int gameId) : ICommand<Game>
{
    public int GameId { get; set; } = gameId;
}

public class GameStartingCommandHandler(IGameManager gameManager, IUserRepository userRepository) : ICommandHandler<GameStartingCommand, Game>
{
    public async Task<Game> Handle(GameStartingCommand request, CancellationToken cancellationToken)
    {
        Game game = gameManager.GetById(request.GameId) ?? throw new GameNotFoundException($"Game with id {request.GameId} not found");
        if (game.Mode == GameMode.Ranked)
        {
            game.CalculateTotalBet();
            foreach (GamePlayer player in game.Players)
            {
                int newPlayerScore = player.Score - player.Bet;
                if (newPlayerScore >= 100)
                {
                    await userRepository.UpdateScore(player, newPlayerScore);
                    player.Score = newPlayerScore;
                }
            }
        }
        game.DistributeDominoes();
        game.StartTimer.HasStarted = false;
        return game;
    }
}
