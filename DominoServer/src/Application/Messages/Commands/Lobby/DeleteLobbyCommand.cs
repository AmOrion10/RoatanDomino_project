using Application.Common;
using Application.Interfaces.Managers;

using MediatR;

namespace Application.Messages.Commands.Lobby;

public class DeleteLobbyCommand(int lobbyId) : ICommand
{
    public int LobbyId { get; set; } = lobbyId;
}

public class DeleteLobbyCommandHandler(ILobbyManager lobbyManager) : ICommandHandler<DeleteLobbyCommand, Unit>
{
    public Task<Unit> Handle(DeleteLobbyCommand request, CancellationToken cancellationToken)
    {
        lobbyManager.DeleteLobby(request.LobbyId);
        return Task.FromResult(Unit.Value);
    }
}