using Application;
using Application.Common;
using Application.Interfaces;
using Application.Interfaces.Managers;
using Application.Messages.Queries.Lobby;

using Domain.Game;

using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame;

namespace Infrastructure.Services.Managers;

public class ClientDisconnectionManager(
    IMessageDispatcher dispatcher,
    PresenceService presenceService,
    IGameManager? gameManager = null,
    IClientManager? clientManager = null,
    MatchAbandonmentService? abandonmentService = null)
{
    public async Task ProcessDisconnection(DisconnectionResult disconnectionResult)
    {
        if (disconnectionResult.LobbyId is not null)
        {
            await SendPlayerLeftToAllClientsInLobby(disconnectionResult);
        }

        if (disconnectionResult.LeaveGameResult is not null)
        {
            if (disconnectionResult.UserConcerned is not null &&
                disconnectionResult.LeaveGameResult.Game is not null &&
                abandonmentService is not null)
            {
                await abandonmentService.ApplyAbandonmentPenaltyAsync(
                    disconnectionResult.LeaveGameResult.Game,
                    disconnectionResult.UserConcerned.Id);
            }
            await SendPlayerGameLeftToAllClientsInGame(disconnectionResult);
        }

        if (disconnectionResult.UserConcerned is not null && gameManager is not null)
        {
            List<Game> affectedGames = gameManager.RemoveSpectatorFromAllGames(disconnectionResult.UserConcerned.Id);
            if (affectedGames.Count > 0 && clientManager is not null)
            {
                List<IClient> loggedIn = clientManager.GetAllLoggedInClients();
                foreach (Game game in affectedGames)
                {
                    HashSet<long> recipientIds = game.Players.Select(p => p.Id).Concat(game.GetSpectatorUserIds()).ToHashSet();
                    NetworkFrame countFrame = new(MessageFrameBuilder.SpectatorCountUpdatedFrame(new SpectatorCountUpdateDto
                    {
                        GameId = game.Id,
                        SpectatorCount = game.SpectatorCount
                    }));
                    foreach (IClient target in loggedIn)
                    {
                        if (target.User is not null && recipientIds.Contains(target.User.Id))
                        {
                            try { await target.SendAsync(countFrame); } catch { }
                        }
                    }
                }
            }
        }

        await presenceService.BroadcastRosterAsync();
    }

    private async Task SendPlayerGameLeftToAllClientsInGame(DisconnectionResult disconnectionResult)
    {
        LeaveGameResult result = disconnectionResult.LeaveGameResult!;
        if (result.Game is null) // The game has been deleted
        {
            NetworkFrame networkFrame = new(MessageFrameBuilder.EndGameFrame(DtoBuilder.GetEndGameDto(false)));
            foreach (IClient clientInGame in result.ClientsInGame)
            {
                await clientInGame.SendAsync(networkFrame);
            }
        }
        else
        {
            NetworkFrame networkFrame = new(MessageFrameBuilder.PlayerLeftGameFrame(DtoBuilder.GetPlayerDto(disconnectionResult.UserConcerned!)));
            foreach (IClient clientInGame in result.ClientsInGame)
            {
                await clientInGame.SendAsync(networkFrame);
            }
        }
    }

    private async Task SendPlayerLeftToAllClientsInLobby(DisconnectionResult disconnectionResult)
    {
        List<IClient> clientsInLobby = await dispatcher.Dispatch(new GetAllClientsForLobbyQuery(disconnectionResult.LobbyId!.Value));
        NetworkFrame frameToBeSended = new(MessageFrameBuilder.PlayerLeftLobbyFrame(DtoBuilder.GetPlayerDto(disconnectionResult.UserConcerned!)));
        foreach (IClient client in clientsInLobby)
        {
            await client.SendAsync(frameToBeSended);
        }
    }
}
