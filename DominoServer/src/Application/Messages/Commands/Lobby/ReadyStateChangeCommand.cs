using Application.Common;
using Application.Exceptions.Player;
using Application.Interfaces;

namespace Application.Messages.Commands.Lobby;

using Application.Interfaces.Managers;

using Domain;

public class ReadyStateChangeCommand(IClient readdyStateChangePlayer) : ICommand<Lobby>
{
    public IClient ReadyStateChangePlayer { get; set; } = readdyStateChangePlayer;
}

public class ReadyStateChangeCommandHandler(ILobbyManager lobbyManager) : ICommandHandler<ReadyStateChangeCommand, Lobby>
{
    public Task<Lobby> Handle(ReadyStateChangeCommand request, CancellationToken cancellationToken)
    {
        Lobby lobby = lobbyManager.GetLobbyForClient(request.ReadyStateChangePlayer) ?? throw new PlayerNotInTheLobbyException("Player is not in this lobby");

        lobbyManager.ReadyPlayerStateChange(request.ReadyStateChangePlayer, lobby.Id);
        
        return Task.FromResult(lobby);
    }
}