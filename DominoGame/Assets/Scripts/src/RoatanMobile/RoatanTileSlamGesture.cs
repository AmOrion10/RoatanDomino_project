using System;

using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;

using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attached to playable hand tiles to detect a downward flick/drag gesture to SLAM the tile onto the table.
/// </summary>
public class RoatanTileSlamGesture : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private const float SlamTriggerDeltaY = -52f;

    private DominoDto domino;
    private bool isPlayable;
    private Action<DominoDto> onSlamFlicked;

    private RectTransform tileRect;
    private Vector2 baseAnchoredPos;
    private Vector2 pointerStartPos;
    private bool pointerActive;
    private bool slamTriggered;

    public bool SuppressNextClick { get; private set; }

    public void Configure(DominoDto tile, bool playable, Action<DominoDto> slamCallback)
    {
        domino = tile;
        isPlayable = playable;
        onSlamFlicked = slamCallback;
        tileRect = GetComponent<RectTransform>();
        if (tileRect != null)
        {
            baseAnchoredPos = tileRect.anchoredPosition;
        }
    }

    public void ConsumeSuppressedClick()
    {
        SuppressNextClick = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!isPlayable || domino == null) return;
        pointerActive = true;
        slamTriggered = false;
        SuppressNextClick = false;
        pointerStartPos = eventData.position;
        if (tileRect != null)
        {
            baseAnchoredPos = tileRect.anchoredPosition;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!pointerActive || !isPlayable || slamTriggered) return;

        Vector2 delta = eventData.position - pointerStartPos;
        if (delta.y < -12f && Mathf.Abs(delta.y) > Mathf.Abs(delta.x) * 1.15f)
        {
            if (tileRect != null)
            {
                float pullOffset = Mathf.Clamp(delta.y * 0.35f, -30f, 0f);
                tileRect.anchoredPosition = baseAnchoredPos + new Vector2(0f, pullOffset);
            }

            if (delta.y <= SlamTriggerDeltaY)
            {
                slamTriggered = true;
                SuppressNextClick = true;
                pointerActive = false;
                if (tileRect != null)
                {
                    tileRect.anchoredPosition = baseAnchoredPos;
                }
                onSlamFlicked?.Invoke(domino);
            }
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (pointerActive && tileRect != null)
        {
            tileRect.anchoredPosition = baseAnchoredPos;
        }
        pointerActive = false;
    }
}
