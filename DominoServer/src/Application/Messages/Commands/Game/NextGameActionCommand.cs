using Application.Common;
using Application.Interfaces.Managers;

namespace Application.Messages.Commands.Game;

using Application.Exceptions;
using Application.Exceptions.Game;

using Domain.Game;

using DominoCommon.com.domino.common.Scripts.Enums;
using DominoCommon.com.domino.common.Scripts.Networking;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;

public class NextGameActionCommand : ICommand<GameAction>
{
    public int GameId { get; set; }
    public required PlayActionType ActionType { get; set; }
    public MoveDto? Move { get; set; }
}

public class NextGameActionCommandHandler(IGameManager gameManager) : ICommandHandler<NextGameActionCommand, GameAction>
{
    public Task<GameAction> Handle(NextGameActionCommand request, CancellationToken cancellationToken)
    {
        Game game = gameManager.GetById(request.GameId) ?? throw new GameNotFoundException($"Game with id {request.GameId} not found");
        GameAction gameAction = new(GameActionType.Default);

        if (request.ActionType == PlayActionType.PlaceDomino)
        {
            if (game.GetPlayerPossibleMoves(game.PlayerPlaying).Count > 0)
            {
                GameMove move = new(new Domino(request.Move!.Domino.Side1, request.Move.Domino.Side2), request.Move.Row, request.Move.Col, request.Move.Orientation, request.Move.PlayerId);
                bool validMovement = game.PlaceDomino(move);
                if (!validMovement) throw new WrongPositionForDominoException("Wrong position for this domino");
                gameAction.PlacedDomino = move;
                game.NextTurn();
                gameAction.Type = game.GetPlayerPossibleMoves(game.PlayerPlaying).Count > 0
                    ? GameActionType.NextPlayerCanPlay
                    : GameActionType.NextPlayerBlocked;
            }
        }
        else if (request.ActionType == PlayActionType.Draw)
        {
            if (game.IsLegacyDrawAdvance)
            {
                if (!game.Stack.IsEmpty())
                {
                    Domino? domino = game.Draw(game.PlayerPlaying.Id);
                    gameAction.DrawDomino = domino;
                }
                game.NextTurn();
                gameAction.Type = game.GetPlayerPossibleMoves(game.PlayerPlaying).Count > 0
                    ? GameActionType.NextPlayerCanPlay
                    : GameActionType.NextPlayerBlocked;
            }
            else if (game.TableStyle == TableStyle.BLOCK)
            {
                // BLOCK mode forbids drawing; immediately pass when blocked
                game.PassTurn(game.PlayerPlaying.Id);
                gameAction.Type = game.GetPlayerPossibleMoves(game.PlayerPlaying).Count > 0
                    ? GameActionType.NextPlayerCanPlay
                    : GameActionType.NextPlayerBlocked;
            }
            else
            {
                // DRAW mode: draw 1 tile at a time until playable or boneyard empty; pass only once boneyard is empty
                if (game.CanDraw(game.PlayerPlaying.Id))
                {
                    gameAction.DrawDomino = game.Draw(game.PlayerPlaying.Id);
                    if (game.GetPlayerPossibleMoves(game.PlayerPlaying).Count > 0)
                    {
                        gameAction.Type = GameActionType.NextPlayerCanPlay;
                    }
                    else if (!game.Stack.IsEmpty())
                    {
                        gameAction.Type = GameActionType.NextPlayerBlocked;
                    }
                    else
                    {
                        game.PassTurn(game.PlayerPlaying.Id);
                        gameAction.Type = game.GetPlayerPossibleMoves(game.PlayerPlaying).Count > 0
                            ? GameActionType.NextPlayerCanPlay
                            : GameActionType.NextPlayerBlocked;
                    }
                }
                else
                {
                    game.PassTurn(game.PlayerPlaying.Id);
                    gameAction.Type = game.GetPlayerPossibleMoves(game.PlayerPlaying).Count > 0
                        ? GameActionType.NextPlayerCanPlay
                        : GameActionType.NextPlayerBlocked;
                }
            }
        }
        else if (request.ActionType == PlayActionType.Pass)
        {
            game.PassTurn(game.PlayerPlaying.Id);
            gameAction.Type = game.GetPlayerPossibleMoves(game.PlayerPlaying).Count > 0
                ? GameActionType.NextPlayerCanPlay
                : GameActionType.NextPlayerBlocked;
        }

        if (game.CheckEndGame())
        {
            gameAction.Type = GameActionType.EndGame;
        }
        return Task.FromResult(gameAction);
    }
}
