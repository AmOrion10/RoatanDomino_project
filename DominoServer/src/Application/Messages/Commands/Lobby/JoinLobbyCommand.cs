using Application.Common;
using Application.Exceptions.Lobby;
using Application.Exceptions.Player;
using Application.Interfaces;
using Application.Interfaces.Managers;

namespace Application.Messages.Commands.Lobby;

using Domain;

public class JoinLobbyCommand(IClient joiningPlayer, int lobbyIdToJoin) : ICommand<Lobby>
{
    public IClient JoiningPlayer { get; set; } = joiningPlayer;
    public int LobbyIdToJoin { get; set; } = lobbyIdToJoin;
}

public class JoinLobbyCommandHandler(ILobbyManager lobbyManager) : ICommandHandler<JoinLobbyCommand, Lobby>
{
    public Task<Lobby> Handle(JoinLobbyCommand request, CancellationToken cancellationToken)
    {
        if (lobbyManager.PlayerIsAlreadyInALobby(request.JoiningPlayer))
        {
            throw new PlayerAlreadyInALobbyException("User is already in a lobby");
        }

        Lobby lobby = lobbyManager.GetLobbyById(request.LobbyIdToJoin) ?? throw new LobbyNotExistingException($"Lobby with id {request.LobbyIdToJoin} not found");
        
        if (lobbyManager.MaxNumberOfPlayerReached(request.LobbyIdToJoin))
        {
            throw new MaxNumberOfPlayerReachedException($"This table is full ({lobby.PlayersInLobby.Count}/{lobby.MaxNumberOfPlayer} players).");
        }

        lobbyManager.AddPlayerToLobby(request.JoiningPlayer, request.LobbyIdToJoin);

        return Task.FromResult(lobby);
    }
}