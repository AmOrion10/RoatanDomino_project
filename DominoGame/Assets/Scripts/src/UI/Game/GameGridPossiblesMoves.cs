using System;
using System.Collections.Generic;

using DominoCommon.com.domino.common.Scripts.Networking;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;

using UnityEngine;

public class GameGridPossiblesMoves : MonoBehaviour
{
    public event Action<MoveDto> PossibleMoveSelected;
    private GameGrid gameGrid;
    private readonly List<PossibleMove> possibleMoves = new();

    private void CreatePossibleMoveAndPlaceIt(MoveDto move, Vector3 position)
    {
        PossibleMove possibleMove = Instantiate(gameGrid.PossibleMovePrefab, gameGrid.GridContainer.transform);
        possibleMove.SetUp(move);
        possibleMove.MoveSelectedEvent += MoveSelectedEvent;
        RectTransform possibleMoveRectTransform = possibleMove.GetComponent<RectTransform>();
        possibleMoveRectTransform.localPosition = position;
        switch (move.Orientation)
        {
            case Orientation.PerpendicularHorizontalFromBottom:
            case Orientation.PerpendicularHorizontalFromTop:
            case Orientation.HorizontalRight:
                possibleMoveRectTransform.Rotate(new Vector3(0, 0, -90));
                break;
            case Orientation.HorizontalLeft:
                possibleMoveRectTransform.Rotate(new Vector3(0, 0, 90));
                break;
            case Orientation.PerpendicularVerticalFromRight:
            case Orientation.PerpendicularVerticalFromLeft:
            case Orientation.VerticalDown:
                possibleMoveRectTransform.Rotate(new Vector3(0, 0, 180));
                break;
        }
        possibleMoves.Add(possibleMove);
    }

    private Vector3 GetPositionOfPossibleMove(MoveDto move, NearestDomino nearestDomino)
    {
        Vector3 position = gameGrid.GetPositionOfNewDomino(move, nearestDomino);
        switch (move.Orientation)
        {
            case Orientation.HorizontalLeft:
                position.x -= gameGrid.Gap * gameGrid.FactorSpacePossibleMove;
                break;
            case Orientation.HorizontalRight:
                position.x += gameGrid.Gap * gameGrid.FactorSpacePossibleMove;
                break;
            case Orientation.VerticalDown:
                position.y -= gameGrid.Gap * gameGrid.FactorSpacePossibleMove;
                break;
            case Orientation.VerticalUp:
                position.y += gameGrid.Gap * gameGrid.FactorSpacePossibleMove;
                break;
        }
        return position;
    }

    public void ClearPossibleMoves()
    {
        foreach (PossibleMove possibleMove in possibleMoves)
        {
            possibleMove.MoveSelectedEvent -= MoveSelectedEvent;
            Destroy(possibleMove.gameObject);
        }

        possibleMoves.Clear();
    }

    public void SetGameGrid(GameGrid newGameGrid)
    {
        gameGrid = newGameGrid;
    }

    private void MoveSelectedEvent(MoveDto move)
    {
        PossibleMoveSelected?.Invoke(move);
    }

    public void ShowPossiblesMoves(List<MoveDto> moves, GameGridDto gridIndex, List<DominoBoard> dominoes)
    {
        ClearPossibleMoves();
        foreach (MoveDto move in moves)
        {
            NearestDomino nearestDomino = NearestDomino.GetNearestDomino(move, gridIndex, dominoes);
            Vector3 position = GetPositionOfPossibleMove(move, nearestDomino);
            CreatePossibleMoveAndPlaceIt(move, position);
        }
    }
}
