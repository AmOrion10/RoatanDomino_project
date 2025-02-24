using DominoCommon.com.domino.common.Scripts.Networking;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR;

public class GameGrid : MonoBehaviour
{
    public float Gap = 5f;
    public GameObject GridContainer;
    public DominoBoard DominoPrefab;
    public PossibleMove PossibleMovePrefab;
    public float FactorSpacePossibleMove = 4;
    public float GridMovementSpeed = 200;
    public float MaxGridScale = 2f;
    public float MinGridScale = 0.3f;
    private bool isDragging;
    private Vector3 oldMousePosition;

    [Header("Grid max placement")]
    public float MaxGridX = 160f;
    public float MaxGridY = 190f;

    public event Action<MoveDto> PossibleMoveSelectedEvent;
    private GameGridPlacer gridPlacer;
    private GameGridPossiblesMoves gridPossiblesMoves;
    public readonly GameGridDto gridIndex = new(60, 60);
    private readonly GameGridDto gridValue = new(60, 60);
    public readonly List<DominoBoard> dominoes = new();
    private float dominoWidth, dominoHeight;
    private float halfWidth, halfHeight, halfWidthAndHeight;
    private PossibleMovesDto possibleMoves;
    private HandManager hand;

    private readonly Vector3 initialGridPosition = new(0, 0, 0);
    private readonly Vector3 initialGridScale = new(1, 1, 1);

    private void Awake()
    {
        gridPlacer = GetComponent<GameGridPlacer>();
        gridPossiblesMoves = GetComponent<GameGridPossiblesMoves>();

        gridPlacer.SetGameGrid(this);
        gridPossiblesMoves.SetGameGrid(this);
        gridPossiblesMoves.PossibleMoveSelected += PossibleMoveSelected;

        dominoWidth = DominoPrefab.GetComponent<RectTransform>().rect.width;
        dominoHeight = DominoPrefab.GetComponent<RectTransform>().rect.height;
        halfHeight = dominoHeight / 2;
        halfWidth = dominoWidth / 2;
        halfWidthAndHeight = halfWidth + halfHeight;
    }

    private void OnDestroy()
    {
        gridPossiblesMoves.PossibleMoveSelected -= PossibleMoveSelected;
    }

    private void Update()
    {
        if (Input.GetKey(KeyCode.F))
        {
            GridContainer.transform.localPosition = initialGridPosition;
            GridContainer.transform.localScale = initialGridScale;
        }

        CheckForMovingGridArrows();
        CheckForScroll();
        CheckForMovingGridClick();
    }

    private void CheckForMovingGridArrows()
    {
        if (Input.GetKey(KeyCode.RightArrow))
        {
            float newX = Math.Min(MaxGridX, GridContainer.transform.localPosition.x + GridMovementSpeed * Time.deltaTime);
            GridContainer.transform.localPosition = new Vector3(newX, GridContainer.transform.localPosition.y, 0);
        }

        if (Input.GetKey(KeyCode.LeftArrow))
        {
            float newX = Math.Max(-MaxGridX, GridContainer.transform.localPosition.x - GridMovementSpeed * Time.deltaTime);
            GridContainer.transform.localPosition = new Vector3(newX, GridContainer.transform.localPosition.y, 0);
        }

        if (Input.GetKey(KeyCode.UpArrow))
        {
            float newY = Math.Min(MaxGridY, GridContainer.transform.localPosition.y + GridMovementSpeed * Time.deltaTime);
            GridContainer.transform.localPosition = new Vector3(GridContainer.transform.localPosition.x, newY, 0);
        }

        if (Input.GetKey(KeyCode.DownArrow))
        {
            float newY = Math.Max(-MaxGridY, GridContainer.transform.localPosition.y - GridMovementSpeed * Time.deltaTime);
            GridContainer.transform.localPosition = new Vector3(GridContainer.transform.localPosition.x, newY, 0);
        }
    }

