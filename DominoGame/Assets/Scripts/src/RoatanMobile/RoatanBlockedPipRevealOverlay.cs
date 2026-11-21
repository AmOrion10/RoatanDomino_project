using System.Collections;
using System.Collections.Generic;
using System.Linq;

using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays all players' remaining tiles flipped face-up on a Blocked Board (Trancado!),
/// animates numeric pip count-up badges above each hand, highlights lowest pip total in green (winner)
/// or amber (tie/draw), and holds for 3.5 seconds before transitioning to score update / CHOOSE_POSER.
/// </summary>
public class RoatanBlockedPipRevealOverlay : MonoBehaviour
{
    public const float HoldAfterRevealSeconds = 3.5f;

    private TMP_FontAsset uiFont;
    private GameObject overlayRoot;
    private RectTransform cardsParent;
    private TMP_Text headerTitle;
    private TMP_Text headerSubtitle;
    private readonly List<GameObject> spawnedItems = new List<GameObject>();

    private class PlayerRevealUI
    {
        public RoatanMatchEngine.MatchPlayer Player;
        public int FinalPipSum;
        public Image BadgeBg;
        public Image BadgeRing;
        public TMP_Text BadgeLabel;
        public readonly List<TileFlipSlot> TileSlots = new List<TileFlipSlot>();
    }

    private class TileFlipSlot
    {
        public RectTransform Rect;
        public Image Image;
        public Sprite FaceSprite;
    }

    public void Initialize(Transform parent, TMP_FontAsset font)
    {
        uiFont = font;

        overlayRoot = new GameObject("BlockedPipRevealOverlay");
        overlayRoot.transform.SetParent(parent, false);
        RectTransform rootRect = overlayRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image dimBg = overlayRoot.AddComponent<Image>();
        dimBg.color = new Color(0.02f, 0.09f, 0.13f, 0.84f);

        // Top Title Card
        GameObject hdrObj = CreateCard(overlayRoot.transform, "RevealHeader", new Color(0.06f, 0.18f, 0.24f, 0.96f), new Vector2(0f, 770f), new Vector2(880f, 140f));
        headerTitle = CreateText(hdrObj.transform, "Title", "BOARD BLOCKED! (TRANCADO!)", 42, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, 24f), new Vector2(840f, 56f));
        headerSubtitle = CreateText(hdrObj.transform, "Subtitle", "Counting remaining individual pips...", 26, FontStyles.Normal, new Color(0.86f, 0.94f, 0.94f, 1f), new Vector2(0f, -26f), new Vector2(840f, 44f));

        GameObject cardsObj = new GameObject("RevealCardsContainer");
        cardsObj.transform.SetParent(overlayRoot.transform, false);
        cardsParent = cardsObj.AddComponent<RectTransform>();
        cardsParent.anchorMin = Vector2.zero;
        cardsParent.anchorMax = Vector2.one;
        cardsParent.offsetMin = Vector2.zero;
        cardsParent.offsetMax = Vector2.zero;

