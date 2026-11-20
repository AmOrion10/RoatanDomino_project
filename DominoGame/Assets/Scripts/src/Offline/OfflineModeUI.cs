using System.Collections.Generic;

using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;

using TMPro;

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class OfflineModeUI : MonoBehaviour
{
    public static OfflineModeUI Instance { get; private set; }

    private static BotDifficulty selectedDifficulty = BotDifficulty.Novice;
    private static int selectedBotCount = 3;

    private Canvas rootCanvas;
    private GameObject menuEntryButtonObj;
    private GameObject setupModalObj;
    private GameObject inGameHudObj;
    private GameObject postGameBarObj;

    private TMP_Text statusLabel;
    private TMP_Text boneyardLabel;
    private TMP_Text difficultyDescriptionLabel;

    private readonly Dictionary<BotDifficulty, Image> difficultyButtonImages = new Dictionary<BotDifficulty, Image>();
    private readonly Dictionary<int, Image> botCountButtonImages = new Dictionary<int, Image>();

    private OfflineBotClient subscribedClient;

    private static readonly Color AccentBlue = new Color(0.16f, 0.50f, 0.85f, 1f);
    private static readonly Color AccentGreen = new Color(0.15f, 0.62f, 0.36f, 1f);
    private static readonly Color AccentGold = new Color(0.84f, 0.56f, 0.12f, 1f);
    private static readonly Color DarkPanel = new Color(0.10f, 0.13f, 0.20f, 0.96f);
    private static readonly Color NeutralButton = new Color(0.22f, 0.26f, 0.36f, 1f);

    // Legacy desktop offline launcher retired in favor of RoatanMobileApp portrait UI.
    public static void BootstrapLegacy()
    {
        if (Instance != null) return;
        GameObject host = new GameObject("OfflineModeUI");
        DontDestroyOnLoad(host);
        host.AddComponent<OfflineModeUI>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildCanvas();
        SceneManager.activeSceneChanged += OnSceneChanged;
    }

    private void Start()
    {
        RefreshForScene(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnSceneChanged;
        UnsubscribeOfflineClient();
    }

    public void OpenSetupModal()
    {
        if (setupModalObj != null)
        {
            setupModalObj.SetActive(true);
            RefreshModalHighlights();
        }
    }

    private void OnSceneChanged(Scene oldScene, Scene newScene)
    {
        RefreshForScene(newScene.name);
    }

    private void RefreshForScene(string sceneName)
    {
        UnsubscribeOfflineClient();

        bool isMenu = sceneName == "MenuScene" || sceneName == "MenuPlayScene";
        bool isGame = sceneName == "GameScene";
        bool isOfflineActive = Networking.Instance != null && Networking.Instance.IsOfflineMode;

        menuEntryButtonObj.SetActive(isMenu);
        setupModalObj.SetActive(false);
        inGameHudObj.SetActive(isGame && isOfflineActive);
        postGameBarObj.SetActive(false);

        if (isGame && isOfflineActive && Networking.Instance.ActiveOfflineClient != null)
        {
            SubscribeOfflineClient(Networking.Instance.ActiveOfflineClient);
        }
    }

    private void SubscribeOfflineClient(OfflineBotClient offlineClient)
    {
        subscribedClient = offlineClient;
        subscribedClient.StatusUpdated += OnOfflineStatusUpdated;
        subscribedClient.MatchEndedWithDetails += OnOfflineMatchEnded;
        UpdateHud(offlineClient.LastStatusMessage);
    }

    private void UnsubscribeOfflineClient()
    {
        if (subscribedClient != null)
        {
            subscribedClient.StatusUpdated -= OnOfflineStatusUpdated;
            subscribedClient.MatchEndedWithDetails -= OnOfflineMatchEnded;
            subscribedClient = null;
        }
    }

    private void OnOfflineStatusUpdated(string message)
    {
        UpdateHud(message);
    }

    private void OnOfflineMatchEnded(EndGameDto dto, List<OfflineGameEngine.OfflinePlayer> ranked)
    {
        if (postGameBarObj != null)
        {
            postGameBarObj.SetActive(true);
        }
    }

    private void UpdateHud(string status)
    {
        if (subscribedClient == null) return;
        if (boneyardLabel != null)
        {
            boneyardLabel.text = $"OFFLINE  |  {subscribedClient.Difficulty} Bots  |  Boneyard: {subscribedClient.BoneyardCount}";
        }
        if (statusLabel != null)
        {
            statusLabel.text = status;
        }
    }

    private void LaunchOfflineMatch()
    {
        if (Networking.Instance == null)
        {
            Debug.LogError("[OfflineModeUI] Networking.Instance is missing in this scene.");
            return;
        }

        setupModalObj.SetActive(false);
        OfflineBotClient offlineClient = Networking.Instance.StartOfflineMode(selectedDifficulty, selectedBotCount);
        InLobby.SetOfflineLobby(offlineClient.OfflineLobby);
        SceneManager.LoadScene("GameScene");
    }

    private void ReturnToMenu()
    {
        if (Networking.Instance != null && Networking.Instance.IsOfflineMode)
        {
            Networking.Instance.StopOfflineMode();
        }
        SceneManager.LoadScene("MenuScene");
    }

    private void BuildCanvas()
    {
        GameObject canvasObj = new GameObject("OfflineCanvas");
        canvasObj.transform.SetParent(transform, false);

        rootCanvas = canvasObj.AddComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        BuildMenuEntryButton(canvasObj.transform);
        BuildSetupModal(canvasObj.transform);
        BuildInGameHud(canvasObj.transform);
        BuildPostGameBar(canvasObj.transform);
    }

    private void BuildMenuEntryButton(Transform parent)
    {
        menuEntryButtonObj = CreateButton(
            parent,
            "BtnPlayOffline",
            "Play Offline (vs Bots)",
            AccentGreen,
            new Vector2(360, 68),
            26,
            OpenSetupModal).gameObject;

        RectTransform rect = menuEntryButtonObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 36f);
    }

    private void BuildSetupModal(Transform parent)
    {
        setupModalObj = new GameObject("OfflineSetupModal");
        setupModalObj.transform.SetParent(parent, false);
        RectTransform backdropRect = setupModalObj.AddComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;
        Image backdrop = setupModalObj.AddComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.72f);

        GameObject card = new GameObject("ModalCard");
        card.transform.SetParent(setupModalObj.transform, false);
        RectTransform cardRect = card.AddComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(760, 560);
        cardRect.anchoredPosition = Vector2.zero;
        Image cardImg = card.AddComponent<Image>();
        cardImg.color = DarkPanel;

        CreateLabel(card.transform, "Title", "Play Offline vs Bots", 36, FontStyles.Bold, Color.white, new Vector2(0, 215), new Vector2(680, 56));
        CreateLabel(card.transform, "DiffHeader", "Select Bot Difficulty", 24, FontStyles.Bold, new Color(0.85f, 0.90f, 1f), new Vector2(0, 145), new Vector2(680, 36));

        // 3 Difficulty buttons: Beginner, Novice, Pro
        BotDifficulty[] diffs = { BotDifficulty.Beginner, BotDifficulty.Novice, BotDifficulty.Pro };
        float[] diffX = { -215f, 0f, 215f };
        for (int i = 0; i < diffs.Length; i++)
        {
            BotDifficulty diff = diffs[i];
            Button btn = CreateButton(card.transform, $"BtnDiff_{diff}", diff.ToString(), NeutralButton, new Vector2(195, 62), 24, () =>
            {
                selectedDifficulty = diff;
                RefreshModalHighlights();
            });
            btn.GetComponent<RectTransform>().anchoredPosition = new Vector2(diffX[i], 82f);
            difficultyButtonImages[diff] = btn.GetComponent<Image>();
        }

        difficultyDescriptionLabel = CreateLabel(
            card.transform,
            "DiffDesc",
            "",
            20,
            FontStyles.Italic,
            new Color(0.76f, 0.84f, 0.95f),
            new Vector2(0, 20f),
            new Vector2(680, 52));

        CreateLabel(card.transform, "CountHeader", "Number of Bot Opponents", 24, FontStyles.Bold, new Color(0.85f, 0.90f, 1f), new Vector2(0, -45f), new Vector2(680, 36));

        int[] counts = { 1, 2, 3 };
        string[] countLabels = { "1 Bot (1v1)", "2 Bots (3P)", "3 Bots (4P)" };
        float[] countX = { -215f, 0f, 215f };
        for (int i = 0; i < counts.Length; i++)
        {
            int count = counts[i];
            Button btn = CreateButton(card.transform, $"BtnCount_{count}", countLabels[i], NeutralButton, new Vector2(195, 58), 22, () =>
            {
                selectedBotCount = count;
                RefreshModalHighlights();
            });
            btn.GetComponent<RectTransform>().anchoredPosition = new Vector2(countX[i], -105f);
            botCountButtonImages[count] = btn.GetComponent<Image>();
        }

        Button startBtn = CreateButton(card.transform, "BtnStartMatch", "Start Match", AccentGreen, new Vector2(260, 68), 26, LaunchOfflineMatch);
        startBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(145f, -205f);

        Button cancelBtn = CreateButton(card.transform, "BtnCancel", "Cancel", NeutralButton, new Vector2(200, 68), 24, () =>
        {
            setupModalObj.SetActive(false);
        });
        cancelBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-145f, -205f);

        setupModalObj.SetActive(false);
    }

    private void BuildInGameHud(Transform parent)
    {
        inGameHudObj = new GameObject("OfflineInGameHud");
        inGameHudObj.transform.SetParent(parent, false);
        RectTransform rect = inGameHudObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(760, 76);
        rect.anchoredPosition = new Vector2(0f, -12f);

        Image bg = inGameHudObj.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.11f, 0.18f, 0.82f);
        bg.raycastTarget = false;

        boneyardLabel = CreateLabel(inGameHudObj.transform, "BoneyardInfo", "OFFLINE", 20, FontStyles.Bold, new Color(1f, 0.86f, 0.42f), new Vector2(0, 16f), new Vector2(730, 30));
        boneyardLabel.raycastTarget = false;

        statusLabel = CreateLabel(inGameHudObj.transform, "StatusInfo", "", 19, FontStyles.Normal, Color.white, new Vector2(0, -16f), new Vector2(730, 30));
        statusLabel.raycastTarget = false;

        inGameHudObj.SetActive(false);
    }

    private void BuildPostGameBar(Transform parent)
    {
        postGameBarObj = new GameObject("OfflinePostGameBar");
        postGameBarObj.transform.SetParent(parent, false);
        RectTransform rect = postGameBarObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(760, 96);
        rect.anchoredPosition = new Vector2(0f, 28f);

        Image bg = postGameBarObj.AddComponent<Image>();
        bg.color = DarkPanel;

        Button replayBtn = CreateButton(postGameBarObj.transform, "BtnPlayAgain", "Play Again", AccentGreen, new Vector2(210, 62), 23, LaunchOfflineMatch);
        replayBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-235f, 0f);

        Button diffBtn = CreateButton(postGameBarObj.transform, "BtnChangeDiff", "Difficulty", AccentGold, new Vector2(210, 62), 23, OpenSetupModal);
        diffBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 0f);

        Button menuBtn = CreateButton(postGameBarObj.transform, "BtnBackMenu", "Main Menu", NeutralButton, new Vector2(210, 62), 23, ReturnToMenu);
        menuBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(235f, 0f);

        postGameBarObj.SetActive(false);
    }

    private void RefreshModalHighlights()
    {
        foreach (KeyValuePair<BotDifficulty, Image> kvp in difficultyButtonImages)
        {
            kvp.Value.color = kvp.Key == selectedDifficulty ? AccentBlue : NeutralButton;
        }

        foreach (KeyValuePair<int, Image> kvp in botCountButtonImages)
        {
            kvp.Value.color = kvp.Key == selectedBotCount ? AccentBlue : NeutralButton;
        }

        if (difficultyDescriptionLabel != null)
        {
            switch (selectedDifficulty)
            {
                case BotDifficulty.Beginner:
                    difficultyDescriptionLabel.text = "Beginner: Relaxed, mostly random moves — great for learning the board.";
                    break;
                case BotDifficulty.Novice:
                    difficultyDescriptionLabel.text = "Novice: Sheds heavy dominoes & doubles while keeping its hand playable.";
                    break;
                case BotDifficulty.Pro:
                    difficultyDescriptionLabel.text = "Pro: Counts played tiles, tracks your passes, and actively blocks your suits.";
                    break;
            }
        }
    }

    private static Button CreateButton(
        Transform parent,
        string name,
        string label,
        Color bgColor,
        Vector2 size,
        int fontSize,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        RectTransform rect = btnObj.AddComponent<RectTransform>();
        rect.sizeDelta = size;

        Image img = btnObj.AddComponent<Image>();
        img.color = bgColor;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        TMP_Text text = CreateLabel(btnObj.transform, "Label", label, fontSize, FontStyles.Bold, Color.white, Vector2.zero, size - new Vector2(16, 8));
        text.raycastTarget = false;

        return btn;
    }

    private static TMP_Text CreateLabel(
        Transform parent,
        string name,
        string content,
        int fontSize,
        FontStyles style,
        Color color,
        Vector2 anchoredPos,
        Vector2 size)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;

        return tmp;
    }
}
