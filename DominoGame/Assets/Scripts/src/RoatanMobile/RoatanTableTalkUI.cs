using System;
using System.Collections;

using DominoCommon.com.domino.common.Scripts.Enums;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages anti-cheating Caribbean Quick-Emote Wheel (Table Talk), floating avatar speech bubbles,
/// major-event celebratory banners (Game Bruk, Six-Lov, Cut-Throat), and the "Call Your Padna"
/// (CHOOSE_POSER) 8-second countdown prompt.
/// </summary>
public class RoatanTableTalkUI : MonoBehaviour
{
    public const int MaxSeatBubbles = 4; // 0: Top, 1: Left, 2: Right, 3: Human (Bottom)
    public const int HumanSeatIndex = 3;

    private const float BubblePopDuration = 0.18f;
    private const float BubbleHoldDuration = 2.10f;
    private const float BubbleFadeDuration = 0.35f;
    private const float BubbleHardTimeoutMargin = 0.25f;

    private TMP_FontAsset uiFont;
    private Transform parentRoot;
    private Action<string> onHumanEmote;

    // Quick-Emote Wheel overlay
    private GameObject emoteWheelPanel;

    // Per-seat tracked speech bubbles (reused & watchdog-protected so none can ever stick)
    private readonly GameObject[] seatBubbleObjs = new GameObject[MaxSeatBubbles];
    private readonly RectTransform[] seatBubbleRects = new RectTransform[MaxSeatBubbles];
    private readonly CanvasGroup[] seatBubbleCanvasGroups = new CanvasGroup[MaxSeatBubbles];
    private readonly TMP_Text[] seatBubbleLabels = new TMP_Text[MaxSeatBubbles];
    private readonly Coroutine[] activeBubbleCoroutines = new Coroutine[MaxSeatBubbles];
    private readonly float[] bubbleHideAtUnscaledTime = new float[MaxSeatBubbles];
    private readonly int[] seatBubbleGenerations = new int[MaxSeatBubbles];

    // Celebratory banner
    private GameObject celebrationBannerObj;
    private Image celebrationBannerBg;
    private CanvasGroup celebrationCanvasGroup;
    private TMP_Text celebrationTitleLabel;
    private TMP_Text celebrationSubLabel;
    private Coroutine celebrationRoutine;
    private float celebrationHideAtUnscaledTime;
    private int celebrationGeneration;

    // "Call Your Padna" CHOOSE_POSER modal
    private GameObject choosePoserOverlay;
    private TMP_Text choosePoserTimerLabel;
    private Image choosePoserProgressFill;
    private Button poseMyselfBtn;
    private Button passPadnaBtn;
    private TMP_Text passPadnaBtnLabel;
    private Coroutine choosePoserRoutine;
    private Action<PoserChoice> activePoserCallback;
    private bool poserChoiceResolved;

    public bool IsEmoteWheelOpen => emoteWheelPanel != null && emoteWheelPanel.activeSelf;

    public void Initialize(Transform matchScreenRoot, TMP_FontAsset font, Action<string> onHumanEmoteSelected)
    {
        parentRoot = matchScreenRoot;
        uiFont = font;
        onHumanEmote = onHumanEmoteSelected;

        BuildEmoteWheel();
        BuildCelebrationBanner();
        BuildChoosePoserModal();
    }

    private void Update()
    {
        float now = Time.unscaledTime;

        for (int i = 0; i < MaxSeatBubbles; i++)
        {
            if (seatBubbleObjs[i] != null && seatBubbleObjs[i].activeSelf)
            {
                if (bubbleHideAtUnscaledTime[i] <= 0f || now >= bubbleHideAtUnscaledTime[i])
                {
                    seatBubbleGenerations[i]++;
                    if (activeBubbleCoroutines[i] != null)
                    {
                        StopCoroutine(activeBubbleCoroutines[i]);
                        activeBubbleCoroutines[i] = null;
                    }
                    ForceHideSeatBubble(i);
                }
            }
        }

        if (celebrationBannerObj != null && celebrationBannerObj.activeSelf)
        {
            if (celebrationHideAtUnscaledTime <= 0f || now >= celebrationHideAtUnscaledTime)
            {
                celebrationGeneration++;
                if (celebrationRoutine != null)
                {
                    StopCoroutine(celebrationRoutine);
                    celebrationRoutine = null;
                }
                ForceHideCelebrationBanner();
            }
        }
    }

