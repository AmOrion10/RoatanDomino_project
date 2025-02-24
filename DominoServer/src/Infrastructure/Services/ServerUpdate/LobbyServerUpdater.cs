using Application.Common;
using Application.Interfaces;
using Application.Messages.Commands.Game;
using Application.Messages.Commands.Lobby;
using Application.Messages.Queries.Chat;
using Application.Messages.Queries.Lobby;

using Domain;

using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame;

using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Services.ServerUpdate;

public class LobbyServerUpdater(IServiceProvider serviceProvider, ISeedGenerator seedGenerator)
{
    private TimeSpan timeElapsedForLobby;

    public async Task UpdateAsync(TimeSpan deltaTime)
    {
        timeElapsedForLobby += deltaTime;

        await CheckForTimerInLobbies();
    }

    private async Task CheckForTimerInLobbies()
    {
        if (timeElapsedForLobby.TotalSeconds < 1)
        {
            return;
        }

        IServiceScope scope = serviceProvider.CreateScope();
        IMessageDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IMessageDispatcher>();

        List<int> lobbiesWithTimerExpired = await dispatcher.Dispatch(new UpdateLobbyTimerCommand((int)timeElapsedForLobby.TotalSeconds));

        timeElapsedForLobby -= new TimeSpan(0, 0, (int)timeElapsedForLobby.TotalSeconds);

        foreach (int lobbyId in lobbiesWithTimerExpired)
        {
            List<IClient> clients = await dispatcher.Dispatch(new GetAllClientsForLobbyQuery(lobbyId));
            Lobby lobby = await dispatcher.Dispatch(new GetLobbyQuery(lobbyId));
            Chat chat = await dispatcher.Dispatch(new GetChatQuery(lobby.ChatId));

            NetworkFrame frame = new([
                MessageFrameBuilder.LeaveChatFrame(DtoBuilder.GetChatDto(chat)),
                MessageFrameBuilder.LobbyTimerExpiredFrame()
            ]);
            foreach (IClient clientInLobby in clients)
            {
                await clientInLobby.SendAsync(frame);
            }

            await dispatcher.Dispatch(new CreateGameCommand(lobbyId, seedGenerator.GetNewSeed()));
            await dispatcher.Dispatch(new DeleteLobbyCommand(lobbyId));
        }
    }
}
