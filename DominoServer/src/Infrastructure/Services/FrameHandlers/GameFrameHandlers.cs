using Application;
using Application.Common;
using Application.Interfaces;
using Application.Interfaces.Managers;
using Application.Messages.Commands.Game;
using Application.Messages.Queries.Chat;
using Application.Messages.Queries.Game;
using Domain;
using Domain.Game;

using DominoCommon.com.domino.common.Scripts.Networking;
using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;
using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame;

using Infrastructure.Services.ServerUpdate;

using Timer = Domain.Timer;

namespace Infrastructure.Services.FrameHandlers;

public class GameFrameHandlers(
    IMessageDispatcher dispatcher,
    IGameManager gameManager,
    ITokenManager tokenManager,
    IChatManager chatManager,
    GameServerUpdater gameServerUpdater) : BaseFrameHandlers
{
    public override void RegisterHandlers(IMessageFrameHandler handler)
    {
        handler.RegisterHandler(FrameCommandType.ReadyInGame, ReadyInGameCallback);
        handler.RegisterHandler(FrameCommandType.PlayerPlays, PlayerPlaysCallback);
        handler.RegisterHandler(FrameCommandType.LeaveGame, LeaveGameCallback);
        handler.RegisterHandler(FrameCommandType.CheckActiveMatch, CheckActiveMatchCallback, needToBeLoggedIn: false);
        handler.RegisterHandler(FrameCommandType.RejoinMatch, RejoinMatchCallback, needToBeLoggedIn: false);
        handler.RegisterHandler(FrameCommandType.AbandonMatch, AbandonMatchCallback, needToBeLoggedIn: false);
    }

    private async Task CheckActiveMatchCallback(MessageFrame frame, IClient client)
    {
        RejoinSessionRequestDto req = frame.GetObjectFromMessage<RejoinSessionRequestDto>();
        Game? game = gameManager.FindRejoinableGame(req.MatchId, req.PlayerId, req.ReconnectToken, out int remainingSeconds);
        ResponseObjectDto<ActiveMatchCheckResponseDto> response = DtoBuilder.GetActiveMatchCheckResponseDto(game, req.PlayerId, remainingSeconds);
        await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(response, FrameCommandType.CheckActiveMatch)));
    }

    private async Task RejoinMatchCallback(MessageFrame frame, IClient client)
    {
        RejoinSessionRequestDto req = frame.GetObjectFromMessage<RejoinSessionRequestDto>();
        Game? game = gameManager.FindRejoinableGame(req.MatchId, req.PlayerId, req.ReconnectToken, out _);
        if (game is null || !game.RejoinPlayer(req.PlayerId, req.ReconnectToken))
        {
            ResponseObjectDto<RejoinMatchSnapshotDto> fail = new()
            {
                OkResult = false,
                ErrorType = ErrorTypes.Unknown
            };
            await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(fail, FrameCommandType.RejoinMatch)));
            return;
        }

        GamePlayer player = game.Players.First(p => p.Id == req.PlayerId);

        // Re-bind the player's socket to their original seat and issue a valid auth token
        client.User = player;
        client.IsLoggedIn = true;
        tokenManager.RemoveTokenForClientIfPresent(client);
        Guid newAuthToken = tokenManager.GenerateTokenForClient(client);

        if (game.ChatId > 0)
        {
            try
            {
                chatManager.ClientJoinChat(client, game.ChatId);
            }
            catch
            {
                // Chat may already contain or not exist in unit/mock setups
            }
        }

        ResponseObjectDto<RejoinMatchSnapshotDto> snapshotResponse = DtoBuilder.GetRejoinMatchSnapshotDto(game, player, newAuthToken);
        await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(snapshotResponse, FrameCommandType.RejoinMatch)));
    }

    private async Task AbandonMatchCallback(MessageFrame frame, IClient client)
    {
        RejoinSessionRequestDto req = frame.GetObjectFromMessage<RejoinSessionRequestDto>();
        Game? game = gameManager.FindRejoinableGame(req.MatchId, req.PlayerId, req.ReconnectToken, out _);
        if (game is not null)
        {
            game.AbandonDisconnectedSeat(req.PlayerId, req.ReconnectToken);
            if (game.AreAllHumanSeatsTimedOut())
            {
                gameManager.DeleteGame(game.Id);
            }
        }

        await SendOkToClient(client, FrameCommandType.AbandonMatch);
    }

    private async Task ReadyInGameCallback(MessageFrame frame, IClient client)
    {
        int gameId = await dispatcher.Dispatch(new PlayerReadyInGameCommand(client));
        await SendOkToClient(client, FrameCommandType.ReadyInGame);

        Timer timer = await dispatcher.Dispatch(new GetStartTimerOfGameQuery(gameId));

        if (!timer.HasStarted) return;

        TimerDto timerDto = DtoBuilder.GetTimerDto(timer);
        List<IClient> clientsInGame = await dispatcher.Dispatch(new GetAllClientsForGameQuery(gameId));
        foreach (IClient clientInGame in clientsInGame)
        {
            await clientInGame.SendAsync(new NetworkFrame(MessageFrameBuilder.GameStartingTimerFrame(timerDto)));
        }

        // For modern Roatan tables (including humans + bots), immediately deal tiles once all humans are ready
        // so clients never hang on "Waiting for server to deal dominoes..."
        Game? game = gameManager.GetById(gameId);
        if (game is not null && !game.IsLegacyDrawAdvance && !game.HasDealtOpeningHand)
        {
            await gameServerUpdater.StartAndDealGameAsync(gameId, skipOpeningDelay: true);
        }
    }

    private async Task PlayerPlaysCallback(MessageFrame frame, IClient client)
    {
        Game game = await dispatcher.Dispatch(new GetGameForPlayerQuery(client));

        PlayerPlayDto dto = frame.GetObjectFromMessage<PlayerPlayDto>();

        GameAction gameAction = await dispatcher.Dispatch(new NextGameActionCommand
        {
            GameId = game.Id,
            ActionType = dto.PlayActionType,
            Move = dto.PlayActionType == PlayActionType.PlaceDomino ? dto.Moves : null
        });

        await SendOkToClient(client, FrameCommandType.PlayerPlays);

        List<IClient> clientsInGame = await dispatcher.Dispatch(new GetAllClientsForGameQuery(game.Id));

        if (dto.PlayActionType == PlayActionType.PlaceDomino)
        {
            await SendPlayerPlaceDomino(game.Id, dto, clientsInGame);
        }
        else if (dto.PlayActionType == PlayActionType.Draw || dto.PlayActionType == PlayActionType.Pass)
        {
            await SendPlayerDrawDomino(game.Id, gameAction, dto, clientsInGame);
        }

        if (gameAction.Type == GameActionType.EndGame)
        {
            await SendEndGame(game.Id, clientsInGame);
        }
        else if (game.PlayerPlaying.IsBotControlled)
        {
            // Bot turns execute automatically via GameServerUpdater on the short bot pacing timer
        }
        else if (gameAction.Type == GameActionType.NextPlayerBlocked)
        {
            while (gameAction.Type != GameActionType.EndGame && gameAction.Type != GameActionType.NextPlayerCanPlay)
            {
                await Task.Delay(2000);
                PlayActionType nextAction = game.CanDraw(game.PlayerPlaying.Id) || (game.IsLegacyDrawAdvance && !game.Stack.IsEmpty())
                    ? PlayActionType.Draw
                    : PlayActionType.Pass;
                PlayerPlayDto dtoNextPlayer = DtoBuilder.GetPlayerPlayDto(game.PlayerPlaying.Id, nextAction);
                gameAction = await dispatcher.Dispatch(new NextGameActionCommand
                {
                    ActionType = nextAction,
                    GameId = game.Id
                });
                await SendPlayerDrawDomino(game.Id, gameAction, dtoNextPlayer, clientsInGame);
            }
            if (gameAction.Type == GameActionType.EndGame)
            {
                await SendEndGame(game.Id, clientsInGame);
            }
            else if (gameAction.Type == GameActionType.NextPlayerCanPlay)
            {
                await SendPlayerPossibleMoves(clientsInGame);
            }
        }
        else if (gameAction.Type == GameActionType.NextPlayerCanPlay)
        {
            await SendPlayerPossibleMoves(clientsInGame);
        }
    }

    private async Task LeaveGameCallback(MessageFrame frame, IClient client)
    {
        LeaveGameResult result = await dispatcher.Dispatch(new LeaveGameCommand(client));

        NetworkFrame nframe = new([
            MessageFrameBuilder.LeaveChatFrame(DtoBuilder.GetChatDto(result.Chat)),
            MessageFrameBuilder.ResponseFrame(ResponseDto.Ok(), frame.CommandType)
        ]);
        await client.SendAsync(nframe);

        if (result.Game is null) // The game has been deleted
        {
            NetworkFrame networkFrame = new([
                MessageFrameBuilder.EndGameFrame(DtoBuilder.GetEndGameDto(false)),
                MessageFrameBuilder.LeaveChatFrame(DtoBuilder.GetChatDto(result.Chat))
            ]);
            foreach (IClient clientInGame in result.ClientsInGame)
            {
                await clientInGame.SendAsync(networkFrame);
            }
        }
        else
        {
            NetworkFrame networkFrame = new(MessageFrameBuilder.PlayerLeftGameFrame(DtoBuilder.GetPlayerDto(client.User!)));
            foreach (IClient clientInGame in result.ClientsInGame)
            {
                await clientInGame.SendAsync(networkFrame);
            }
        }
    }

    private async Task SendPlayerPlaceDomino(int gameId, PlayerPlayDto dto, List<IClient> clientsInGame)
    {
        Game game = await dispatcher.Dispatch(new GameChangeTurnTimerCommand(gameId, timerExpired: false));

        Timer timerPerTurn = await dispatcher.Dispatch(new GetPlayerTurnTimerQuery(gameId));

        Dictionary<long, int> numberOfDomino = await dispatcher.Dispatch(new GameGetNumberOfDominoQuery(game.Id));
        NumberOfDominoPerPlayerDto dtoNumberDomino = DtoBuilder.GetNumberOfDominoPerPlayerDto(numberOfDomino);

        foreach (IClient clientInGame in clientsInGame)
        {
            await clientInGame.SendAsync(new NetworkFrame([
                MessageFrameBuilder.PlayerPlaysFrame(dto),
                    MessageFrameBuilder.ChangePlayerTurnFrame(DtoBuilder.GetPlayerTurnDto(game, DtoBuilder.GetTimerDto(timerPerTurn))),
                    MessageFrameBuilder.NumberOfDominoPerPlayerFrame(dtoNumberDomino)
            ]));
        }
    }

    private async Task SendPlayerDrawDomino(int gameId, GameAction gameAction, PlayerPlayDto dto, List<IClient> clientsInGame)
    {
        PlayerPlayDto dtoPlayerPlaying = DtoBuilder.CopyPlayerPlayDto(dto);
        Game game = await dispatcher.Dispatch(new GameChangeTurnTimerCommand(gameId, timerExpired: false));

        Timer timerPerTurn = await dispatcher.Dispatch(new GetPlayerTurnTimerQuery(gameId));

        if (gameAction.DrawDomino is not null)
        {
            dtoPlayerPlaying.Moves = new MoveDto
            {
                Domino = new DominoDto
                {
                    Side1 = gameAction.DrawDomino!.Side1,
                    Side2 = gameAction.DrawDomino!.Side2
                }
            };
            Dictionary<long, int> numberOfDomino = await dispatcher.Dispatch(new GameGetNumberOfDominoQuery(game.Id));
            NumberOfDominoPerPlayerDto dtoNumberDomino = DtoBuilder.GetNumberOfDominoPerPlayerDto(numberOfDomino);

            foreach (IClient clientInGame in clientsInGame)
            {
                if (clientInGame.User!.Id == dto.IdUserPlayed)
                {
                    await clientInGame.SendAsync(new NetworkFrame([
                        MessageFrameBuilder.PlayerPlaysFrame(dtoPlayerPlaying),
                            MessageFrameBuilder.ChangePlayerTurnFrame(DtoBuilder.GetPlayerTurnDto(game, DtoBuilder.GetTimerDto(timerPerTurn))),
                            MessageFrameBuilder.NumberOfDominoPerPlayerFrame(dtoNumberDomino)
                    ]));
                }
                else
                {
                    await clientInGame.SendAsync(new NetworkFrame([
                        MessageFrameBuilder.PlayerPlaysFrame(dto),
                            MessageFrameBuilder.ChangePlayerTurnFrame(DtoBuilder.GetPlayerTurnDto(game, DtoBuilder.GetTimerDto(timerPerTurn))),
                            MessageFrameBuilder.NumberOfDominoPerPlayerFrame(dtoNumberDomino)
                    ]));
                }
            }
        }
        else
        {
            Dictionary<long, int> numberOfDomino = await dispatcher.Dispatch(new GameGetNumberOfDominoQuery(game.Id));
            NumberOfDominoPerPlayerDto dtoNumberDomino = DtoBuilder.GetNumberOfDominoPerPlayerDto(numberOfDomino);

            foreach (IClient clientInGame in clientsInGame)
            {
                await clientInGame.SendAsync(new NetworkFrame([
                    MessageFrameBuilder.ChangePlayerTurnFrame(DtoBuilder.GetPlayerTurnDto(game, DtoBuilder.GetTimerDto(timerPerTurn))),
                        MessageFrameBuilder.NumberOfDominoPerPlayerFrame(dtoNumberDomino)
                ]));
            }
        }
    }

    private async Task SendPlayerPossibleMoves(List<IClient> clientsInGame)
    {
        if (clientsInGame.Count == 0) return;
        Game game = await dispatcher.Dispatch(new GetGameForPlayerQuery(clientsInGame[0]));
        IClient? activeClient = clientsInGame.FirstOrDefault(c => c.User!.Id == game.PlayerPlaying.Id);
        if (activeClient is null) return;
        PossibleMovesDto possibleMovesdto = DtoBuilder.GetPlayerPossibleMovesDto(game);
        await activeClient.SendAsync(new NetworkFrame(MessageFrameBuilder.PossibleMovesFrame(possibleMovesdto)));
    }

    private async Task SendEndGame(int gameId, List<IClient> clientsInGame)
    {
        Game game = await dispatcher.Dispatch(new GameEndingCommand(gameId));
        Chat chat = await dispatcher.Dispatch(new GetChatQuery(game.ChatId));
        List<MessageFrame> endFrames =
        [
            MessageFrameBuilder.EndGameFrame(DtoBuilder.GetEndGameDto(true, game)),
            MessageFrameBuilder.LeaveChatFrame(DtoBuilder.GetChatDto(chat))
        ];
        if (game.LastMatchEloBreakdown is not null)
        {
            endFrames.Add(MessageFrameBuilder.MatchEloBreakdownFrame(game.LastMatchEloBreakdown));
        }

        NetworkFrame networkFrame = new(endFrames);
        foreach (IClient clientInGame in clientsInGame)
        {
            await clientInGame.SendAsync(networkFrame);
        }
        await dispatcher.Dispatch(new SaveGameCommand(gameId));
    }
}