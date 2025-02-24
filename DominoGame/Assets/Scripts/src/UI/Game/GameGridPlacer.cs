using System.Collections;
using System.Collections.Generic;
using System.Linq;

using DominoCommon.com.domino.common.Scripts.Networking;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;

using UnityEngine;

public class GameGridPlacer : MonoBehaviour
{
    private GameGrid gameGrid;
    private bool firstDominoPlaced;

    public void PlaceDomino(MoveDto move, GameGridDto gridIndex, GameGridDto gridValue, List<DominoBoard> dominoes)
    {
        if (!firstDominoPlaced)
        {
            PlaceFirstDomino(move, gridIndex, gridValue, dominoes);
            firstDominoPlaced = true;
            return;
        }

        NearestDomino nearestDomino = NearestDomino.GetNearestDomino(move, gridIndex, dominoes);
        Vector3 position = gameGrid.GetPositionOfNewDomino(move, nearestDomino);
        DominoBoard newDomino = CreateDominoAndPlaceIt(move, position, nearestDomino, gridValue);
        UpdateGrids(move, dominoes.Count, gridIndex, gridValue);
        dominoes.Add(newDomino);
    }

    public void MoveDomino(MoveDto move, GameGridDto gridIndex, GameGridDto gridValue, List<DominoBoard> dominoes, HandManager hand)
    {
        DominoDto dominoDto = move.Domino;
        foreach (DominoHand domino in hand.Dominoes.ToList())
        {
            if (domino.Domino.Side1 != dominoDto.Side1 || domino.Domino.Side2 != dominoDto.Side2)
            {
                continue;
            }

            GameObject go = domino.gameObject;
            RectTransform dominoHandTrans = go.GetComponent<RectTransform>();
            dominoHandTrans.SetParent(GameObject.Find("Grid").GetComponent<RectTransform>());
            hand.Dominoes.Remove(domino);

            Vector3 targetPosition = new(0, 0, 0); // The target position to move the Domino to;
            NearestDomino nearestDomino = null;
            if (firstDominoPlaced)
            {
                nearestDomino = NearestDomino.GetNearestDomino(move, gridIndex, dominoes);
                targetPosition += gameGrid.GetPositionOfNewDomino(move, nearestDomino);
            }
            else
            {
                firstDominoPlaced = true;
            }

            DominoBoard newDomino = CreateDominoAndPlaceIt(move, dominoHandTrans.localPosition, nearestDomino, gridValue, targetPosition);
            Destroy(go);

            UpdateGrids(move, dominoes.Count, gridIndex, gridValue);
            newDomino.Domino = move.Domino;
            Vector3 endEulerAngles = SetOrientation(move, newDomino, nearestDomino, gridValue);
            dominoes.Add(newDomino);

            float duration = 0.5f; // For how much time the Domino moves

            StartCoroutine(MoveAndRotateOverTime(newDomino.GetComponent<RectTransform>(), targetPosition, endEulerAngles, duration));
            break;
        }
    }

    private DominoBoard CreateDominoAndPlaceIt(MoveDto move, Vector3 position, NearestDomino nearestDomino, GameGridDto gridValue)
    {
        return CreateDominoAndPlaceIt(move, position, nearestDomino, gridValue, position);
    }

    private DominoBoard CreateDominoAndPlaceIt(MoveDto move, Vector3 position, NearestDomino nearestDomino, GameGridDto gridValue, Vector3 destinationPosition)
    {
        DominoBoard newDomino = Instantiate(gameGrid.DominoPrefab, gameGrid.GridContainer.transform);
        RectTransform newDominoRect = newDomino.GetComponent<RectTransform>();
        newDominoRect.localPosition = position;
        newDomino.destinationPosition = destinationPosition;

        if (position == destinationPosition)
        {
            newDominoRect.Rotate(SetOrientation(move, newDomino, nearestDomino, gridValue));
        }

        newDomino.SetUp(move.Domino);
        return newDomino;
    }