    private void OnDisable()
    {
        ClearAllBubbles();
    }

    public void ToggleEmoteWheel()
    {
        if (emoteWheelPanel == null) return;
        bool next = !emoteWheelPanel.activeSelf;
        emoteWheelPanel.SetActive(next);
        if (next) emoteWheelPanel.transform.SetAsLastSibling();
    }

    /// <summary>
    /// Immediately stops all active speech-bubble and celebration-banner coroutines and hides
    /// every seat bubble and banner. Call on round start, match/mode start, and menu transitions.
    /// </summary>
    public void ClearAllBubbles()
    {
        for (int i = 0; i < MaxSeatBubbles; i++)
        {
            seatBubbleGenerations[i]++;
            if (activeBubbleCoroutines[i] != null)
            {
                StopCoroutine(activeBubbleCoroutines[i]);
                activeBubbleCoroutines[i] = null;
            }
            ForceHideSeatBubble(i);
        }

        celebrationGeneration++;
        if (celebrationRoutine != null)
        {
            StopCoroutine(celebrationRoutine);
            celebrationRoutine = null;
        }
        ForceHideCelebrationBanner();
    }

    public void HideAllOverlays()
    {
        ClearAllBubbles();
        if (emoteWheelPanel != null) emoteWheelPanel.SetActive(false);
        if (choosePoserOverlay != null) choosePoserOverlay.SetActive(false);
        if (choosePoserRoutine != null)
        {
            StopCoroutine(choosePoserRoutine);
            choosePoserRoutine = null;
        }
    }

    public void ShowSpeechBubble(string speakerName, string phrase, Vector2 anchoredPos)
    {
        int inferredSeat = anchoredPos.y < 0f
            ? HumanSeatIndex
            : (anchoredPos.x < -100f ? 1 : (anchoredPos.x > 100f ? 2 : 0));
        ShowSpeechBubble(inferredSeat, speakerName, phrase, anchoredPos);
    }

    public void ShowSpeechBubble(int seatIndex, string speakerName, string phrase, Vector2 anchoredPos)
    {
        if (parentRoot == null || !isActiveAndEnabled) return;
        int slot = (seatIndex >= 0 && seatIndex < MaxSeatBubbles) ? seatIndex : 0;

        if (activeBubbleCoroutines[slot] != null)
        {
            StopCoroutine(activeBubbleCoroutines[slot]);
            activeBubbleCoroutines[slot] = null;
        }

        EnsureSeatBubble(slot);

        int gen = ++seatBubbleGenerations[slot];
        GameObject bubbleObj = seatBubbleObjs[slot];
        RectTransform rect = seatBubbleRects[slot];
        CanvasGroup cg = seatBubbleCanvasGroups[slot];
        TMP_Text label = seatBubbleLabels[slot];

        label.text = $"{speakerName}: \"{phrase}\"";
        rect.anchoredPosition = anchoredPos;
        rect.localScale = Vector3.one * 0.6f;
        cg.alpha = 1f;
        bubbleObj.SetActive(true);
        bubbleObj.transform.SetAsLastSibling();

        float totalDuration = BubblePopDuration + BubbleHoldDuration + BubbleFadeDuration;
        bubbleHideAtUnscaledTime[slot] = Time.unscaledTime + totalDuration + BubbleHardTimeoutMargin;
        activeBubbleCoroutines[slot] = StartCoroutine(AnimateSpeechBubbleCoroutine(slot, gen, anchoredPos));
    }