    private void CheckForScroll()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (scroll != 0)
        {
            Vector3 oldPosition = GridContainer.transform.localPosition;
            Vector3 mouseCenterBased = Input.mousePosition - new Vector3(Screen.width / 2f, Screen.height / 2f, 0);
            Vector3 move = mouseCenterBased - oldPosition;
            GridContainer.transform.localPosition += move;
            float scrollFactor = Math.Min( 
                Math.Max((1f + scroll * 0.1f) * GridContainer.transform.localScale.x, MinGridScale),
                MaxGridScale
            ) / GridContainer.transform.localScale.x;
            GridContainer.transform.localScale *= scrollFactor;
            GridContainer.transform.localPosition /= scrollFactor;
            GridContainer.transform.localPosition -= move;
            GridContainer.transform.localPosition = new Vector3(
                Mathf.Clamp(GridContainer.transform.localPosition.x, -MaxGridX, MaxGridX),
                Mathf.Clamp(GridContainer.transform.localPosition.y, -MaxGridY, MaxGridY),
                0
            );
        }
    }

    private void CheckForMovingGridClick()
    {
        if (Input.GetMouseButton(1))
        {
            if (!isDragging)
            {
                isDragging = true;
                oldMousePosition = Input.mousePosition;
                return;
            }
            Vector3 changingPosition = Input.mousePosition - oldMousePosition;
            GridContainer.transform.localPosition += changingPosition;
            // Check if the grid is out of the screen
            GridContainer.transform.localPosition = new Vector3(
                Mathf.Clamp(GridContainer.transform.localPosition.x, -MaxGridX, MaxGridX),
                Mathf.Clamp(GridContainer.transform.localPosition.y, -MaxGridY, MaxGridY),
                0
            );
            oldMousePosition = Input.mousePosition;
        }
        else if (isDragging)
        {
            isDragging = false;
        }
    }

    private void PossibleMoveSelected(MoveDto move)
    {
        PossibleMoveSelectedEvent?.Invoke(move);
    }

    public void MoveDomino(MoveDto move)
    {
        gridPlacer.MoveDomino(move, gridIndex, gridValue, dominoes, hand);
    }

    public void PlaceDomino(MoveDto move)
    {
        gridPlacer.PlaceDomino(move, gridIndex, gridValue, dominoes);
    }

    public void ShowPossiblesMovesForDomino(DominoDto domino)
    {
        if (possibleMoves == null)
        {
            return;
        }

        List<MoveDto> possiblesMovesForDomino = possibleMoves.Moves.Where(d => d.Domino.Side1 == domino.Side1 && d.Domino.Side2 == domino.Side2).ToList();
        if (possiblesMovesForDomino.Count == 0)
        {
            //TODO: Afficher un message
            Debug.Log($"Pas de mouvements possibles pour ce domino {domino.Side1}:{domino.Side2}");
            return;
        }

        gridPossiblesMoves.ShowPossiblesMoves(possiblesMovesForDomino, gridIndex, dominoes);
    }

    public void CachePossiblesMoves(PossibleMovesDto dto)
    {
        possibleMoves = dto;
    }

    public void ClearPossiblesMoves()
    {
        gridPossiblesMoves.ClearPossibleMoves();
    }

    public Vector3 GetPositionOfNewDomino(MoveDto move, NearestDomino nearestDomino)
    {
        float x = nearestDomino.Domino.destinationPosition.x;
        float y = nearestDomino.Domino.destinationPosition.y;

        switch (move.Orientation)
        {
            case Orientation.HorizontalRight:
                if (nearestDomino.RelativePosition == DominoRelativePosition.Right)
                {
                    x += nearestDomino.Domino.Alignement == DominoAlignement.Vertical ? halfWidthAndHeight + Gap : dominoHeight + Gap;
                }
                else if (nearestDomino.RelativePosition == DominoRelativePosition.Top)
                {
                    x += halfWidth;
                    y += halfWidthAndHeight + Gap;
                }
                else // DominoRelativePosition.Bottom
                {
                    x += halfWidth;
                    y -= halfWidthAndHeight + Gap;
                }
                break;

            case Orientation.HorizontalLeft:
                if (nearestDomino.RelativePosition == DominoRelativePosition.Left)
                {
                    x -= nearestDomino.Domino.Alignement == DominoAlignement.Vertical ? halfWidthAndHeight + Gap : dominoHeight + Gap;
                }
                else if (nearestDomino.RelativePosition == DominoRelativePosition.Top)
                {
                    x -= halfWidth;
                    y += halfWidthAndHeight + Gap;
                }
                else // DominoRelativePosition.Bottom
                {
                    x -= halfWidth;
                    y -= halfWidthAndHeight + Gap;
                }
                break;

            case Orientation.VerticalUp:
                if (nearestDomino.RelativePosition == DominoRelativePosition.Top)
                {
                    y += nearestDomino.Domino.Alignement == DominoAlignement.Horizontal ? halfWidthAndHeight + Gap : dominoHeight + Gap;
                }
                else if (nearestDomino.RelativePosition == DominoRelativePosition.Right)
                {
                    x += halfWidthAndHeight + Gap;
                    y += halfWidth;
                }
                else // DominoRelativePosition.Left
                {
                    x -= halfWidthAndHeight + Gap;
                    y += halfWidth;
                }
                break;

            case Orientation.VerticalDown:
                if (nearestDomino.RelativePosition == DominoRelativePosition.Bottom)
                {
                    y -= nearestDomino.Domino.Alignement == DominoAlignement.Horizontal ? halfWidthAndHeight + Gap : dominoHeight + Gap;
                }
                else if (nearestDomino.RelativePosition == DominoRelativePosition.Right)
                {
                    x += halfWidthAndHeight + Gap;
                    y -= halfWidth;
                }
                else // DominoRelativePosition.Left
                {
                    x -= halfWidthAndHeight + Gap;
                    y -= halfWidth;
                }
                break;

            case Orientation.PerpendicularVerticalFromRight:
                x -= halfWidthAndHeight + Gap;
                break;

            case Orientation.PerpendicularVerticalFromLeft:
                x += halfWidthAndHeight + Gap;
                break;

            case Orientation.PerpendicularHorizontalFromTop:
                y -= halfWidthAndHeight + Gap;
                break;

            case Orientation.PerpendicularHorizontalFromBottom:
                y += halfWidthAndHeight + Gap;
                break;
        }

        return new Vector3(x, y);
    }

    public void SetHand(HandManager handManager)
    {
        hand = handManager;
    }
}