    private Vector3 SetOrientation(MoveDto move, DominoBoard newDomino, NearestDomino nearestDomino, GameGridDto gridValue)
    {
        Vector3 retour = new(0, 0, 0);
        switch (move.Orientation)
        {
            case Orientation.HorizontalRight:
            case Orientation.HorizontalLeft:
                newDomino.Alignement = DominoAlignement.Horizontal;
                retour += new Vector3(0, 0, 90);
                if (HasToBeRotatedBy180Degree(move, nearestDomino, gridValue))
                {
                    retour += new Vector3(0, 0, 180);
                }
                break;

            case Orientation.PerpendicularHorizontalFromBottom:
            case Orientation.PerpendicularHorizontalFromTop:
                newDomino.Alignement = DominoAlignement.Horizontal;
                retour += new Vector3(0, 0, 90);
                break;

            case Orientation.VerticalDown:
            case Orientation.VerticalUp:
                newDomino.Alignement = DominoAlignement.Vertical;
                if (HasToBeRotatedBy180Degree(move, nearestDomino, gridValue))
                {
                    retour += new Vector3(0, 0, 180);
                }
                break;

            case Orientation.PerpendicularVerticalFromRight:
            case Orientation.PerpendicularVerticalFromLeft:
                newDomino.Alignement = DominoAlignement.Vertical;
                break;
        }
        while (retour[2] > 180)
        {
            retour[2] -= 360;
        }
        return retour;
    }

    private static bool HasToBeRotatedBy180Degree(MoveDto move, NearestDomino nearestDomino, GameGridDto gridValue)
    {
        if (nearestDomino == null) { return false; }
        switch (move.Orientation)
        {
            case Orientation.VerticalDown:
            case Orientation.HorizontalRight:
                return move.Domino.Side1 != gridValue[nearestDomino.Row][nearestDomino.Col];
            case Orientation.VerticalUp:
            case Orientation.HorizontalLeft:
                return move.Domino.Side2 != gridValue[nearestDomino.Row][nearestDomino.Col];
            default:
                return false;
        }
    }

    private void PlaceFirstDomino(MoveDto move, GameGridDto gridIndex, GameGridDto gridValue, List<DominoBoard> dominoes)
    {
        DominoBoard domino = Instantiate(gameGrid.DominoPrefab, gameGrid.GridContainer.transform);
        domino.SetUp(move.Domino);
        RectTransform dominoRectTransform = domino.GetComponent<RectTransform>();
        dominoRectTransform.localPosition = new Vector3(0, 0);
        domino.destinationPosition = new Vector3(0, 0);

        switch (move.Orientation)
        {
            case Orientation.PerpendicularHorizontalFromBottom:
            case Orientation.PerpendicularHorizontalFromTop:
            case Orientation.HorizontalRight:
            case Orientation.HorizontalLeft:
                domino.Alignement = DominoAlignement.Horizontal;
                dominoRectTransform.Rotate(new Vector3(0, 0, 90));
                break;
            case Orientation.PerpendicularVerticalFromLeft:
            case Orientation.PerpendicularVerticalFromRight:
            case Orientation.VerticalUp:
            case Orientation.VerticalDown:
                domino.Alignement = DominoAlignement.Vertical;
                break;
        }

        UpdateGrids(move, dominoes.Count, gridIndex, gridValue);
        dominoes.Add(domino);
    }

