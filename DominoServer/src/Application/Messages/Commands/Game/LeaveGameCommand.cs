using Application.Common;
using Application.Exceptions.Chat;
using Application.Exceptions.Game;
using Application.Interfaces;
using Application.Interfaces.Managers;
using Application.Messages.Queries.Game;

namespace Application.Messages.Commands.Game;

using Domain.Game;

public class LeaveGameCommand(IClient playerLeaving) : ICommand<LeaveGameResult>
{
    public IClient PlayerLeaving { get; set; } = playerLeaving;
}

public class LeaveGameCommandHandler(IGameManager gameManager, IMessageDispatcher dispatcher, IChatManager chatManager) : ICommandHandler<LeaveGameCommand, LeaveGameResult>
{
    public async Task<LeaveGameResult> Handle(LeaveGameCommand request, CancellationToken cancellationToken)
    {
        LeaveGameResult res = new();
        
        Game game = gameManager.GetForClient(request.PlayerLeaving) ?? throw new GameNotFoundForPlayerException("Game was not found");
        res.Game = game;
        res.Chat = chatManager.GetById(game.ChatId) ?? throw new ChatNotFoundException($"Chat with id {game.ChatId} not found");
        bool playerWhoLeftWasThePlayerWhoIsPlaying;

        /*
         * Here it's a bit tricky, because depending on a combination of circumstances,
         * there may be a forced player change at the same time as a player quits.
         */
        try
        {
            playerWhoLeftWasThePlayerWhoIsPlaying = gameManager.PlayerLeave(game.Id, request.PlayerLeaving);
        }
        catch
        {
            await Task.Delay(1000, cancellationToken);
            playerWhoLeftWasThePlayerWhoIsPlaying = gameManager.PlayerLeave(game.Id, request.PlayerLeaving);
        }

        res.PlayerTurnChanged = playerWhoLeftWasThePlayerWhoIsPlaying;
        res.ClientsInGame = await dispatcher.Dispatch(new GetAllClientsForGameQuery(game.Id));

        if (game.Players.Count <= 1)
        {
            gameManager.DeleteGame(game.Id);
            res.Game = null;
        }
        return res;
    }
}