    public void ShowCelebrationBanner(string title, string subtitle, Color accentColor, float holdSeconds = 2.6f)
    {
        if (celebrationBannerObj == null || !isActiveAndEnabled) return;
        if (celebrationRoutine != null)
        {
            StopCoroutine(celebrationRoutine);
            celebrationRoutine = null;
        }

        int gen = ++celebrationGeneration;
        float clampedHold = Mathf.Max(0.4f, holdSeconds);
        celebrationHideAtUnscaledTime = Time.unscaledTime + 0.24f + clampedHold + 0.30f + 0.35f;
        celebrationRoutine = StartCoroutine(AnimateCelebrationBannerCoroutine(gen, title, subtitle, accentColor, clampedHold));
    }

    public void ShowChoosePoserPrompt(string partnerName, float timeoutSeconds, Action<PoserChoice> onConfirmed)
    {
        if (choosePoserOverlay == null)
        {
            onConfirmed?.Invoke(PoserChoice.POSE_MYSELF);
            return;
        }

        if (choosePoserRoutine != null) StopCoroutine(choosePoserRoutine);
        activePoserCallback = onConfirmed;
        poserChoiceResolved = false;

        passPadnaBtnLabel.text = $"Pass Pose to Padna ({partnerName})";
        choosePoserOverlay.SetActive(true);
        choosePoserOverlay.transform.SetAsLastSibling();

        choosePoserRoutine = StartCoroutine(RunChoosePoserCountdownCoroutine(timeoutSeconds));
    }

    private void ConfirmPoser(PoserChoice choice)
    {
        if (poserChoiceResolved) return;
        poserChoiceResolved = true;

        if (choosePoserRoutine != null)
        {
            StopCoroutine(choosePoserRoutine);
            choosePoserRoutine = null;
        }

        if (choosePoserOverlay != null) choosePoserOverlay.SetActive(false);
        RoatanSoundFX.Instance?.PlayTap();
        activePoserCallback?.Invoke(choice);
    }

    private IEnumerator RunChoosePoserCountdownCoroutine(float duration)
    {
        float remaining = duration;
        while (remaining > 0f && !poserChoiceResolved)
        {
            int secs = Mathf.CeilToInt(remaining);
            choosePoserTimerLabel.text = $"Auto-selecting \"Pose Myself\" in {secs}s...";
            if (choosePoserProgressFill != null)
            {
                choosePoserProgressFill.fillAmount = Mathf.Clamp01(remaining / duration);
            }
            remaining -= Time.deltaTime;
            yield return null;
        }

        if (!poserChoiceResolved)
        {
            ConfirmPoser(PoserChoice.POSE_MYSELF);
        }
    }

    private void BuildEmoteWheel()
    {
        emoteWheelPanel = CreateCard(parentRoot, "QuickEmoteWheel", new Color(0.05f, 0.16f, 0.22f, 0.96f), Vector2.zero, new Vector2(860f, 456f));
        RectTransform wheelRect = emoteWheelPanel.GetComponent<RectTransform>();
        wheelRect.anchorMin = new Vector2(0.5f, 0f);
        wheelRect.anchorMax = new Vector2(0.5f, 0f);
        wheelRect.pivot = new Vector2(0.5f, 0f);
        wheelRect.anchoredPosition = new Vector2(0f, 430f);

        GameObject border = new GameObject("GoldBorder");
        border.transform.SetParent(emoteWheelPanel.transform, false);
        RectTransform br = border.AddComponent<RectTransform>();
        br.anchorMin = Vector2.zero;
        br.anchorMax = Vector2.one;
        br.offsetMin = Vector2.zero;
        br.offsetMax = Vector2.zero;
        Image bImg = border.AddComponent<Image>();
        bImg.sprite = RoatanVisualAssets.RoundedOutlineSprite;
        bImg.type = Image.Type.Sliced;
        bImg.color = RoatanVisualAssets.IslandGold;
        bImg.raycastTarget = false;

        CreateText(emoteWheelPanel.transform, "WheelTitle", "TABLE TALK  •  QUICK EMOTES", 25, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, 192f), new Vector2(780f, 42f));