    private static void UpdateGrids(MoveDto move, int index, GameGridDto gridIndex, GameGridDto gridValue)
    {
        bool normalOrientation;
        switch (move.Orientation)
        {
            case Orientation.HorizontalLeft:
                normalOrientation = gridValue[move.Row][move.Col + 1] == move.Domino.Side1 ||
                                    gridValue[move.Row - 1][move.Col] == move.Domino.Side1 ||
                                    gridValue[move.Row + 1][move.Col] == move.Domino.Side1;
                gridIndex[move.Row][move.Col] = index; gridValue[move.Row][move.Col] = normalOrientation ? move.Domino.Side1 : move.Domino.Side2;
                gridIndex[move.Row][move.Col - 1] = index; gridValue[move.Row][move.Col - 1] = normalOrientation ? move.Domino.Side2 : move.Domino.Side1;
                break;
            case Orientation.HorizontalRight:
                normalOrientation = gridValue[move.Row][move.Col - 1] == move.Domino.Side1 ||
                                    gridValue[move.Row - 1][move.Col] == move.Domino.Side1 ||
                                    gridValue[move.Row + 1][move.Col] == move.Domino.Side1;
                gridIndex[move.Row][move.Col] = index; gridValue[move.Row][move.Col] = normalOrientation ? move.Domino.Side1 : move.Domino.Side2;
                gridIndex[move.Row][move.Col + 1] = index; gridValue[move.Row][move.Col + 1] = normalOrientation ? move.Domino.Side2 : move.Domino.Side1;
                break;
            case Orientation.VerticalUp:
                normalOrientation = gridValue[move.Row + 1][move.Col] == move.Domino.Side1 ||
                                    gridValue[move.Row][move.Col - 1] == move.Domino.Side1 ||
                                    gridValue[move.Row][move.Col + 1] == move.Domino.Side1;
                gridIndex[move.Row][move.Col] = index; gridValue[move.Row][move.Col] = normalOrientation ? move.Domino.Side1 : move.Domino.Side2;
                gridIndex[move.Row - 1][move.Col] = index; gridValue[move.Row - 1][move.Col] = normalOrientation ? move.Domino.Side2 : move.Domino.Side1;
                break;
            case Orientation.VerticalDown:
                normalOrientation = gridValue[move.Row - 1][move.Col] == move.Domino.Side1 ||
                                    gridValue[move.Row][move.Col - 1] == move.Domino.Side1 ||
                                    gridValue[move.Row][move.Col + 1] == move.Domino.Side1;
                gridIndex[move.Row][move.Col] = index; gridValue[move.Row][move.Col] = normalOrientation ? move.Domino.Side1 : move.Domino.Side2;
                gridIndex[move.Row + 1][move.Col] = index; gridValue[move.Row + 1][move.Col] = normalOrientation ? move.Domino.Side2 : move.Domino.Side1;
                break;
            case Orientation.PerpendicularHorizontalFromBottom:
            case Orientation.PerpendicularHorizontalFromTop:
                gridIndex[move.Row][move.Col - 1] = index; gridValue[move.Row][move.Col - 1] = move.Domino.Side1;
                gridIndex[move.Row][move.Col] = index; gridValue[move.Row][move.Col] = move.Domino.Side1;
                gridIndex[move.Row][move.Col + 1] = index; gridValue[move.Row][move.Col + 1] = move.Domino.Side1;
                break;
            case Orientation.PerpendicularVerticalFromLeft:
            case Orientation.PerpendicularVerticalFromRight:
                gridIndex[move.Row - 1][move.Col] = index; gridValue[move.Row - 1][move.Col] = move.Domino.Side1;
                gridIndex[move.Row][move.Col] = index; gridValue[move.Row][move.Col] = move.Domino.Side1;
                gridIndex[move.Row + 1][move.Col] = index; gridValue[move.Row + 1][move.Col] = move.Domino.Side1;
                break;
        }
    }

    private static IEnumerator MoveAndRotateOverTime(Transform domino, Vector3 endPosition, Vector3 endEulerAngles, float duration)
    {
        Vector3 startPosition = domino.localPosition;
        Quaternion startRotation = domino.rotation;
        Quaternion endRotation = Quaternion.Euler(endEulerAngles);
        Vector3 startScale = domino.localScale;
        Vector3 endScale = new(1, 1, 1);
        float elapsedTime = 0;

        while (elapsedTime < duration)
        {
            domino.localPosition = Vector3.Lerp(startPosition, endPosition, elapsedTime / duration);
            domino.rotation = Quaternion.Slerp(startRotation, endRotation, elapsedTime / duration);
            domino.localScale = Vector3.Lerp(startScale, endScale, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        domino.localPosition = endPosition;
        domino.rotation = endRotation;
        domino.localScale = endScale; // failsafe
    }

    public void SetGameGrid(GameGrid newGameGrid)
    {
        gameGrid = newGameGrid;
    }
}