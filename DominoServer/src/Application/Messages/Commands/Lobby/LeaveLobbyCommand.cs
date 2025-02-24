using Application.Common;
using Application.Exceptions.Chat;
using Application.Exceptions.Player;
using Application.Interfaces;

namespace Application.Messages.Commands.Lobby;

using Application.Interfaces.Managers;

using Domain;

// A refactorer pour éviter le tuple
public class LeaveLobbyCommand(IClient leavingPlayer) : ICommand<Tuple<Lobby?, Chat>>
{
    public IClient LeavingPlayer { get; set; } = leavingPlayer;
}

public class LeaveLobbyCommandHandler(ILobbyManager lobbyManager, IChatManager chatManager) : ICommandHandler<LeaveLobbyCommand, Tuple<Lobby?, Chat>>
{
    public Task<Tuple<Lobby?, Chat>> Handle(LeaveLobbyCommand request, CancellationToken cancellationToken)
    {
        Lobby lobby = lobbyManager.GetLobbyForClient(request.LeavingPlayer) ?? throw new PlayerNotInALobbyException("User is not in a lobby");
        Chat chat = chatManager.GetById(lobby.ChatId) ?? throw new ChatNotFoundException($"Chat with id {lobby.ChatId} not found");

        lobbyManager.RemovePlayerFromLobby(request.LeavingPlayer, lobby.Id);
        
        return Task.FromResult(new Tuple<Lobby?, Chat>(lobbyManager.GetLobbyById(lobby.Id), chat));
    }
}