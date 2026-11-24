using Application.Common;
using Application.Interfaces;
using Application.Interfaces.Managers;
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
    private TimeSpan timeElapsedDisconnectGrace;

    public async Task UpdateAsync(TimeSpan deltaTime)
    {
        timeElapsedBeforeGame += deltaTime;
        timeElapsedGeneralGame += deltaTime;
        timeElapsedTurnPerPlayer += deltaTime;
        timeElapsedDisconnectGrace += deltaTime;

        await CheckForTimerBeforeStart();
        await CheckForGeneralTimerInGames();
        await CheckForPlayerTurnTimer();
        await CheckForDisconnectGraceTimers();
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
                MessageFrameBuilder.ChangePlayerTurnFrame(DtoBuilder.GetPlayerTurnDto(game, DtoBuilder.GetTimerDto(timerPerTurn))),
                MessageFrameBuilder.NumberOfDominoPerPlayerFrame(DtoBuilder.GetNumberOfDominoPerPlayerDto(game.GetNumberOfDominoAllPlayer()))
            ];

            foreach (IClient clientInGame in clientsInGame)
            {
                await clientInGame.SendAsync(new NetworkFrame(framesToBeSendedToAllClients));
            }

            IClient? clientPlaying = clientsInGame.FirstOrDefault(c => c.User!.Id == game.PlayerPlaying.Id);
            if (clientPlaying is not null)
            {
                await clientPlaying.SendAsync(new NetworkFrame(MessageFrameBuilder.PossibleMovesFrame(DtoBuilder.GetPlayerPossibleMovesDto(game))));
            }
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
            IGameManager gameManager = scope.ServiceProvider.GetRequiredService<IGameManager>();
            Game? game = gameManager.GetById(gameId);
            if (game is null) continue;

            long previousPlayerId = game.PlayerPlaying.Id;

            if (!game.IsLegacyDrawAdvance)
            {
                await HandleRoatanTurnTimerExpired(dispatcher, game, clientsInGame, previousPlayerId);
                continue;
            }

            if (clientsInGame.Count == 0) continue;

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

    private static async Task HandleRoatanTurnTimerExpired(
        IMessageDispatcher dispatcher,
        Game game,
        List<IClient> clientsInGame,
        long previousPlayerId)
    {
        List<Domino> drawnDominoes;
        GameMove? placedMove = null;
        bool playedTile = false;
        bool passed = false;

        if (game.PlayerPlaying.IsBotControlled)
        {
            (playedTile, passed, drawnDominoes, placedMove) = game.ExecuteBotTakeoverTurn(previousPlayerId);
        }
        else if (game.IsCurrentPlayerAutoPassTurn())
        {
            passed = game.ExecuteAutoPassOnTimeout(previousPlayerId);
            drawnDominoes = [];
        }
        else
        {
            (playedTile, passed, drawnDominoes, placedMove, _) = game.ExecuteTimeoutAutoPlay(previousPlayerId);
        }

        // Send any auto-drawn dominoes first
        foreach (Domino drawn in drawnDominoes)
        {
            PlayerPlayDto publicDraw = new()
            {
                PlayActionType = PlayActionType.Draw,
                IdUserPlayed = previousPlayerId
            };
            PlayerPlayDto privateDraw = DtoBuilder.CopyPlayerPlayDto(publicDraw);
            privateDraw.Moves = new MoveDto
            {
                Domino = new DominoDto { Side1 = drawn.Side1, Side2 = drawn.Side2 },
                PlayerId = previousPlayerId
            };

            foreach (IClient clientInGame in clientsInGame)
            {
                PlayerPlayDto payload = clientInGame.User!.Id == previousPlayerId ? privateDraw : publicDraw;
                await clientInGame.SendAsync(new NetworkFrame(MessageFrameBuilder.PlayerPlaysFrame(payload)));
            }
        }

        bool handEnded = game.CheckEndGame();
        game = await dispatcher.Dispatch(new GameChangeTurnTimerCommand(game.Id, timerExpired: false));
        Timer timerPerTurn = await dispatcher.Dispatch(new GetPlayerTurnTimerQuery(game.Id));
        Dictionary<long, int> numberOfDomino = await dispatcher.Dispatch(new GameGetNumberOfDominoQuery(game.Id));

        PlayerPlayDto? actionDto = null;
        if (playedTile && placedMove is not null)
        {
            actionDto = new PlayerPlayDto
            {
                IdUserPlayed = previousPlayerId,
                PlayActionType = PlayActionType.PlaceDomino,
                Moves = new MoveDto
                {
                    Domino = new DominoDto { Side1 = placedMove.Domino.Side1, Side2 = placedMove.Domino.Side2 },
                    Row = placedMove.Row,
                    Col = placedMove.Col,
                    Orientation = placedMove.Orientation,
                    PlayerId = previousPlayerId
                }
            };
        }
        else if (passed)
        {
            actionDto = new PlayerPlayDto
            {
                IdUserPlayed = previousPlayerId,
                PlayActionType = PlayActionType.Pass
            };
        }

        foreach (IClient clientInGame in clientsInGame)
        {
            List<MessageFrame> frames = [];
            if (actionDto is not null)
            {
                frames.Add(MessageFrameBuilder.PlayerPlaysFrame(actionDto));
            }
            frames.Add(MessageFrameBuilder.ChangePlayerTurnFrame(DtoBuilder.GetPlayerTurnDto(game, DtoBuilder.GetTimerDto(timerPerTurn))));
            frames.Add(MessageFrameBuilder.NumberOfDominoPerPlayerFrame(DtoBuilder.GetNumberOfDominoPerPlayerDto(numberOfDomino)));
            await clientInGame.SendAsync(new NetworkFrame(frames));

            if (!handEnded && clientInGame.User!.Id == game.PlayerPlaying.Id)
            {
                await clientInGame.SendAsync(new NetworkFrame(MessageFrameBuilder.PossibleMovesFrame(DtoBuilder.GetPlayerPossibleMovesDto(game))));
            }
        }

        if (handEnded)
        {
            Game endedGame = await dispatcher.Dispatch(new GameEndingCommand(game.Id));
            Chat chat = await dispatcher.Dispatch(new GetChatQuery(endedGame.ChatId));
            NetworkFrame endFrame = new([
                MessageFrameBuilder.EndGameFrame(DtoBuilder.GetEndGameDto(true, endedGame)),
                MessageFrameBuilder.LeaveChatFrame(DtoBuilder.GetChatDto(chat))
            ]);
            foreach (IClient clientInGame in clientsInGame)
            {
                await clientInGame.SendAsync(endFrame);
            }
            await dispatcher.Dispatch(new SaveGameCommand(endedGame.Id));
        }
    }

    private Task CheckForDisconnectGraceTimers()
    {
        if (timeElapsedDisconnectGrace.TotalSeconds < 1)
        {
            return Task.CompletedTask;
        }

        int wholeSeconds = (int)timeElapsedDisconnectGrace.TotalSeconds;
        timeElapsedDisconnectGrace -= new TimeSpan(0, 0, wholeSeconds);

        using IServiceScope scope = serviceProvider.CreateScope();
        IGameManager gameManager = scope.ServiceProvider.GetRequiredService<IGameManager>();

        foreach (Game game in gameManager.GetAllActiveGames())
        {
            (List<long> convertedIds, bool shouldCloseRoom) = game.TickDisconnectGraceTimers(wholeSeconds);
            foreach (long playerId in convertedIds)
            {
                logger.LogInformation("Game {gameId}: Player {playerId} exceeded 40s disconnect grace window and was permanently converted to a bot", game.Id, playerId);
            }

            if (shouldCloseRoom)
            {
                logger.LogInformation("Game {gameId}: All human players timed out; closing room", game.Id);
                gameManager.DeleteGame(game.Id);
            }
        }

        return Task.CompletedTask;
    }

    private static async Task SendHandsToPlayers(List<IClient> clientsInGame, Game game)
    {
        foreach (GamePlayer player in game.Players)
        {
            IClient? clientForPlayer = clientsInGame.FirstOrDefault(c => c.User!.Id == player.Id);
            if (clientForPlayer is not null)
            {
                await clientForPlayer.SendAsync(new NetworkFrame(MessageFrameBuilder.DominoDistributionFrame(DtoBuilder.GetHandDto(game, player))));
            }
        }
    }
}
