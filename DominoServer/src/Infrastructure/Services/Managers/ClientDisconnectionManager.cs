using Application;
using Application.Common;
using Application.Interfaces;
using Application.Messages.Queries.Lobby;

using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame;

namespace Infrastructure.Services.Managers;

public class ClientDisconnectionManager(IMessageDispatcher dispatcher, PresenceService presenceService)
{
    public async Task ProcessDisconnection(DisconnectionResult disconnectionResult)
    {
        if (disconnectionResult.LobbyId is not null)
        {
            await SendPlayerLeftToAllClientsInLobby(disconnectionResult);
        }

        if (disconnectionResult.LeaveGameResult is not null)
        {
            await SendPlayerGameLeftToAllClientsInGame(disconnectionResult);
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
