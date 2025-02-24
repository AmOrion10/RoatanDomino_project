using Application.Common;
using Application.Exceptions.Lobby;
using Application.Interfaces.Managers;

using MediatR;

using Microsoft.Extensions.Logging;

namespace Application.Messages.Commands.Game;

using Domain;

public class CreateGameCommand(int lobbyId, int seed) : ICommand
{
    public int LobbyId { get; set; } = lobbyId;
    public int Seed { get; set; } = seed;
}

public class CreateGameCommandHandler(IGameManager gameManager, ILobbyManager lobbyManager, ILogger<CreateGameCommandHandler> logger) : ICommandHandler<CreateGameCommand, Unit>
{
    public Task<Unit> Handle(CreateGameCommand request, CancellationToken cancellationToken)
    {
        Lobby lobby = lobbyManager.GetLobbyById(request.LobbyId) ?? throw new LobbyNotExistingException("Lobby not found");
        logger.LogInformation("Creating game with seed : {seed}", request.Seed);
        gameManager.CreateGameFromLobby(lobby, request.Seed);
        return Task.FromResult(Unit.Value);
    }
}