        overlayRoot.SetActive(false);
    }

    public void HideImmediate()
    {
        ClearSpawned();
        if (overlayRoot != null) overlayRoot.SetActive(false);
    }

    public IEnumerator ShowRevealCoroutine(RoatanMatchEngine engine, RoatanMatchEngine.RoundResult result)
    {
        if (engine == null || result == null || overlayRoot == null) yield break;

        ClearSpawned();
        overlayRoot.SetActive(true);
        overlayRoot.transform.SetAsLastSibling();

        headerTitle.text = "BOARD BLOCKED! (TRANCADO!)";
        headerSubtitle.text = "Flipping hands — lowest individual pip count wins...";

        int lowestPips = engine.Players.Min(p => p.HandPipSum());
        List<PlayerRevealUI> revealList = new List<PlayerRevealUI>();

        // Position cards cleanly by player count (You at bottom, opponents above)
        Vector2[] positions = GetSeatPanelPositions(engine.Players.Count);
        for (int i = 0; i < engine.Players.Count; i++)
        {
            RoatanMatchEngine.MatchPlayer player = engine.Players[i];
            Vector2 cardPos = i < positions.Length ? positions[i] : Vector2.zero;
            PlayerRevealUI ui = BuildPlayerRevealCard(player, cardPos);
            revealList.Add(ui);
        }

        // Phase 1: Flip all remaining tiles face-up (0.28s)
        yield return StartCoroutine(FlipAllTilesFaceUpCoroutine(revealList));

        // Phase 2: Animate numeric pip badges counting up from 0 to FinalPipSum (~0.75s)
        yield return StartCoroutine(CountUpPipBadgesCoroutine(revealList));

        // Phase 3: Highlight lowest individual pip total in Green (winner) or Amber (tie/draw)
        Color highlightColor = result.IsDraw ? RoatanVisualAssets.AmberHighlight : RoatanVisualAssets.PalmGreen;
        string tagSuffix = result.IsDraw ? "  •  TIE (DRAW)" : "  •  LOWEST!";

        foreach (PlayerRevealUI ui in revealList)
        {
            bool isLowest = ui.FinalPipSum == lowestPips;
            if (isLowest)
            {
                ui.BadgeBg.color = highlightColor;
                ui.BadgeRing.color = result.IsDraw ? new Color(1f, 0.85f, 0.35f, 1f) : new Color(0.55f, 1f, 0.72f, 1f);
                ui.BadgeLabel.text = $"{ui.Player.Name}: {ui.FinalPipSum} pips{tagSuffix}";
                ui.BadgeLabel.color = result.IsDraw ? new Color(0.08f, 0.08f, 0.10f, 1f) : Color.white;
            }
            else
            {
                ui.BadgeBg.color = new Color(0.10f, 0.18f, 0.24f, 0.92f);
                ui.BadgeRing.color = new Color(1f, 1f, 1f, 0.15f);
                ui.BadgeLabel.text = $"{ui.Player.Name}: {ui.FinalPipSum} pips";
            }
        }

        if (result.IsDraw)
        {
            headerSubtitle.text = $"Tie at {lowestPips} pips! Hand is a Draw (0 pts — same Poser next hand)";
            RoatanSoundFX.Instance?.PlayPass();
        }
        else if (result.Winner != null)
        {
            headerSubtitle.text = $"{result.Winner.Name} has the lowest count ({lowestPips} pips)!";
            RoatanSoundFX.Instance?.PlayWin();
        }

        // Hold for 3.5 seconds before transitioning to score update / CHOOSE_POSER
        yield return new WaitForSeconds(HoldAfterRevealSeconds);

        HideImmediate();
    }

    private Vector2[] GetSeatPanelPositions(int playerCount)
    {
        if (playerCount == 2)
        {
            return new[]
            {
                new Vector2(0f, -400f), // You (Bottom)
                new Vector2(0f, 220f)   // Opponent (Top)
            };
        }

        if (playerCount == 3)
        {
            return new[]
            {
                new Vector2(0f, -490f), // You
                new Vector2(0f, -40f),  // Seat 1
                new Vector2(0f, 380f)   // Seat 2
            };
        }

        return new[]
        {
            new Vector2(0f, -560f), // You (Bottom)
            new Vector2(0f, -220f), // Seat 1 (Left)
            new Vector2(0f, 120f),  // Seat 2 (Top / Padna)
            new Vector2(0f, 460f)   // Seat 3 (Right)
        };
    }

    private PlayerRevealUI BuildPlayerRevealCard(RoatanMatchEngine.MatchPlayer player, Vector2 pos)
    {
        PlayerRevealUI ui = new PlayerRevealUI
        {
            Player = player,
            FinalPipSum = player.HandPipSum()
        };

        GameObject panel = CreateCard(cardsParent, $"Reveal_{player.Name}", new Color(0.05f, 0.17f, 0.23f, 0.92f), pos, new Vector2(920f, 290f));
        spawnedItems.Add(panel);

        // Animated Pip Badge ABOVE the player's revealed hand
        GameObject badgeObj = CreateCard(panel.transform, "PipBadge", new Color(0.11f, 0.24f, 0.31f, 0.96f), new Vector2(0f, 102f), new Vector2(620f, 64f));
        ui.BadgeBg = badgeObj.GetComponent<Image>();

        GameObject ringObj = new GameObject("Ring");
        ringObj.transform.SetParent(badgeObj.transform, false);
        RectTransform rr = ringObj.AddComponent<RectTransform>();
        rr.anchorMin = Vector2.zero;
        rr.anchorMax = Vector2.one;
        rr.offsetMin = Vector2.zero;
        rr.offsetMax = Vector2.zero;
        ui.BadgeRing = ringObj.AddComponent<Image>();
        ui.BadgeRing.sprite = RoatanVisualAssets.RoundedOutlineSprite;
        ui.BadgeRing.type = Image.Type.Sliced;
        ui.BadgeRing.color = new Color(1f, 1f, 1f, 0.25f);
        ui.BadgeRing.raycastTarget = false;

        ui.BadgeLabel = CreateText(badgeObj.transform, "BadgeText", $"{player.Name}: 0 pips", 30, FontStyles.Bold, Color.white, Vector2.zero, new Vector2(590f, 54f));

        // Row of remaining tiles (start face-down, then flip open!)
        GameObject rowObj = new GameObject("TilesRow");
        rowObj.transform.SetParent(panel.transform, false);
        RectTransform rowRect = rowObj.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(880f, 175f);
        rowRect.anchoredPosition = new Vector2(0f, -28f);

        HorizontalLayoutGroup hlg = rowObj.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        int tileCount = Mathf.Max(1, player.Hand.Count);
        float tileW = tileCount <= 7 ? 74f : Mathf.Max(54f, 740f / tileCount);
        float tileH = tileW * 2f;

        foreach (DominoDto tile in player.Hand)
        {
            GameObject tObj = new GameObject($"Tile_{tile.Side1}_{tile.Side2}");
            tObj.transform.SetParent(rowObj.transform, false);
            RectTransform tr = tObj.AddComponent<RectTransform>();
            tr.sizeDelta = new Vector2(tileW, tileH);

            Image img = tObj.AddComponent<Image>();
            img.sprite = RoatanVisualAssets.DominoBackSprite;
            img.raycastTarget = false;

            ui.TileSlots.Add(new TileFlipSlot
            {
                Rect = tr,
                Image = img,
                FaceSprite = RoatanVisualAssets.GetDominoSprite(tile.Side1, tile.Side2)
            });
        }

        return ui;
    }

    private IEnumerator FlipAllTilesFaceUpCoroutine(List<PlayerRevealUI> revealList)
    {
        float halfDur = 0.14f;
        float t = 0f;
        while (t < halfDur)
        {
            t += Time.deltaTime;
            float sx = Mathf.Lerp(1f, 0.04f, Mathf.Clamp01(t / halfDur));
            foreach (PlayerRevealUI ui in revealList)
            {
                foreach (TileFlipSlot slot in ui.TileSlots)
                {
                    if (slot.Rect != null) slot.Rect.localScale = new Vector3(sx, 1f, 1f);
                }
            }
            yield return null;
        }

        RoatanSoundFX.Instance?.PlayPlace();
        foreach (PlayerRevealUI ui in revealList)
        {
            foreach (TileFlipSlot slot in ui.TileSlots)
            {
                if (slot.Image != null) slot.Image.sprite = slot.FaceSprite;
            }
        }

        t = 0f;
        while (t < halfDur)
        {
            t += Time.deltaTime;
            float sx = Mathf.Lerp(0.04f, 1f, Mathf.Clamp01(t / halfDur));
            foreach (PlayerRevealUI ui in revealList)
            {
                foreach (TileFlipSlot slot in ui.TileSlots)
                {
                    if (slot.Rect != null) slot.Rect.localScale = new Vector3(sx, 1f, 1f);
                }
            }
            yield return null;
        }

        foreach (PlayerRevealUI ui in revealList)
        {
            foreach (TileFlipSlot slot in ui.TileSlots)
            {
                if (slot.Rect != null) slot.Rect.localScale = Vector3.one;
            }
        }
    }

    private IEnumerator CountUpPipBadgesCoroutine(List<PlayerRevealUI> revealList)
    {
        const float dur = 0.72f;
        float t = 0f;
        float nextTick = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            float ease = 1f - (1f - p) * (1f - p);

            if (t >= nextTick)
            {
                RoatanSoundFX.Instance?.PlayPipTick();
                nextTick = t + 0.09f;
            }

            foreach (PlayerRevealUI ui in revealList)
            {
                int currentVal = Mathf.RoundToInt(Mathf.Lerp(0f, ui.FinalPipSum, ease));
                ui.BadgeLabel.text = $"{ui.Player.Name}: {currentVal} pips";
            }

            yield return null;
        }

        foreach (PlayerRevealUI ui in revealList)
        {
            ui.BadgeLabel.text = $"{ui.Player.Name}: {ui.FinalPipSum} pips";
        }
    }

    private void ClearSpawned()
    {
        foreach (GameObject go in spawnedItems)
        {
            if (go != null) Destroy(go);
        }
        spawnedItems.Clear();
    }

    private static GameObject CreateCard(Transform parent, string name, Color bgColor, Vector2 pos, Vector2 size)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        Image img = obj.AddComponent<Image>();
        img.sprite = RoatanVisualAssets.RoundedCardSprite;
        img.type = Image.Type.Sliced;
        img.color = bgColor;
        return obj;
    }

    private TMP_Text CreateText(Transform parent, string name, string text, int fontSize, FontStyles style, Color color, Vector2 pos, Vector2 size)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        TMP_Text tmp = obj.AddComponent<TextMeshProUGUI>();
        if (uiFont != null) tmp.font = uiFont;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;
        return tmp;
    }
}