        // 2 rows x 3 columns + prominent centered 7th pill ("Key / Capicú!")
        Vector2[] offsets =
        {
            new Vector2(-265f, 95f), new Vector2(0f, 95f), new Vector2(265f, 95f),
            new Vector2(-265f, -15f), new Vector2(0f, -15f), new Vector2(265f, -15f),
            new Vector2(0f, -135f)
        };

        for (int i = 0; i < RoatanQuickEmotes.Phrases.Length && i < offsets.Length; i++)
        {
            string phrase = RoatanQuickEmotes.Phrases[i];
            bool isKeyEmote = i == 6;
            Button btn = CreatePillButton(
                emoteWheelPanel.transform,
                $"Emote_{i}",
                phrase,
                isKeyEmote ? new Color(0.58f, 0.39f, 0.08f, 1f) : new Color(0.10f, 0.30f, 0.36f, 1f),
                offsets[i],
                isKeyEmote ? new Vector2(360f, 84f) : new Vector2(248f, 88f),
                isKeyEmote ? 26 : 24,
                () =>
                {
                    emoteWheelPanel.SetActive(false);
                    RoatanSoundFX.Instance?.PlayEmoteCallout();
                    ShowSpeechBubble(HumanSeatIndex, "You", phrase, new Vector2(0f, -430f));
                    onHumanEmote?.Invoke(phrase);
                });
            TMP_Text lbl = btn.GetComponentInChildren<TMP_Text>();
            if (lbl != null) lbl.color = isKeyEmote ? new Color(1f, 0.96f, 0.78f, 1f) : Color.white;
        }

