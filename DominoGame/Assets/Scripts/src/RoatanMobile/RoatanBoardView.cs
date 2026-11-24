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

    [Serializable]
    public struct BoardSafeMargins
    {
        public float Left;
        public float Right;
        public float Top;
        public float Bottom;

        public BoardSafeMargins(float left, float right, float top, float bottom)
        {
            Left = left;
            Right = right;
            Top = top;
            Bottom = bottom;
        }
    }

    public event Action<BoardEnd> EndSlotClicked;
    public Vector2 LastPlacedTilePos { get; private set; }
    public BoardSafeMargins SafeMargins => boardSafeMargins;

    [SerializeField] private BoardSafeMargins boardSafeMargins = new BoardSafeMargins(48f, 48f, 210f, 120f);

    private RectTransform viewportRect;
    private RectTransform boardContent;
    private RectTransform effectsLayer;
    private RectTransform tilesLayer;
    private RectTransform slotsLayer;

    private readonly List<GameObject> spawnedTiles = new List<GameObject>();
    private readonly List<GameObject> spawnedSlots = new List<GameObject>();
    private readonly List<GameObject> shuffleTiles = new List<GameObject>();
    public bool IsShuffling { get; private set; }

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

    private Vector2 shakeOffset = Vector2.zero;
    private Coroutine shakeRoutine;

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

        // Effects layer sits underneath tiles so shockwaves and dust ripples radiate beneath slammed tiles
        GameObject effectsObj = new GameObject("EffectsLayer");
        effectsObj.transform.SetParent(boardContent, false);
        effectsLayer = effectsObj.AddComponent<RectTransform>();
        effectsLayer.anchoredPosition = Vector2.zero;

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
        CancelShuffle();
        foreach (GameObject go in spawnedTiles) Destroy(go);
        spawnedTiles.Clear();
        ClearSlots();

        minX = maxX = minY = maxY = 0f;
        currentZoom = 1f;
        targetZoom = 1f;
        targetPan = Vector2.zero;
        shakeOffset = Vector2.zero;
        LastPlacedTilePos = Vector2.zero;
        userHasPannedOrZoomed = false;

        if (boardContent != null)
        {
            boardContent.anchoredPosition = Vector2.zero;
            boardContent.localScale = Vector3.one;
        }
    }

    public void CancelShuffle()
    {
        IsShuffling = false;
        foreach (GameObject go in shuffleTiles)
        {
            if (go != null) Destroy(go);
        }
        shuffleTiles.Clear();
    }

    /// <summary>
    /// Runs a 28-tile face-down "washing the dominoes" table shuffle & mix animation
    /// synced to the shuffle audio clip duration, then gathers and deals tiles to each player seat.
    /// </summary>
    public IEnumerator PlayTableShuffleAndDealCoroutine(float audioDurationSeconds, int playerCount, Action<string> onStatusUpdate = null)
    {
        ClearBoard();
        IsShuffling = true;

        // Center board cleanly inside the unobstructed safe-area rectangle during shuffle
        Vector2 safeCenter = new Vector2(
            (boardSafeMargins.Left - boardSafeMargins.Right) * 0.5f,
            (boardSafeMargins.Bottom - boardSafeMargins.Top) * 0.5f);
        targetZoom = 1f;
        currentZoom = 1f;
        targetPan = safeCenter;
        if (boardContent != null)
        {
            boardContent.localScale = Vector3.one;
            boardContent.anchoredPosition = safeCenter;
        }

        yield return PlayShuffleAndDealOnLayerCoroutine(tilesLayer, audioDurationSeconds, playerCount, shuffleTiles, onStatusUpdate);
        IsShuffling = false;
    }

    /// <summary>
    /// Shared 28-tile table wash & deal animation usable on both the offline RoatanBoardView
    /// and the online match board layer.
    /// </summary>
    public static IEnumerator PlayShuffleAndDealOnLayerCoroutine(
        RectTransform targetLayer,
        float audioDurationSeconds,
        int playerCount,
        List<GameObject> spawnedTracker,
        Action<string> onStatusUpdate = null)
    {
        if (targetLayer == null) yield break;

        if (spawnedTracker != null)
        {
            foreach (GameObject old in spawnedTracker)
            {
                if (old != null) Destroy(old);
            }
            spawnedTracker.Clear();
        }

        const int totalTiles = 28;
        float clampedTotal = Mathf.Clamp(audioDurationSeconds > 0.1f ? audioDurationSeconds : 5.0f, 4.0f, 6.5f);
        float scatterDur = 0.35f;
        float dealDur = 0.85f;
        float mixDur = Mathf.Max(2.4f, clampedTotal - scatterDur - dealDur);

        RectTransform[] rects = new RectTransform[totalTiles];
        Image[] faceImgs = new Image[totalTiles];
        Image[] shadowImgs = new Image[totalTiles];
        Vector2[] startOffsets = new Vector2[totalTiles];
        float[] radiusX = new float[totalTiles];
        float[] radiusY = new float[totalTiles];
        float[] freqX = new float[totalTiles];
        float[] freqY = new float[totalTiles];
        float[] phase = new float[totalTiles];
        float[] baseAngles = new float[totalTiles];
        float[] spinSpeed = new float[totalTiles];

        System.Random rng = new System.Random(System.Environment.TickCount ^ 9731);

        for (int i = 0; i < totalTiles; i++)
        {
            GameObject tileObj = new GameObject($"ShuffleTile_{i}");
            tileObj.transform.SetParent(targetLayer, false);
            RectTransform rt = tileObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(54f, 108f);

            GameObject shObj = new GameObject("Shadow");
            shObj.transform.SetParent(tileObj.transform, false);
            RectTransform shRt = shObj.AddComponent<RectTransform>();
            shRt.anchorMin = Vector2.zero;
            shRt.anchorMax = Vector2.one;
            shRt.offsetMin = new Vector2(4f, -6f);
            shRt.offsetMax = new Vector2(4f, -6f);
            Image shImg = shObj.AddComponent<Image>();
            shImg.sprite = RoatanVisualAssets.DominoBackSprite;
            shImg.color = new Color(0f, 0f, 0f, 0.32f);
            shImg.raycastTarget = false;

            GameObject faceObj = new GameObject("Back");
            faceObj.transform.SetParent(tileObj.transform, false);
            RectTransform fRt = faceObj.AddComponent<RectTransform>();
            fRt.anchorMin = Vector2.zero;
            fRt.anchorMax = Vector2.one;
            fRt.offsetMin = Vector2.zero;
            fRt.offsetMax = Vector2.zero;
            Image fImg = faceObj.AddComponent<Image>();
            fImg.sprite = RoatanVisualAssets.DominoBackSprite;
            fImg.color = Color.white;
            fImg.raycastTarget = false;

            float angle = (i / (float)totalTiles) * Mathf.PI * 2f + (float)(rng.NextDouble() - 0.5) * 0.45f;
            float ringDist = 45f + (float)rng.NextDouble() * 145f;
            startOffsets[i] = new Vector2(Mathf.Cos(angle) * ringDist * 1.12f, Mathf.Sin(angle) * ringDist * 0.85f);

            radiusX[i] = 85f + (float)rng.NextDouble() * 145f;
            radiusY[i] = 65f + (float)rng.NextDouble() * 120f;
            freqX[i] = 1.6f + (float)rng.NextDouble() * 1.35f;
            freqY[i] = 2.1f + (float)rng.NextDouble() * 1.45f;
            phase[i] = (float)(rng.NextDouble() * Mathf.PI * 2.0);
            baseAngles[i] = (float)(rng.NextDouble() * 360.0);
            spinSpeed[i] = ((rng.Next(2) == 0 ? 1f : -1f) * (55f + (float)rng.NextDouble() * 125f));

            rt.anchoredPosition = startOffsets[i] * 0.25f;
            rt.localRotation = Quaternion.Euler(0f, 0f, baseAngles[i]);
            rt.localScale = Vector3.one * 0.65f;

            rects[i] = rt;
            faceImgs[i] = fImg;
            shadowImgs[i] = shImg;
            spawnedTracker?.Add(tileObj);
        }

        onStatusUpdate?.Invoke("Shuffling & Mixing Tiles...");

        // Phase 1: Scatter 28 face-down tiles into a loose center cluster
        float t = 0f;
        while (t < scatterDur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / scatterDur);
            float ease = 1f - (1f - p) * (1f - p);

            for (int i = 0; i < totalTiles; i++)
            {
                if (rects[i] == null) yield break;
                rects[i].anchoredPosition = Vector2.Lerp(startOffsets[i] * 0.25f, startOffsets[i], ease);
                rects[i].localScale = Vector3.one * Mathf.Lerp(0.65f, 1.0f, ease);
            }
            yield return null;
        }

        // Phase 2: "Washing the dominoes" — circular figure-eight & jostling mixing motion
        t = 0f;
        float siblingSwapTimer = 0f;
        Vector2[] lastMixPos = new Vector2[totalTiles];
        float[] lastMixRot = new float[totalTiles];

        while (t < mixDur)
        {
            float dt = Time.deltaTime;
            t += dt;
            siblingSwapTimer += dt;

            float norm = Mathf.Clamp01(t / mixDur);
            // Smooth ramp-in and gentle ease-out at end of wash
            float rampIn = Mathf.Clamp01(t / 0.30f);
            float rampOut = Mathf.Clamp01((mixDur - t) / 0.35f);
            float swirlAmp = rampIn * (0.28f + 0.72f * rampOut);

            // Global hand-wash figure-eight sweep
            float globalSwirlX = Mathf.Sin(t * 2.6f) * 38f * swirlAmp;
            float globalSwirlY = Mathf.Sin(t * 5.2f) * 24f * swirlAmp;

            for (int i = 0; i < totalTiles; i++)
            {
                if (rects[i] == null) yield break;

                // Blend individual figure-eight orbital path + micro jostle
                float lx = Mathf.Sin(t * freqX[i] + phase[i]) * radiusX[i];
                float ly = Mathf.Sin(t * freqY[i] + phase[i] * 1.37f) * radiusY[i];
                float orbitAngle = t * (i % 2 == 0 ? 1.85f : -1.65f) + phase[i];
                float ox = Mathf.Cos(orbitAngle) * 42f;
                float oy = Mathf.Sin(orbitAngle) * 34f;

                Vector2 targetPos = new Vector2(
                    (lx * 0.72f + ox) * swirlAmp + globalSwirlX,
                    (ly * 0.72f + oy) * swirlAmp + globalSwirlY);

                // Smoothly blend from initial scatter into wash motion
                Vector2 blended = Vector2.Lerp(startOffsets[i], targetPos, rampIn);
                rects[i].anchoredPosition = blended;
                lastMixPos[i] = blended;

                float rot = baseAngles[i] + t * spinSpeed[i] * (0.45f + 0.55f * rampOut)
                          + Mathf.Sin(t * 11f + phase[i]) * 8f * swirlAmp;
                rects[i].localRotation = Quaternion.Euler(0f, 0f, rot);
                lastMixRot[i] = rot;
            }

            // Periodically shuffle sibling draw order so tiles naturally slide over/under one another
            if (siblingSwapTimer >= 0.14f)
            {
                siblingSwapTimer = 0f;
                int a = rng.Next(totalTiles);
                if (rects[a] != null) rects[a].SetAsLastSibling();
            }

            yield return null;
        }

        // Phase 3: Gather briefly, then smoothly deal tiles out to each player's seat
        onStatusUpdate?.Invoke("Dealing tiles to players...");

        Vector2[] seatTargets;
        if (playerCount <= 2)
        {
            seatTargets = new[] { new Vector2(0f, -520f), new Vector2(0f, 440f) };
        }
        else if (playerCount == 3)
        {
            seatTargets = new[] { new Vector2(0f, -520f), new Vector2(-390f, 320f), new Vector2(390f, 320f) };
        }
        else
        {
            seatTargets = new[] { new Vector2(0f, -520f), new Vector2(-390f, 320f), new Vector2(0f, 440f), new Vector2(390f, 320f) };
        }

        Vector2[] gatherPos = new Vector2[totalTiles];
        for (int i = 0; i < totalTiles; i++)
        {
            gatherPos[i] = lastMixPos[i] * 0.42f;
        }

        t = 0f;
        const float gatherDur = 0.24f;
        while (t < gatherDur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / gatherDur);
            float ease = p * p * (3f - 2f * p);
            for (int i = 0; i < totalTiles; i++)
            {
                if (rects[i] == null) yield break;
                rects[i].anchoredPosition = Vector2.Lerp(lastMixPos[i], gatherPos[i], ease);
            }
            yield return null;
        }

        float flyDur = Mathf.Max(0.45f, dealDur - gatherDur);
        t = 0f;
        while (t < flyDur)
        {
            t += Time.deltaTime;
            for (int i = 0; i < totalTiles; i++)
            {
                if (rects[i] == null) yield break;
                float delay = (i / (float)totalTiles) * (flyDur * 0.42f);
                float localP = Mathf.Clamp01((t - delay) / (flyDur * 0.58f));
                float ease = localP * localP;

                Vector2 dest = seatTargets[i % seatTargets.Length];
                rects[i].anchoredPosition = Vector2.Lerp(gatherPos[i], dest, ease);
                rects[i].localScale = Vector3.one * Mathf.Lerp(1.0f, 0.45f, ease);
                rects[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(lastMixRot[i], 0f, ease));

                float alpha = 1f - Mathf.Clamp01((localP - 0.55f) / 0.45f);
                if (faceImgs[i] != null) faceImgs[i].color = new Color(1f, 1f, 1f, alpha);
                if (shadowImgs[i] != null) shadowImgs[i].color = new Color(0f, 0f, 0f, 0.32f * alpha);
            }
            yield return null;
        }

        for (int i = 0; i < totalTiles; i++)
        {
            if (rects[i] != null) Destroy(rects[i].gameObject);
        }
        spawnedTracker?.Clear();
    }

    public void RebuildFromEngine(RoatanMatchEngine engine, bool animateLastTile, bool slammedLastTile = false)
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
        if (animateCenter) LastPlacedTilePos = Vector2.zero;
        SpawnBoardTile(centerSprite, Vector2.zero, centerRot, animateCenter, slammedLastTile && animateCenter);

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

        // Determine which arm received the very last tile
        BoardEnd lastPlayedEnd = BoardEnd.Center;
        if (engine.LeftChain.Count > 0 || engine.RightChain.Count > 0)
        {
            if (engine.PlayedDominoes.Count > 1)
            {
                var lastDom = engine.PlayedDominoes[engine.PlayedDominoes.Count - 1];
                if (engine.LeftChain.Count > 0)
                {
                    var lt = engine.LeftChain[engine.LeftChain.Count - 1].Domino;
                    if ((lt.Side1 == lastDom.Side1 && lt.Side2 == lastDom.Side2) || (lt.Side1 == lastDom.Side2 && lt.Side2 == lastDom.Side1))
                    {
                        lastPlayedEnd = BoardEnd.Left;
                    }
                }
                if (lastPlayedEnd == BoardEnd.Center && engine.RightChain.Count > 0)
                {
                    lastPlayedEnd = BoardEnd.Right;
                }
            }
        }

        // Lay out Left Arm (snakes DOWNWARD when wrapping near edges -> never collides with Right arm)
        for (int i = 0; i < engine.LeftChain.Count; i++)
        {
            RoatanMatchEngine.PlacedTile tile = engine.LeftChain[i];
            bool isLast = animateLastTile && lastPlayedEnd == BoardEnd.Left && i == engine.LeftChain.Count - 1;
            PlaceTileAlongArm(tile, isUpperArm: false, ref leftTipPos, ref leftTipDir, ref leftTipHalfExtent, isLast, slammedLastTile && isLast);
        }

        // Lay out Right Arm (snakes UPWARD when wrapping near edges -> never collides with Left arm)
        for (int i = 0; i < engine.RightChain.Count; i++)
        {
            RoatanMatchEngine.PlacedTile tile = engine.RightChain[i];
            bool isLast = animateLastTile && lastPlayedEnd == BoardEnd.Right && i == engine.RightChain.Count - 1;
            PlaceTileAlongArm(tile, isUpperArm: true, ref rightTipPos, ref rightTipDir, ref rightTipHalfExtent, isLast, slammedLastTile && isLast);
        }

        if (!userHasPannedOrZoomed)
        {
            AutoFitCamera();
        }
    }

    public void PlaySlamImpact(Vector2 boardLocalPos, bool highImpact)
    {
        if (effectsLayer != null)
        {
            StartCoroutine(SpawnShockwaveAndDustCoroutine(boardLocalPos, highImpact));
        }

        TriggerBoardShake(highImpact ? 22f : 11f, highImpact ? 0.38f : 0.24f);
    }

    public void TriggerBoardShake(float amplitude, float duration)
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
        }
        shakeRoutine = StartCoroutine(ShakeBoardCoroutine(amplitude, duration));
    }

    private IEnumerator ShakeBoardCoroutine(float amplitude, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            float damp = (1f - p) * (1f - p);
            float angle = t * 62f;
            shakeOffset = new Vector2(
                Mathf.Sin(angle) * amplitude * damp,
                Mathf.Cos(angle * 1.35f) * amplitude * 0.72f * damp
            );
            yield return null;
        }
        shakeOffset = Vector2.zero;
        shakeRoutine = null;
    }

    private IEnumerator SpawnShockwaveAndDustCoroutine(Vector2 pos, bool highImpact)
    {
        // Primary shockwave ring
        GameObject ringObj = new GameObject("SlamShockwave");
        ringObj.transform.SetParent(effectsLayer, false);
        RectTransform ringRect = ringObj.AddComponent<RectTransform>();
        ringRect.anchoredPosition = pos;
        ringRect.sizeDelta = new Vector2(highImpact ? 190f : 145f, highImpact ? 190f : 145f);
        Image ringImg = ringObj.AddComponent<Image>();
        ringImg.sprite = RoatanVisualAssets.ShockwaveRingSprite;
        ringImg.color = highImpact ? new Color(1f, 0.86f, 0.34f, 0.95f) : new Color(1f, 0.96f, 0.85f, 0.85f);
        ringImg.raycastTarget = false;

        // Spawn 6 radial dust puffs under the slammed tile
        int puffCount = highImpact ? 8 : 6;
        RectTransform[] puffRects = new RectTransform[puffCount];
        Image[] puffImgs = new Image[puffCount];
        Vector2[] puffDirs = new Vector2[puffCount];

        for (int i = 0; i < puffCount; i++)
        {
            float rad = (i / (float)puffCount) * Mathf.PI * 2f + 0.25f;
            puffDirs[i] = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            GameObject puff = new GameObject("SlamDust");
            puff.transform.SetParent(effectsLayer, false);
            RectTransform pr = puff.AddComponent<RectTransform>();
            pr.anchoredPosition = pos + puffDirs[i] * 18f;
            float sz = highImpact ? 22f : 16f;
            pr.sizeDelta = new Vector2(sz, sz);
            Image pi = puff.AddComponent<Image>();
            pi.sprite = RoatanVisualAssets.CircleSprite;
            pi.color = new Color(0.95f, 0.90f, 0.76f, 0.65f);
            pi.raycastTarget = false;

            puffRects[i] = pr;
            puffImgs[i] = pi;
        }

        float t = 0f;
        float dur = highImpact ? 0.46f : 0.36f;
        float maxScale = highImpact ? 2.45f : 1.85f;
        float puffTravel = highImpact ? 95f : 68f;

        while (t < dur && ringRect != null)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            float easeOut = 1f - (1f - p) * (1f - p);

            ringRect.localScale = Vector3.one * Mathf.Lerp(0.35f, maxScale, easeOut);
            Color rc = ringImg.color;
            rc.a = (1f - p) * (highImpact ? 0.95f : 0.82f);
            ringImg.color = rc;

            for (int i = 0; i < puffCount; i++)
            {
                if (puffRects[i] == null) continue;
                puffRects[i].anchoredPosition = pos + puffDirs[i] * Mathf.Lerp(18f, puffTravel, easeOut);
                puffRects[i].localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, p);
                Color pc = puffImgs[i].color;
                pc.a = (1f - p) * 0.65f;
                puffImgs[i].color = pc;
            }

            yield return null;
        }

        if (ringObj != null) Destroy(ringObj);
        for (int i = 0; i < puffCount; i++)
        {
            if (puffRects[i] != null) Destroy(puffRects[i].gameObject);
        }
    }

    public void ShowEndSlots(bool showLeft, bool showRight)
    {
        ClearSlots();

        if (showLeft)
        {
            Vector2 pos;
            float rot;
            ComputeNextPlacement(isDouble: false, isUpperArm: false, leftTipPos, leftTipDir, leftTipHalfExtent, GetEffectiveMaxArmX(), out pos, out rot, out _, out _);
            SpawnSlot(BoardEnd.Left, pos, rot);
        }

        if (showRight)
        {
            Vector2 pos;
            float rot;
            ComputeNextPlacement(isDouble: false, isUpperArm: true, rightTipPos, rightTipDir, rightTipHalfExtent, GetEffectiveMaxArmX(), out pos, out rot, out _, out _);
            SpawnSlot(BoardEnd.Right, pos, rot);
        }
    }

    public void ClearSlots()
    {
        foreach (GameObject go in spawnedSlots) Destroy(go);
        spawnedSlots.Clear();
    }

    public void SetSafeMargins(float left, float right, float top, float bottom)
    {
        boardSafeMargins = new BoardSafeMargins(
            Mathf.Max(0f, left),
            Mathf.Max(0f, right),
            Mathf.Max(0f, top),
            Mathf.Max(0f, bottom));

        if (!userHasPannedOrZoomed)
        {
            AutoFitCamera();
        }
    }

    public void RecenterBoard()
    {
        userHasPannedOrZoomed = false;
        AutoFitCamera();
    }

    private float GetEffectiveMaxArmX()
    {
        if (viewportRect == null) return MaxArmX;
        float safeHalfW = (viewportRect.rect.width - boardSafeMargins.Left - boardSafeMargins.Right) * 0.5f;
        if (safeHalfW <= 160f) return MaxArmX;
        return Mathf.Clamp(safeHalfW - 44f, 230f, MaxArmX);
    }

    private void PlaceTileAlongArm(
        RoatanMatchEngine.PlacedTile tile,
        bool isUpperArm,
        ref Vector2 tipPos,
        ref Vector2 tipDir,
        ref float tipHalfExtent,
        bool animate,
        bool slammed)
    {
        Vector2 nextPos;
        float rot;
        Vector2 newDir;
        float newHalfExtent;

        ComputeNextPlacement(tile.IsDouble, isUpperArm, tipPos, tipDir, tipHalfExtent, GetEffectiveMaxArmX(), out nextPos, out rot, out newDir, out newHalfExtent);

        if (animate) LastPlacedTilePos = nextPos;

        // Top half of sprite is OuterPip (faces +dir), bottom half is InnerPip (faces -dir)
        Sprite sprite = RoatanVisualAssets.GetDominoSprite(tile.OuterPip, tile.InnerPip);
        SpawnBoardTile(sprite, nextPos, rot, animate, slammed);

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
        float maxArmX,
        out Vector2 nextPos,
        out float rotZ,
        out Vector2 outDir,
        out float outHalfExtent)
    {
        Vector2 bendDir = isUpperArm ? Vector2.up : Vector2.down;
        Vector2 moveDir = tipDir;

        bool movingHoriz = Mathf.Abs(tipDir.x) > 0.5f;
        bool atEdge = (tipDir.x > 0.5f && tipPos.x + tipHalfExtent + TileH > maxArmX) ||
                      (tipDir.x < -0.5f && tipPos.x - tipHalfExtent - TileH < -maxArmX);

        if (movingHoriz && atEdge)
        {
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

    private void SpawnBoardTile(Sprite sprite, Vector2 pos, float rotZ, bool animate, bool slammed)
    {
        GameObject tileObj = new GameObject("BoardTile");
        tileObj.transform.SetParent(tilesLayer, false);
        RectTransform rect = tileObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(TileW, TileH);
        rect.anchoredPosition = pos;
        rect.localRotation = Quaternion.Euler(0f, 0f, rotZ);

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
            StartCoroutine(slammed ? AnimateTileSlam(rect) : AnimateTilePop(rect));
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

    private IEnumerator AnimateTileSlam(RectTransform rect)
    {
        float t = 0f;
        const float dropDur = 0.11f;
        while (t < dropDur && rect != null)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dropDur);
            float scale = Mathf.Lerp(1.70f, 0.92f, p * p);
            rect.localScale = Vector3.one * scale;
            yield return null;
        }

        t = 0f;
        const float settleDur = 0.12f;
        while (t < settleDur && rect != null)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / settleDur);
            float scale = Mathf.Lerp(0.92f, 1f, 1f - (1f - p) * (1f - p));
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

        float safeW = Mathf.Max(260f, viewportRect.rect.width - boardSafeMargins.Left - boardSafeMargins.Right);
        float safeH = Mathf.Max(260f, viewportRect.rect.height - boardSafeMargins.Top - boardSafeMargins.Bottom);

        float contentW = Mathf.Max(240f, (maxX - minX) + 120f);
        float contentH = Mathf.Max(240f, (maxY - minY) + 120f);

        float fitZoom = Mathf.Min(safeW / contentW, safeH / contentH);
        targetZoom = Mathf.Clamp(fitZoom, 0.42f, 1.12f);

        // Shift center into the unobstructed safe-area rectangle (away from upper-flank nameplates and bottom HUD)
        Vector2 safeCenterOffset = new Vector2(
            (boardSafeMargins.Left - boardSafeMargins.Right) * 0.5f,
            (boardSafeMargins.Bottom - boardSafeMargins.Top) * 0.5f);

        Vector2 contentCenter = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        targetPan = safeCenterOffset - contentCenter * targetZoom;
    }

    private void Update()
    {
        if (boardContent == null) return;

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

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            targetZoom = Mathf.Clamp(targetZoom * (1f + scroll * 0.1f), 0.40f, 1.85f);
            userHasPannedOrZoomed = true;
        }

        currentZoom = Mathf.Lerp(currentZoom, targetZoom, Time.deltaTime * 10f);
        boardContent.localScale = Vector3.one * currentZoom;
        Vector2 smoothPan = Vector2.Lerp(boardContent.anchoredPosition - shakeOffset, targetPan, Time.deltaTime * 10f);
        boardContent.anchoredPosition = smoothPan + shakeOffset;
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
