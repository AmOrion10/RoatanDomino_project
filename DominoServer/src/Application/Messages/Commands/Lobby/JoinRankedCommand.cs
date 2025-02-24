using Application.Common;
using Application.Exceptions.Player;
using Application.Interfaces;
using Application.Interfaces.Managers;

namespace Application.Messages.Commands.Lobby;

using Domain;

public class JoinRankedCommand(IClient joiningPlayer) : ICommand<Lobby>
{
    public IClient JoiningPlayer { get; set; } = joiningPlayer;
}

public class JoinRankedCommandHandler(ILobbyManager lobbyManager) : ICommandHandler<JoinRankedCommand, Lobby>
{
    public Task<Lobby> Handle(JoinRankedCommand request, CancellationToken cancellationToken)
    {
        if (lobbyManager.PlayerIsAlreadyInALobby(request.JoiningPlayer))
        {
            throw new PlayerAlreadyInALobbyException("User is already in a lobby");
        }

        Lobby? lobby = lobbyManager.GetARankedLobby();

        if (lobby is null)
        {
            lobby = lobbyManager.CreateLobby(request.JoiningPlayer, maxNumberOfPlayer: 4, lobbyPublic: false, name: "Classee", ranked: true);
        }
        else
        {
            lobbyManager.AddPlayerToLobby(request.JoiningPlayer, lobby.Id);
        }

        return Task.FromResult(lobby);
    }
}