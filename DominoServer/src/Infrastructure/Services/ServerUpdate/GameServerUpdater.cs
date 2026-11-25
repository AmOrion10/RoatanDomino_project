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

        using IServiceScope scope = serviceProvider.CreateScope();
        IMessageDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IMessageDispatcher>();

        List<int> gamesWithTimerExpired = await dispatcher.Dispatch(new UpdateBeforeGameTimerCommand((int)timeElapsedBeforeGame.TotalSeconds));

        timeElapsedBeforeGame -= new TimeSpan(0, 0, (int)timeElapsedBeforeGame.TotalSeconds);

        foreach (int gameId in gamesWithTimerExpired)
        {
            logger.LogInformation("Game {gameId} has timer expired", gameId);
            await StartAndDealGameAsync(gameId, skipOpeningDelay: false);
        }
    }

    public async Task StartAndDealGameAsync(int gameId, bool skipOpeningDelay = false)
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        IGameManager gameManager = scope.ServiceProvider.GetRequiredService<IGameManager>();
        IMessageDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IMessageDispatcher>();

        Game? targetGame = gameManager.GetById(gameId);
        if (targetGame is null) return;

        lock (targetGame)
        {
            if (targetGame.HasDealtOpeningHand) return;
            targetGame.HasDealtOpeningHand = true;
            targetGame.StartTimer.HasStarted = false;
            targetGame.StartTimer.Duration = 0;
        }

        List<IClient> clientsInGame = await dispatcher.Dispatch(new GetAllClientsForGameQuery(gameId));
        Game game = await dispatcher.Dispatch(new GameStartingCommand(gameId));
        Chat chat = await dispatcher.Dispatch(new ClientsInGameJoinChatCommand(game.Id));
        await SendHandsToPlayers(clientsInGame, game);

        await Task.Delay(skipOpeningDelay ? 150 : 1000);

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

            List<MessageFrame> expiredEndFrames =
            [
                MessageFrameBuilder.LeaveChatFrame(DtoBuilder.GetChatDto(chat)),
                MessageFrameBuilder.EndGameFrame(DtoBuilder.GetEndGameDto(true, game))
            ];
            if (game.LastMatchEloBreakdown is not null)
            {
                expiredEndFrames.Add(MessageFrameBuilder.MatchEloBreakdownFrame(game.LastMatchEloBreakdown));
            }
            NetworkFrame frame = new(expiredEndFrames);
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
                IClientManager? cm = scope.ServiceProvider.GetService<IClientManager>();
                await HandleRoatanTurnTimerExpired(dispatcher, game, clientsInGame, previousPlayerId, cm);
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
        long previousPlayerId,
        IClientManager? clientManager = null)
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

        List<IClient> spectators = [];
        if (clientManager is not null)
        {
            HashSet<long> specIds = game.GetSpectatorUserIds().ToHashSet();
            if (specIds.Count > 0)
            {
                spectators = clientManager.GetAllLoggedInClients()
                    .Where(c => c.User is not null && specIds.Contains(c.User.Id))
                    .ToList();
            }
        }

        // Send any auto-drawn dominoes first (spectators only receive concealed publicDraw!)
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
            foreach (IClient spectator in spectators)
            {
                try { await spectator.SendAsync(new NetworkFrame(MessageFrameBuilder.PlayerPlaysFrame(publicDraw))); } catch { }
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

        List<MessageFrame> publicTurnFrames = [];
        if (actionDto is not null)
        {
            publicTurnFrames.Add(MessageFrameBuilder.PlayerPlaysFrame(actionDto));
        }
        publicTurnFrames.Add(MessageFrameBuilder.ChangePlayerTurnFrame(DtoBuilder.GetPlayerTurnDto(game, DtoBuilder.GetTimerDto(timerPerTurn))));
        publicTurnFrames.Add(MessageFrameBuilder.NumberOfDominoPerPlayerFrame(DtoBuilder.GetNumberOfDominoPerPlayerDto(numberOfDomino)));
        NetworkFrame publicTurnNetworkFrame = new(publicTurnFrames);

        foreach (IClient clientInGame in clientsInGame)
        {
            await clientInGame.SendAsync(publicTurnNetworkFrame);

            if (!handEnded && clientInGame.User!.Id == game.PlayerPlaying.Id)
            {
                await clientInGame.SendAsync(new NetworkFrame(MessageFrameBuilder.PossibleMovesFrame(DtoBuilder.GetPlayerPossibleMovesDto(game))));
            }
        }

        foreach (IClient spectator in spectators)
        {
            try { await spectator.SendAsync(publicTurnNetworkFrame); } catch { }
        }

        if (handEnded)
        {
            Game endedGame = await dispatcher.Dispatch(new GameEndingCommand(game.Id));
            Chat chat = await dispatcher.Dispatch(new GetChatQuery(endedGame.ChatId));
            List<MessageFrame> endFrames =
            [
                MessageFrameBuilder.EndGameFrame(DtoBuilder.GetEndGameDto(true, endedGame)),
                MessageFrameBuilder.LeaveChatFrame(DtoBuilder.GetChatDto(chat))
            ];
            if (endedGame.LastMatchEloBreakdown is not null)
            {
                endFrames.Add(MessageFrameBuilder.MatchEloBreakdownFrame(endedGame.LastMatchEloBreakdown));
            }
            NetworkFrame endFrame = new(endFrames);
            foreach (IClient clientInGame in clientsInGame)
            {
                await clientInGame.SendAsync(endFrame);
            }
            NetworkFrame specEndFrame = new(MessageFrameBuilder.EndGameFrame(DtoBuilder.GetEndGameDto(true, endedGame)));
            foreach (IClient spectator in spectators)
            {
                try { await spectator.SendAsync(specEndFrame); } catch { }
            }
            await dispatcher.Dispatch(new SaveGameCommand(endedGame.Id));
        }
    }

    private async Task CheckForDisconnectGraceTimers()
    {
        if (timeElapsedDisconnectGrace.TotalSeconds < 1)
        {
            return;
        }

        int wholeSeconds = (int)timeElapsedDisconnectGrace.TotalSeconds;
        timeElapsedDisconnectGrace -= new TimeSpan(0, 0, wholeSeconds);

        using IServiceScope scope = serviceProvider.CreateScope();
        IGameManager gameManager = scope.ServiceProvider.GetRequiredService<IGameManager>();
        MatchAbandonmentService? abandonmentService = scope.ServiceProvider.GetService<MatchAbandonmentService>();

        foreach (Game game in gameManager.GetAllActiveGames())
        {
            (List<long> convertedIds, bool shouldCloseRoom) = game.TickDisconnectGraceTimers(wholeSeconds);
            foreach (long playerId in convertedIds)
            {
                logger.LogInformation("Game {gameId}: Player {playerId} exceeded 40s disconnect grace window and was permanently converted to a bot", game.Id, playerId);
                if (abandonmentService is not null)
                {
                    await abandonmentService.ApplyAbandonmentPenaltyAsync(game, playerId);
                }
            }

            if (shouldCloseRoom)
            {
                logger.LogInformation("Game {gameId}: All human players timed out; closing room", game.Id);
                gameManager.DeleteGame(game.Id);
            }
        }
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
