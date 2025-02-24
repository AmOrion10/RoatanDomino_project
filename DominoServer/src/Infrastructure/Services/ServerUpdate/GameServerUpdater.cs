using Application.Common;
using Application.Interfaces;
using Application.Messages.Commands.Game;
using Application.Messages.Queries.Chat;
using Application.Messages.Queries.Game;

using Domain;
using Domain.Game;

using DominoCommon.com.domino.common.Scripts.Networking;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Timer = Domain.Timer;

namespace Infrastructure.Services.ServerUpdate;

public class GameServerUpdater(IServiceProvider serviceProvider, ILogger<GameServerUpdater> logger)
{
    private TimeSpan timeElapsedBeforeGame;
    private TimeSpan timeElapsedGeneralGame;
    private TimeSpan timeElapsedTurnPerPlayer;

    public async Task UpdateAsync(TimeSpan deltaTime)
    {
        timeElapsedBeforeGame += deltaTime;
        timeElapsedGeneralGame += deltaTime;
        timeElapsedTurnPerPlayer += deltaTime;

        await CheckForTimerBeforeStart();
        await CheckForGeneralTimerInGames();
        await CheckForPlayerTurnTimer();
    }
    
    private async Task CheckForTimerBeforeStart()
    {
        if (timeElapsedBeforeGame.TotalSeconds < 1)
        {
            return;
        }

        IServiceScope scope = serviceProvider.CreateScope();
        IMessageDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IMessageDispatcher>();

        List<int> gamesWithTimerExpired = await dispatcher.Dispatch(new UpdateBeforeGameTimerCommand((int)timeElapsedBeforeGame.TotalSeconds));

        timeElapsedBeforeGame -= new TimeSpan(0, 0, (int)timeElapsedBeforeGame.TotalSeconds);

        foreach (int gameId in gamesWithTimerExpired)
        {
            logger.LogInformation("Game {gameId} has timer expired", gameId);
            List<IClient> clientsInGame = await dispatcher.Dispatch(new GetAllClientsForGameQuery(gameId));
            Game game = await dispatcher.Dispatch(new GameStartingCommand(gameId));
            Chat chat = await dispatcher.Dispatch(new ClientsInGameJoinChatCommand(game.Id));
            await SendHandsToPlayers(clientsInGame, game);

            await Task.Delay(1000); // We wait a bit before continuing

            game = await dispatcher.Dispatch(new GamePlaceFirstDominoCommand(gameId));

            Timer generalTimer = await dispatcher.Dispatch(new GetGeneralTimerOfGameQuery(gameId));
            Timer timerPerTurn = await dispatcher.Dispatch(new GetPlayerTurnTimerQuery(gameId));

            List<MessageFrame> framesToBeSendedToAllClients =
            [
                MessageFrameBuilder.JoinChatFrame(DtoBuilder.GetChatDto(chat)),
                MessageFrameBuilder.GameGeneralTimerFrame(DtoBuilder.GetTimerDto(generalTimer)),
                MessageFrameBuilder.PlayerPlaysFrame(DtoBuilder.GetPlayerPlayPlaceDominoDto(game)),
                MessageFrameBuilder.ChangePlayerTurnFrame(DtoBuilder.GetPlayerTurnDto(game.PlayerPlaying.Id, DtoBuilder.GetTimerDto(timerPerTurn))),
                MessageFrameBuilder.NumberOfDominoPerPlayerFrame(DtoBuilder.GetNumberOfDominoPerPlayerDto(game.GetNumberOfDominoAllPlayer()))
            ];

            foreach (IClient clientInGame in clientsInGame)
            {
                await clientInGame.SendAsync(new NetworkFrame(framesToBeSendedToAllClients));
            }

            IClient clientPlaying = clientsInGame.First(c => c.User!.Id == game.PlayerPlaying.Id);
            await clientPlaying.SendAsync(new NetworkFrame(MessageFrameBuilder.PossibleMovesFrame(DtoBuilder.GetPlayerPossibleMovesDto(game))));
        }
    }

    private async Task CheckForGeneralTimerInGames()
    {
        if (timeElapsedGeneralGame.TotalSeconds < 1)
        {
            return;
        }

        IServiceScope scope = serviceProvider.CreateScope();
        IMessageDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IMessageDispatcher>();

        List<int> gamesWithTimerExpired = await dispatcher.Dispatch(new UpdateGeneralGameTimerCommand((int)timeElapsedGeneralGame.TotalSeconds));

        timeElapsedGeneralGame -= new TimeSpan(0, 0, (int)timeElapsedGeneralGame.TotalSeconds);

        foreach (int gameId in gamesWithTimerExpired)
        {
            logger.LogInformation("Game {gameId} has general timer expired", gameId);
            List<IClient> clientsInGame = await dispatcher.Dispatch(new GetAllClientsForGameQuery(gameId));
            Game game = await dispatcher.Dispatch(new GameEndingCommand(gameId));
            Chat chat = await dispatcher.Dispatch(new GetChatQuery(game.ChatId));

            NetworkFrame frame = new([
                MessageFrameBuilder.LeaveChatFrame(DtoBuilder.GetChatDto(chat)),
                MessageFrameBuilder.EndGameFrame(DtoBuilder.GetEndGameDto(true, game))
            ]);
            foreach (IClient clientInGame in clientsInGame)
            {
                await clientInGame.SendAsync(frame);
            }

            await dispatcher.Dispatch(new SaveGameCommand(game.Id));
        }
    }

