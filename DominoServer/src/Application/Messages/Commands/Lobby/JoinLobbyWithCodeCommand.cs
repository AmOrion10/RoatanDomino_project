using Application.Common;
using Application.Exceptions.Lobby;
using Application.Interfaces;
using Application.Interfaces.Managers;

namespace Application.Messages.Commands.Lobby;

using Application.Exceptions.Player;

using Domain;

public class JoinLobbyWithCodeCommand(IClient client, string code): ICommand<Lobby>
{
    public IClient JoiningPlayer { get; set; } = client;
    public string Code { get; set; } = code;
}

public class JoinLobbyWithCodeCommandHandler(ILobbyManager lobbyManager) : ICommandHandler<JoinLobbyWithCodeCommand, Lobby>
{
    public Task<Lobby> Handle(JoinLobbyWithCodeCommand request, CancellationToken cancellationToken)
    {
        if (lobbyManager.PlayerIsAlreadyInALobby(request.JoiningPlayer))
        {
            throw new PlayerAlreadyInALobbyException("User is already in a lobby");
        }

        Lobby lobby = lobbyManager.GetLobbyByCode(request.Code) ?? throw new LobbyNotExistingException($"Lobby with code {request.Code} not found");

        if (lobbyManager.MaxNumberOfPlayerReached(lobby.Id))
        {
            throw new MaxNumberOfPlayerReachedException($"This table is full ({lobby.PlayersInLobby.Count}/{lobby.MaxNumberOfPlayer} players).");
        }

        if (lobby.Ranked)
        {
            throw new CannotJoinRankedLobbyByCodeException("You cannot join a ranked lobby with a code");
        }

        lobbyManager.AddPlayerToLobby(request.JoiningPlayer, lobby.Id);

        return Task.FromResult(lobby);
    }
}