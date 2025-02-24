using Application.Common;
using Application.Exceptions.Game;
using Application.Interfaces.Managers;

using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Messages.Commands.Game;

using Application.Interfaces.Repositories;

using Domain.Game;

public class SaveGameCommand(int gameId) : ICommand
{
    public int GameId { get; set; } = gameId;
}

public class SaveGameCommandHandler(IGameManager gameManager, IGameRepository gameRepository, ILogger<SaveGameCommandHandler> logger) : ICommandHandler<SaveGameCommand, Unit>
{
    public async Task<Unit> Handle(SaveGameCommand request, CancellationToken cancellationToken)
    {
        Game game = gameManager.GetById(request.GameId) ?? throw new GameNotFoundException($"Game with id {request.GameId} not found");
        logger.LogInformation("Saving game {id} into database", request.GameId);
        await gameRepository.Save(game);
        gameManager.DeleteGame(request.GameId);
        return Unit.Value;
    }
}