    private async Task CheckForPlayerTurnTimer()
    {
        if (timeElapsedTurnPerPlayer.TotalSeconds < 1)
        {
            return;
        }

        IServiceScope scope = serviceProvider.CreateScope();
        IMessageDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IMessageDispatcher>();

        List<int> gamesWithTimerExpired = await dispatcher.Dispatch(new UpdatePlayerTurnGameTimerCommand((int)timeElapsedTurnPerPlayer.TotalSeconds));

        timeElapsedTurnPerPlayer -= new TimeSpan(0, 0, (int)timeElapsedTurnPerPlayer.TotalSeconds);

        foreach (int gameId in gamesWithTimerExpired)
        {
            logger.LogInformation("Game {gameId} has player timer expired", gameId);
            List<IClient> clientsInGame = await dispatcher.Dispatch(new GetAllClientsForGameQuery(gameId));

            Game game = await dispatcher.Dispatch(new GetGameForPlayerQuery(clientsInGame[0]));

            long previousPlayerId = game.PlayerPlaying.Id;

            Domino? domino = await dispatcher.Dispatch(new GetDominoFromStackQuery(gameId));
            PlayerPlayDto drawDto = new();
            PlayerPlayDto drawForPlayerDto = new();
            if (domino is not null)
            {
                drawDto = new PlayerPlayDto
                {
                    PlayActionType = PlayActionType.Draw,
                    IdUserPlayed = game.PlayerPlaying.Id,
                };
                drawForPlayerDto = DtoBuilder.CopyPlayerPlayDto(drawDto);
                drawForPlayerDto.Moves = new MoveDto
                {
                    Domino = new DominoDto
                    {
                        Side1 = domino.Side1,
                        Side2 = domino.Side2
                    },
                    PlayerId = game.PlayerPlaying.Id,
                };
            }

            game = await dispatcher.Dispatch(new GameChangeTurnTimerCommand(gameId, timerExpired: true));

            Timer timerPerTurn = await dispatcher.Dispatch(new GetPlayerTurnTimerQuery(gameId));

            Dictionary<long, int> numberOfDomino = await dispatcher.Dispatch(new GameGetNumberOfDominoQuery(game.Id));

            foreach (IClient clientInGame in clientsInGame)
            {
                if (clientInGame.User!.Id == previousPlayerId)
                {
                    await clientInGame.SendAsync(new NetworkFrame([
                            MessageFrameBuilder.PlayerPlaysFrame(drawForPlayerDto),
                            MessageFrameBuilder.ChangePlayerTurnFrame(DtoBuilder.GetPlayerTurnDto(game.PlayerPlaying.Id, DtoBuilder.GetTimerDto(timerPerTurn))),
                            MessageFrameBuilder.NumberOfDominoPerPlayerFrame(DtoBuilder.GetNumberOfDominoPerPlayerDto(numberOfDomino))
                    ]));
                }
                else
                {
                    await clientInGame.SendAsync(new NetworkFrame([
                            MessageFrameBuilder.PlayerPlaysFrame(drawDto),
                            MessageFrameBuilder.ChangePlayerTurnFrame(DtoBuilder.GetPlayerTurnDto(game.PlayerPlaying.Id, DtoBuilder.GetTimerDto(timerPerTurn))),
                            MessageFrameBuilder.NumberOfDominoPerPlayerFrame(DtoBuilder.GetNumberOfDominoPerPlayerDto(numberOfDomino))
                    ]));
                }
                if (clientInGame.User!.Id == game.PlayerPlaying.Id)
                {
                    await clientInGame.SendAsync(new NetworkFrame(MessageFrameBuilder.PossibleMovesFrame(DtoBuilder.GetPlayerPossibleMovesDto(game))));
                }
            }
        }
    }

    private static async Task SendHandsToPlayers(List<IClient> clientsInGame, Game game)
    {
        foreach (GamePlayer player in game.Players)
        {
            IClient clientForPlayer = clientsInGame.First(c => c.User!.Id == player.Id);
            await clientForPlayer.SendAsync(new NetworkFrame(MessageFrameBuilder.DominoDistributionFrame(DtoBuilder.GetHandDto(player))));
        }
    }
}
