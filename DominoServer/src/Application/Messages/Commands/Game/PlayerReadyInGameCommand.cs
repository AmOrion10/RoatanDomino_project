using Application.Common;
using Application.Interfaces;
using Application.Interfaces.Managers;

namespace Application.Messages.Commands.Game;

using Application.Exceptions.Game;
using Domain.Game;

public class PlayerReadyInGameCommand(IClient client) : ICommand<int>
{
    public IClient Client { get; set; } = client;
}

public class PlayerReadyInGameCommandHandler(IGameManager gameManager) : ICommandHandler<PlayerReadyInGameCommand, int>
{
    public Task<int> Handle(PlayerReadyInGameCommand request, CancellationToken cancellationToken)
    {
        Game game = gameManager.GetForClient(request.Client) ?? throw new GameNotFoundForPlayerException("Player isn't in a game");

        game.Players.First(gp => gp.Id == request.Client.User!.Id).IsReady = true;

        return Task.FromResult(game.Id);
    }
}
