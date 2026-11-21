using Application.Common;
using Application.Exceptions.Player;
using Application.Interfaces;
using Application.Interfaces.Managers;

using DominoCommon.com.domino.common.Scripts.Enums;

namespace Application.Messages.Commands.Lobby;

public class CreateLobbyCommand : ICommand<Domain.Lobby>
{
    public required IClient Creator { get; set; }
    public required int MaxNumberOfPlayer { get; set; }
    public required bool LobbyPublic { get; set; }
    public required string Name { get; set; }
    public TableStyle? TableStyle { get; set; }
    public MatchFormat? MatchFormat { get; set; }
}

public class CreateLobbyCommandHandler(ILobbyManager lobbyManager) : ICommandHandler<CreateLobbyCommand, Domain.Lobby>
{
    public Task<Domain.Lobby> Handle(CreateLobbyCommand request, CancellationToken cancellationToken)
    {
        if (lobbyManager.PlayerHasAlreadyALobby(request.Creator))
        {
            throw new PlayerAlreadyCreatedALobbyException("Player already created a lobby");
        }

        return Task.FromResult(lobbyManager.CreateLobby(
            request.Creator,
            request.MaxNumberOfPlayer,
            request.LobbyPublic,
            request.Name,
            tableStyle: request.TableStyle,
            matchFormat: request.MatchFormat));
    }
}
