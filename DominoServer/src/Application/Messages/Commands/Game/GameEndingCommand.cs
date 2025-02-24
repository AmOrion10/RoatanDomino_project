using Application.Common;
using Application.Interfaces;
using Application.Interfaces.Managers;
using Application.Messages.Queries.Game;

using DominoCommon.com.domino.common.Scripts.Enums;

namespace Application.Messages.Commands.Game;

using Application.Exceptions.Game;
using Application.Interfaces.Repositories;

using Domain.Game;

public class GameEndingCommand(int gameId) : ICommand<Game>
{
    public int GameId { get; set; } = gameId;
}

public class GameEndingCommandHandler(IGameManager gameManager, IChatManager chatManager, IMessageDispatcher dispatcher, IUserRepository userRepository) : ICommandHandler<GameEndingCommand, Game>
{
    public async Task<Game> Handle(GameEndingCommand request, CancellationToken cancellationToken)
    {
        Game game = gameManager.GetById(request.GameId) ?? throw new GameNotFoundException($"Game with id {request.GameId} not found"); ;
        game.GeneralTimer.HasStarted = false;
        game.PlayerTurnTimer.HasStarted = false;

        if (game.Mode == GameMode.Ranked)
        {
            game.DistributeScore();
            foreach (GamePlayer player in game.Players)
            {
                int newPlayerScore = player.Score + player.Gain;
                await userRepository.UpdateScore(player, newPlayerScore);
                player.Score = newPlayerScore;
            }
        }

        List<IClient> clientsInGame = await dispatcher.Dispatch(new GetAllClientsForGameQuery(game.Id));
        foreach (IClient client in clientsInGame)
        {
            chatManager.ClientLeaveChat(client, game.ChatId);
        }

        return game;
    }
}