        emoteWheelPanel.SetActive(false);
    }

    private void BuildCelebrationBanner()
    {
        celebrationBannerObj = CreateCard(parentRoot, "EventCelebrationBanner", new Color(0.08f, 0.14f, 0.20f, 0.96f), new Vector2(0f, 340f), new Vector2(920f, 178f));
        celebrationBannerBg = celebrationBannerObj.GetComponent<Image>();
        celebrationBannerBg.raycastTarget = false;
        celebrationCanvasGroup = celebrationBannerObj.AddComponent<CanvasGroup>();
        celebrationCanvasGroup.alpha = 0f;
        celebrationCanvasGroup.blocksRaycasts = false;

        GameObject border = new GameObject("Outline");
        border.transform.SetParent(celebrationBannerObj.transform, false);
        RectTransform br = border.AddComponent<RectTransform>();
        br.anchorMin = Vector2.zero;
        br.anchorMax = Vector2.one;
        br.offsetMin = Vector2.zero;
        br.offsetMax = Vector2.zero;
        Image bImg = border.AddComponent<Image>();
        bImg.sprite = RoatanVisualAssets.RoundedOutlineSprite;
        bImg.type = Image.Type.Sliced;
        bImg.color = RoatanVisualAssets.IslandGold;
        bImg.raycastTarget = false;

        celebrationTitleLabel = CreateText(celebrationBannerObj.transform, "Title", "", 50, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, 30f), new Vector2(860f, 68f));
        celebrationSubLabel = CreateText(celebrationBannerObj.transform, "Sub", "", 28, FontStyles.Bold, Color.white, new Vector2(0f, -34f), new Vector2(860f, 52f));

        celebrationBannerObj.SetActive(false);
    }

    private void BuildChoosePoserModal()
    {
        choosePoserOverlay = new GameObject("ChoosePoserModal");
        choosePoserOverlay.transform.SetParent(parentRoot, false);
        RectTransform root = choosePoserOverlay.AddComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        Image dim = choosePoserOverlay.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.72f);

        GameObject card = CreateCard(choosePoserOverlay.transform, "PadnaCard", RoatanVisualAssets.DarkSlate, Vector2.zero, new Vector2(880f, 590f));
        CreateText(card.transform, "Title", "CALL YOUR PADNA!", 46, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, 215f), new Vector2(820f, 64f));
        CreateText(card.transform, "Desc", "Your pair won the hand! Choose who poses any tile to open the next hand (partner tiles stay hidden).", 27, FontStyles.Normal, new Color(0.88f, 0.94f, 0.95f, 1f), new Vector2(0f, 125f), new Vector2(800f, 92f));

        // Countdown progress bar
        GameObject barBg = CreateCard(card.transform, "TimerBarBg", new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(0f, 44f), new Vector2(720f, 22f));
        GameObject barFillObj = new GameObject("TimerBarFill");
        barFillObj.transform.SetParent(barBg.transform, false);
        RectTransform fillRect = barFillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        choosePoserProgressFill = barFillObj.AddComponent<Image>();
        choosePoserProgressFill.sprite = RoatanVisualAssets.RoundedCardSprite;
        choosePoserProgressFill.type = Image.Type.Filled;
        choosePoserProgressFill.fillMethod = Image.FillMethod.Horizontal;
        choosePoserProgressFill.color = RoatanVisualAssets.IslandGold;

        choosePoserTimerLabel = CreateText(card.transform, "TimerText", "Auto-selecting \"Pose Myself\" in 8s...", 24, FontStyles.Italic, new Color(0.95f, 0.85f, 0.55f, 1f), new Vector2(0f, 4f), new Vector2(760f, 38f));

        poseMyselfBtn = CreatePillButton(card.transform, "BtnPoseMyself", "Pose Myself  (Default)", RoatanVisualAssets.PalmGreen, new Vector2(0f, -92f), new Vector2(680f, 98f), 32, () => ConfirmPoser(PoserChoice.POSE_MYSELF));
        passPadnaBtn = CreatePillButton(card.transform, "BtnPassPadna", "Pass Pose to Padna", RoatanVisualAssets.BrightLagoon, new Vector2(0f, -214f), new Vector2(680f, 98f), 32, () => ConfirmPoser(PoserChoice.PASS_TO_PADNA));
        passPadnaBtnLabel = passPadnaBtn.GetComponentInChildren<TMP_Text>();

        choosePoserOverlay.SetActive(false);
    }

    private void EnsureSeatBubble(int seatIndex)
    {
        if (seatBubbleObjs[seatIndex] != null) return;

        GameObject bubble = CreateCard(parentRoot, $"EmoteBubble_Seat{seatIndex}", new Color(0.99f, 0.96f, 0.88f, 0.98f), Vector2.zero, new Vector2(360f, 84f));
        Image bg = bubble.GetComponent<Image>();
        if (bg != null) bg.raycastTarget = false;

        RectTransform rect = bubble.GetComponent<RectTransform>();
        CanvasGroup cg = bubble.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        GameObject border = new GameObject("Border");
        border.transform.SetParent(bubble.transform, false);
        RectTransform br = border.AddComponent<RectTransform>();
        br.anchorMin = Vector2.zero;
        br.anchorMax = Vector2.one;
        br.offsetMin = Vector2.zero;
        br.offsetMax = Vector2.zero;
        Image bImg = border.AddComponent<Image>();
        bImg.sprite = RoatanVisualAssets.RoundedOutlineSprite;
        bImg.type = Image.Type.Sliced;
        bImg.color = RoatanVisualAssets.IslandGold;
        bImg.raycastTarget = false;

        TMP_Text phraseLabel = CreateText(bubble.transform, "Phrase", "", 25, FontStyles.Bold, new Color(0.08f, 0.16f, 0.20f, 1f), Vector2.zero, new Vector2(334f, 70f));

        bubble.SetActive(false);

        seatBubbleObjs[seatIndex] = bubble;
        seatBubbleRects[seatIndex] = rect;
        seatBubbleCanvasGroups[seatIndex] = cg;
        seatBubbleLabels[seatIndex] = phraseLabel;
    }

    private void ForceHideSeatBubble(int seatIndex)
    {
        bubbleHideAtUnscaledTime[seatIndex] = 0f;
        if (seatBubbleCanvasGroups[seatIndex] != null)
        {
            seatBubbleCanvasGroups[seatIndex].alpha = 0f;
        }
        if (seatBubbleObjs[seatIndex] != null)
        {
            seatBubbleObjs[seatIndex].SetActive(false);
        }
    }

    private void ForceHideCelebrationBanner()
    {
        celebrationHideAtUnscaledTime = 0f;
        if (celebrationCanvasGroup != null)
        {
            celebrationCanvasGroup.alpha = 0f;
        }
        if (celebrationBannerObj != null)
        {
            celebrationBannerObj.SetActive(false);
        }
    }

    private IEnumerator AnimateSpeechBubbleCoroutine(int seatIndex, int generation, Vector2 basePos)
    {
        GameObject bubbleObj = seatBubbleObjs[seatIndex];
        RectTransform rect = seatBubbleRects[seatIndex];
        CanvasGroup cg = seatBubbleCanvasGroups[seatIndex];

        try
        {
            // Pop-in (unscaled time so pauses/timeScale changes never stall the bubble)
            float t = 0f;
            while (t < BubblePopDuration && rect != null)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / BubblePopDuration);
                rect.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, 1f - (1f - p) * (1f - p));
                yield return null;
            }
            if (rect != null) rect.localScale = Vector3.one;

            // Hold (unscaled time)
            float holdElapsed = 0f;
            while (holdElapsed < BubbleHoldDuration)
            {
                holdElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // Drift up and fade out (unscaled time)
            t = 0f;
            while (t < BubbleFadeDuration && rect != null && cg != null)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / BubbleFadeDuration);
                cg.alpha = 1f - p;
                rect.anchoredPosition = basePos + new Vector2(0f, p * 36f);
                yield return null;
            }
        }
        finally
        {
            if (seatBubbleGenerations[seatIndex] == generation)
            {
                if (cg != null) cg.alpha = 0f;
                if (bubbleObj != null) bubbleObj.SetActive(false);
                bubbleHideAtUnscaledTime[seatIndex] = 0f;
                activeBubbleCoroutines[seatIndex] = null;
            }
        }
    }

    private IEnumerator AnimateCelebrationBannerCoroutine(int generation, string title, string subtitle, Color accentColor, float holdSeconds)
    {
        RectTransform rect = celebrationBannerObj.GetComponent<RectTransform>();
        if (celebrationCanvasGroup == null)
        {
            celebrationCanvasGroup = celebrationBannerObj.GetComponent<CanvasGroup>();
            if (celebrationCanvasGroup == null) celebrationCanvasGroup = celebrationBannerObj.AddComponent<CanvasGroup>();
        }

        celebrationBannerBg.color = accentColor;
        celebrationTitleLabel.text = title;
        celebrationSubLabel.text = subtitle;
        celebrationCanvasGroup.alpha = 1f;
        celebrationBannerObj.SetActive(true);
        celebrationBannerObj.transform.SetAsLastSibling();

        try
        {
            float t = 0f;
            while (t < 0.24f)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / 0.24f);
                rect.localScale = Vector3.one * Mathf.Lerp(0.65f, 1.03f, 1f - (1f - p) * (1f - p));
                yield return null;
            }
            rect.localScale = Vector3.one;

            float holdElapsed = 0f;
            while (holdElapsed < holdSeconds)
            {
                holdElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            t = 0f;
            while (t < 0.30f)
            {
                t += Time.unscaledDeltaTime;
                celebrationCanvasGroup.alpha = 1f - Mathf.Clamp01(t / 0.30f);
                yield return null;
            }
        }
        finally
        {
            if (celebrationGeneration == generation)
            {
                ForceHideCelebrationBanner();
                celebrationRoutine = null;
            }
        }
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

    private Button CreatePillButton(Transform parent, string name, string label, Color color, Vector2 pos, Vector2 size, int fontSize, Action onClick)
    {
        GameObject obj = CreateCard(parent, name, color, pos, size);
        Button btn = obj.AddComponent<Button>();
        btn.targetGraphic = obj.GetComponent<Image>();
        btn.onClick.AddListener(() => onClick?.Invoke());
        CreateText(obj.transform, "Label", label, fontSize, FontStyles.Bold, Color.white, Vector2.zero, size - new Vector2(20f, 12f));
        return btn;
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
