using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RoatanBoardView : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    private const float TileW = 62f;
    private const float TileH = 124f;
    private const float TileGap = 5f;
    private const float MaxArmX = 315f;

    public event Action<BoardEnd> EndSlotClicked;

    private RectTransform viewportRect;
    private RectTransform boardContent;
    private RectTransform tilesLayer;
    private RectTransform slotsLayer;

    private readonly List<GameObject> spawnedTiles = new List<GameObject>();
    private readonly List<GameObject> spawnedSlots = new List<GameObject>();

    private Vector2 leftTipPos;
    private Vector2 leftTipDir;
    private float leftTipHalfExtent;

    private Vector2 rightTipPos;
    private Vector2 rightTipDir;
    private float rightTipHalfExtent;

    private float minX, maxX, minY, maxY;
    private bool userHasPannedOrZoomed;
    private float currentZoom = 1f;
    private float targetZoom = 1f;
    private Vector2 targetPan = Vector2.zero;

    private float lastPinchDistance;
    private bool isPinching;

    public void Initialize(RectTransform parentViewport)
    {
        viewportRect = parentViewport;

        // Add transparent raycast surface on viewport so 1-finger pan / pinch works anywhere on the felt
        Image hitArea = viewportRect.GetComponent<Image>();
        if (hitArea == null)
        {
            hitArea = viewportRect.gameObject.AddComponent<Image>();
            hitArea.color = new Color(0f, 0f, 0f, 0f);
        }
        hitArea.raycastTarget = true;

        GameObject contentObj = new GameObject("BoardContent");
        contentObj.transform.SetParent(viewportRect, false);
        boardContent = contentObj.AddComponent<RectTransform>();
        boardContent.anchorMin = new Vector2(0.5f, 0.5f);
        boardContent.anchorMax = new Vector2(0.5f, 0.5f);
        boardContent.pivot = new Vector2(0.5f, 0.5f);
        boardContent.sizeDelta = new Vector2(100f, 100f);
        boardContent.anchoredPosition = Vector2.zero;

        GameObject tilesObj = new GameObject("TilesLayer");
        tilesObj.transform.SetParent(boardContent, false);
        tilesLayer = tilesObj.AddComponent<RectTransform>();
        tilesLayer.anchoredPosition = Vector2.zero;

        GameObject slotsObj = new GameObject("SlotsLayer");
        slotsObj.transform.SetParent(boardContent, false);
        slotsLayer = slotsObj.AddComponent<RectTransform>();
        slotsLayer.anchoredPosition = Vector2.zero;
    }

    public void ClearBoard()
    {
        foreach (GameObject go in spawnedTiles) Destroy(go);
        spawnedTiles.Clear();
        ClearSlots();

        minX = maxX = minY = maxY = 0f;
        currentZoom = 1f;
        targetZoom = 1f;
        targetPan = Vector2.zero;
        userHasPannedOrZoomed = false;

        if (boardContent != null)
        {
            boardContent.anchoredPosition = Vector2.zero;
            boardContent.localScale = Vector3.one;
        }
    }

    public void RebuildFromEngine(RoatanMatchEngine engine, bool animateLastTile)
    {
        foreach (GameObject go in spawnedTiles) Destroy(go);
        spawnedTiles.Clear();
        ClearSlots();

        if (engine == null || engine.CenterTile == null) return;

        minX = maxX = minY = maxY = 0f;

        // Place Center Tile
        RoatanMatchEngine.PlacedTile center = engine.CenterTile;
        bool centerIsDouble = center.IsDouble;
        float centerRot = centerIsDouble ? 0f : -90f; // Side1 (LeftEnd) faces -X, Side2 (RightEnd) faces +X
        Sprite centerSprite = RoatanVisualAssets.GetDominoSprite(center.Domino.Side2, center.Domino.Side1);

        bool animateCenter = animateLastTile && engine.LeftChain.Count == 0 && engine.RightChain.Count == 0;
        SpawnBoardTile(centerSprite, Vector2.zero, centerRot, animateCenter);

        float centerHalfX = centerIsDouble ? TileW * 0.5f : TileH * 0.5f;
        float centerHalfY = centerIsDouble ? TileH * 0.5f : TileW * 0.5f;
        ExpandBounds(Vector2.zero, centerHalfX, centerHalfY);

        // Initialize arms
        leftTipPos = Vector2.zero;
        leftTipDir = Vector2.left;
        leftTipHalfExtent = centerHalfX;

        rightTipPos = Vector2.zero;
        rightTipDir = Vector2.right;
        rightTipHalfExtent = centerHalfX;

        // Lay out Left Arm (snakes DOWNWARD when wrapping near edges -> never collides with Right arm)
        for (int i = 0; i < engine.LeftChain.Count; i++)
        {
            RoatanMatchEngine.PlacedTile tile = engine.LeftChain[i];
            bool isLast = animateLastTile && i == engine.LeftChain.Count - 1 && tile.EndPlaced == BoardEnd.Left;
            PlaceTileAlongArm(tile, isUpperArm: false, ref leftTipPos, ref leftTipDir, ref leftTipHalfExtent, isLast);
        }

        // Lay out Right Arm (snakes UPWARD when wrapping near edges -> never collides with Left arm)
        for (int i = 0; i < engine.RightChain.Count; i++)
        {
            RoatanMatchEngine.PlacedTile tile = engine.RightChain[i];
            bool isLast = animateLastTile && i == engine.RightChain.Count - 1 && tile.EndPlaced == BoardEnd.Right;
            PlaceTileAlongArm(tile, isUpperArm: true, ref rightTipPos, ref rightTipDir, ref rightTipHalfExtent, isLast);
        }

        if (!userHasPannedOrZoomed)
        {
            AutoFitCamera();
        }
    }

    public void ShowEndSlots(bool showLeft, bool showRight)
    {
        ClearSlots();

        if (showLeft)
        {
            Vector2 pos;
            float rot;
            ComputeNextPlacement(isDouble: false, isUpperArm: false, leftTipPos, leftTipDir, leftTipHalfExtent, out pos, out rot, out _, out _);
            SpawnSlot(BoardEnd.Left, pos, rot);
        }

        if (showRight)
        {
            Vector2 pos;
            float rot;
            ComputeNextPlacement(isDouble: false, isUpperArm: true, rightTipPos, rightTipDir, rightTipHalfExtent, out pos, out rot, out _, out _);
            SpawnSlot(BoardEnd.Right, pos, rot);
        }
    }

    public void ClearSlots()
    {
        foreach (GameObject go in spawnedSlots) Destroy(go);
        spawnedSlots.Clear();
    }

    public void RecenterBoard()
    {
        userHasPannedOrZoomed = false;
        AutoFitCamera();
    }

    private void PlaceTileAlongArm(
        RoatanMatchEngine.PlacedTile tile,
        bool isUpperArm,
        ref Vector2 tipPos,
        ref Vector2 tipDir,
        ref float tipHalfExtent,
        bool animate)
    {
        Vector2 nextPos;
        float rot;
        Vector2 newDir;
        float newHalfExtent;

        ComputeNextPlacement(tile.IsDouble, isUpperArm, tipPos, tipDir, tipHalfExtent, out nextPos, out rot, out newDir, out newHalfExtent);

        // Top half of sprite is OuterPip (faces +dir), bottom half is InnerPip (faces -dir)
        Sprite sprite = RoatanVisualAssets.GetDominoSprite(tile.OuterPip, tile.InnerPip);
        SpawnBoardTile(sprite, nextPos, rot, animate);

        bool horizontalVisual = Mathf.Abs(Mathf.DeltaAngle(rot, 90f)) < 5f || Mathf.Abs(Mathf.DeltaAngle(rot, -90f)) < 5f;
        ExpandBounds(nextPos, horizontalVisual ? TileH * 0.5f : TileW * 0.5f, horizontalVisual ? TileW * 0.5f : TileH * 0.5f);

        tipPos = nextPos;
        tipDir = newDir;
        tipHalfExtent = newHalfExtent;
    }

    private static void ComputeNextPlacement(
        bool isDouble,
        bool isUpperArm,
        Vector2 tipPos,
        Vector2 tipDir,
        float tipHalfExtent,
        out Vector2 nextPos,
        out float rotZ,
        out Vector2 outDir,
        out float outHalfExtent)
    {
        Vector2 bendDir = isUpperArm ? Vector2.up : Vector2.down;
        Vector2 moveDir = tipDir;

        // If moving horizontally and approaching portrait screen edge, turn cleanly into bendDir (Up for Right arm, Down for Left arm)
        bool movingHoriz = Mathf.Abs(tipDir.x) > 0.5f;
        bool atEdge = (tipDir.x > 0.5f && tipPos.x + tipHalfExtent + TileH > MaxArmX) ||
                      (tipDir.x < -0.5f && tipPos.x - tipHalfExtent - TileH < -MaxArmX);

        if (movingHoriz && atEdge)
        {
            // Corner step: step vertically in bendDir aligned to the outer half of the previous tile
            moveDir = bendDir;
            float offsetAlongPrev = Mathf.Max(0f, tipHalfExtent - TileW * 0.5f);
            float prevVertHalf = (tipHalfExtent > TileW * 0.6f) ? TileW * 0.5f : TileH * 0.5f;
            float myVertHalf = TileH * 0.5f;

            nextPos = tipPos + tipDir * offsetAlongPrev + bendDir * (prevVertHalf + TileGap + myVertHalf);
            rotZ = DirectionToRotation(moveDir, isDouble: false);
            outDir = bendDir;
            outHalfExtent = TileH * 0.5f;
            return;
        }

        // If the previous tile was a vertical corner step, immediately turn back inward horizontally!
        if (!movingHoriz)
        {
            Vector2 inwardDir = tipPos.x >= 0f ? Vector2.left : Vector2.right;
            float myHorizHalf = TileH * 0.5f;
            float offsetY = Mathf.Max(0f, tipHalfExtent - TileW * 0.5f);

            nextPos = tipPos + tipDir * offsetY + inwardDir * (TileW * 0.5f + TileGap + myHorizHalf);
            rotZ = DirectionToRotation(inwardDir, isDouble: false);
            outDir = inwardDir;
            outHalfExtent = myHorizHalf;
            return;
        }

        // Standard straight extension along moveDir
        float myExtent = isDouble ? TileW * 0.5f : TileH * 0.5f;
        nextPos = tipPos + moveDir * (tipHalfExtent + TileGap + myExtent);
        rotZ = DirectionToRotation(moveDir, isDouble);
        outDir = moveDir;
        outHalfExtent = myExtent;
    }

    private static float DirectionToRotation(Vector2 dir, bool isDouble)
    {
        if (Mathf.Abs(dir.x) > 0.5f)
        {
            if (isDouble) return 0f;
            return dir.x > 0f ? -90f : 90f;
        }
        else
        {
            if (isDouble) return 90f;
            return dir.y > 0f ? 0f : 180f;
        }
    }

    private void SpawnBoardTile(Sprite sprite, Vector2 pos, float rotZ, bool animate)
    {
        // Shadow + tile container
        GameObject tileObj = new GameObject("BoardTile");
        tileObj.transform.SetParent(tilesLayer, false);
        RectTransform rect = tileObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(TileW, TileH);
        rect.anchoredPosition = pos;
        rect.localRotation = Quaternion.Euler(0f, 0f, rotZ);

        // Soft drop shadow behind tile
        GameObject shadowObj = new GameObject("Shadow");
        shadowObj.transform.SetParent(tileObj.transform, false);
        RectTransform shadowRect = shadowObj.AddComponent<RectTransform>();
        shadowRect.anchorMin = Vector2.zero;
        shadowRect.anchorMax = Vector2.one;
        shadowRect.offsetMin = new Vector2(4f, -6f);
        shadowRect.offsetMax = new Vector2(4f, -6f);
        Image shadowImg = shadowObj.AddComponent<Image>();
        shadowImg.sprite = sprite;
        shadowImg.color = new Color(0f, 0f, 0f, 0.34f);
        shadowImg.raycastTarget = false;

        GameObject faceObj = new GameObject("Face");
        faceObj.transform.SetParent(tileObj.transform, false);
        RectTransform faceRect = faceObj.AddComponent<RectTransform>();
        faceRect.anchorMin = Vector2.zero;
        faceRect.anchorMax = Vector2.one;
        faceRect.offsetMin = Vector2.zero;
        faceRect.offsetMax = Vector2.zero;
        Image faceImg = faceObj.AddComponent<Image>();
        faceImg.sprite = sprite;
        faceImg.raycastTarget = false;

        spawnedTiles.Add(tileObj);

        if (animate)
        {
            StartCoroutine(AnimateTilePop(rect));
        }
    }

    private void SpawnSlot(BoardEnd end, Vector2 pos, float rotZ)
    {
        GameObject slotObj = new GameObject($"Slot_{end}");
        slotObj.transform.SetParent(slotsLayer, false);
        RectTransform rect = slotObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(TileW + 14f, TileH + 14f);
        rect.anchoredPosition = pos;
        rect.localRotation = Quaternion.Euler(0f, 0f, rotZ);

        Image img = slotObj.AddComponent<Image>();
        img.sprite = RoatanVisualAssets.GlowSlotSprite;
        img.color = new Color(1f, 0.88f, 0.30f, 0.95f);

        Button btn = slotObj.AddComponent<Button>();
        btn.targetGraphic = img;
        BoardEnd capturedEnd = end;
        btn.onClick.AddListener(() => EndSlotClicked?.Invoke(capturedEnd));

        spawnedSlots.Add(slotObj);
        StartCoroutine(PulseSlot(rect));
    }

    private IEnumerator AnimateTilePop(RectTransform rect)
    {
        float t = 0f;
        const float dur = 0.22f;
        while (t < dur && rect != null)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            float scale = Mathf.Lerp(1.32f, 1f, 1f - (1f - p) * (1f - p));
            rect.localScale = Vector3.one * scale;
            yield return null;
        }
        if (rect != null) rect.localScale = Vector3.one;
    }

    private IEnumerator PulseSlot(RectTransform rect)
    {
        float phase = 0f;
        while (rect != null)
        {
            phase += Time.deltaTime * 5f;
            float s = 1f + Mathf.Sin(phase) * 0.06f;
            rect.localScale = Vector3.one * s;
            yield return null;
        }
    }

    private void ExpandBounds(Vector2 pos, float halfW, float halfH)
    {
        minX = Mathf.Min(minX, pos.x - halfW);
        maxX = Mathf.Max(maxX, pos.x + halfW);
        minY = Mathf.Min(minY, pos.y - halfH);
        maxY = Mathf.Max(maxY, pos.y + halfH);
    }

    private void AutoFitCamera()
    {
        if (viewportRect == null) return;

        float viewW = Mathf.Max(300f, viewportRect.rect.width - 110f);
        float viewH = Mathf.Max(300f, viewportRect.rect.height - 140f);

        float contentW = Mathf.Max(260f, (maxX - minX) + 180f);
        float contentH = Mathf.Max(260f, (maxY - minY) + 180f);

        float fitZoom = Mathf.Min(viewW / contentW, viewH / contentH);
        targetZoom = Mathf.Clamp(fitZoom, 0.45f, 1.15f);

        Vector2 center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        targetPan = -center * targetZoom;
    }

    private void Update()
    {
        if (boardContent == null) return;

        // Touch pinch-to-zoom on mobile
        if (Input.touchCount == 2)
        {
            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);
            float dist = Vector2.Distance(t0.position, t1.position);

            if (!isPinching)
            {
                isPinching = true;
                lastPinchDistance = dist;
            }
            else if (lastPinchDistance > 1f)
            {
                float ratio = dist / lastPinchDistance;
                targetZoom = Mathf.Clamp(targetZoom * ratio, 0.40f, 1.85f);
                lastPinchDistance = dist;
                userHasPannedOrZoomed = true;
            }
        }
        else
        {
            isPinching = false;
        }

        // Mouse wheel zoom in Unity Editor
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            targetZoom = Mathf.Clamp(targetZoom * (1f + scroll * 0.1f), 0.40f, 1.85f);
            userHasPannedOrZoomed = true;
        }

        currentZoom = Mathf.Lerp(currentZoom, targetZoom, Time.deltaTime * 10f);
        boardContent.localScale = Vector3.one * currentZoom;
        boardContent.anchoredPosition = Vector2.Lerp(boardContent.anchoredPosition, targetPan, Time.deltaTime * 10f);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (Input.touchCount >= 2) return;
        userHasPannedOrZoomed = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (Input.touchCount >= 2 || boardContent == null) return;
        float canvasScale = viewportRect.lossyScale.x > 0.001f ? viewportRect.lossyScale.x : 1f;
        targetPan += eventData.delta / canvasScale;
        targetPan.x = Mathf.Clamp(targetPan.x, -700f, 700f);
        targetPan.y = Mathf.Clamp(targetPan.y, -900f, 900f);
    }
}
