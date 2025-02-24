using Application.Common;
using Application.Interfaces.Managers;

namespace Application.Messages.Commands.Game;

using Application.Exceptions;
using Application.Exceptions.Game;

using Domain.Game;

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
        Game game = gameManager.GetById(request.GameId) ?? throw new GameNotFoundException($"Game with id {request.GameId} not found"); ;
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
                if (game.GetPlayerPossibleMoves(game.PlayerPlaying).Count > 0)
                {
                    gameAction.Type = GameActionType.NextPlayerCanPlay;
                }
                else
                {
                    gameAction.Type = GameActionType.NextPlayerBlocked;
                }
            }
        }
        else if (request.ActionType == PlayActionType.Draw)
        {
            if (!game.Stack.IsEmpty())
            {
                Domino domino = game.Draw(game.PlayerPlaying.Id)!;
                gameAction.DrawDomino = domino;
            }
            game.NextTurn();
            if (game.GetPlayerPossibleMoves(game.PlayerPlaying).Count > 0)
            {
                gameAction.Type = GameActionType.NextPlayerCanPlay;
            }
            else
            {
                gameAction.Type = GameActionType.NextPlayerBlocked;
            }
        }
        if (game.CheckEndGame())
        {
            gameAction.Type = GameActionType.EndGame;
        }
        return Task.FromResult(gameAction);
    }
}
