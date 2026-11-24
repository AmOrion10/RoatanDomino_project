using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using DominoCommon.com.domino.common.Scripts.Enums;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;

using TMPro;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RoatanMobileApp : MonoBehaviour
{
    private const string PrefWinsKey = "RoatanDomino_Wins";
    private const string PrefGamesKey = "RoatanDomino_Games";
    private const string PrefPresetModeKey = "RoatanDomino_PresetMode";
    private const string PrefPresetFormatKey = "RoatanDomino_PresetFormat";
    private const string PrefPresetOneOneKey = "RoatanDomino_PresetOneOne";
    private const string PrefPresetDiffKey = "RoatanDomino_PresetDiff";
    private const string PrefPresetBotsKey = "RoatanDomino_PresetBots";

    public static RoatanMobileApp Instance { get; private set; }

    private TMP_FontAsset uiFont;
    private Canvas rootCanvas;
    private RectTransform safeAreaRoot;

    // Screens & Modals
    private GameObject homeScreen;
    private GameObject matchSetupSheet;
    private GameObject utilityDrawerOverlay;
    private GameObject matchScreen;
    private GameObject howToPlayModal;
    private GameObject roundSummaryOverlay;
    private RoatanOnlineController onlineController;

    // Home UI refs (Decluttered 2-step lobby + compact top bar + utility drawer)
    private TMP_Text subtitleLabel;
    private TMP_Text subtitleShadowLabel;
    private TMP_Text activePresetPillLabel;
    private TMP_Text topLangPillLabel;
    private TMP_Text drawerStatsLabel;
    private TMP_InputField drawerServerUrlInput;
    private TMP_Text drawerServerPreviewLabel;
    private readonly Dictionary<RoatanServerPreset, Image> drawerServerPresetBtns = new Dictionary<RoatanServerPreset, Image>();
    private RoatanServerPreset drawerServerPreset = RoatanServerPreset.Localhost;
    private bool suppressDrawerServerEvents;
    private TMP_Text statsLabel;
    private TMP_Text modeHeadingLabel;
    private TMP_Text cerradoTitleLabel;
    private TMP_Text cerradoDescLabel;
    private TMP_Text drawTitleLabel;
    private TMP_Text drawDescLabel;
    private TMP_Text diffHeadingLabel;
    private TMP_Text diffDescLabel;
    private TMP_Text formatDescLabel;
    private GameObject setupRuleTooltipCard;
    private TMP_Text setupRuleTooltipText;
    private Button oneOnePlayoffBtn;
    private Image oneOnePlayoffBtnImg;
    private TMP_Text oneOnePlayoffBtnLabel;
    private TMP_Text opponentsHeaderLabel;
    private GameObject opponentsRowObj;
    private TMP_Text fixed2v2NoteLabel;
    private TMP_Text playNowBtnLabel;
    private TMP_Text homeSoundBtnLabel;
    private TMP_Text howToPlayBtnLabel;
    private TMP_Text onlineModeBtnLabel;

    // Tabbed How-To-Play refs (Six-Lov, Two-Best, Cut-Throat, Game Bruk)
    private TMP_Text rulesBodyLabel;
    private readonly Dictionary<string, Image> rulesTabImages = new Dictionary<string, Image>();
    private string activeRulesTab = "Six-Lov";

    private readonly Dictionary<RoatanGameMode, Image> modeCardImages = new Dictionary<RoatanGameMode, Image>();
    private readonly Dictionary<MatchFormat, Image> formatBtnImages = new Dictionary<MatchFormat, Image>();
    private readonly Dictionary<BotDifficulty, Image> diffBtnImages = new Dictionary<BotDifficulty, Image>();
    private readonly Dictionary<BotDifficulty, TMP_Text> diffBtnLabels = new Dictionary<BotDifficulty, TMP_Text>();
    private readonly Dictionary<int, Image> botCountBtnImages = new Dictionary<int, Image>();
    private readonly Dictionary<int, GameObject> botCountBtnObjs = new Dictionary<int, GameObject>();

    // Match UI refs
    private RoatanBoardView boardView;
    private TMP_Text matchBackBtnLabel;
    private TMP_Text matchFitBtnLabel;
    private TMP_Text matchHeaderPill;
    private TMP_Text matchStatusBanner;
    private TMP_Text matchSoundBtnLabel;
    private RectTransform handContainer;
    private Button actionButton;
    private TMP_Text actionButtonLabel;
    private Button slamButton;
    private Button emoteWheelBtn;
    private RoatanTableTalkUI tableTalkUI;
    private RoatanBlockedPipRevealOverlay pipRevealOverlay;
    private bool slamArmed;

    // Match Resilience HUD refs (10s turn timer, 5s auto-pass, Bot Auto-Playing + Resume Control)
    private GameObject humanTimerBadgeObj;
    private Image humanTimerBadgeBg;
    private TMP_Text humanTimerLabel;
    private GameObject humanBotIndicatorObj;
    private TMP_Text humanBotIndicatorLabel;
    private Button resumeControlBtn;
    private Coroutine humanTimerRoutine;

    // Opponent seat badges (Top = index 0, Upper-Left = index 1, Upper-Right = index 2)
    private readonly List<OpponentSeatUI> opponentSeats = new List<OpponentSeatUI>();
    private readonly Dictionary<int, HashSet<int>> playerPassedPips = new Dictionary<int, HashSet<int>>();
    private RectTransform humanPassedPipsRow;
    private readonly List<GameObject> humanPassedPipObjs = new List<GameObject>();

    // Summary overlay refs
    private TMP_Text summaryTitleLabel;
    private TMP_Text summaryBodyLabel;
    private Button summaryNextBtn;
    private TMP_Text summaryNextBtnLabel;
    private Button summaryNewMatchBtn;
    private TMP_Text summaryNewMatchBtnLabel;
    private TMP_Text summaryMenuBtnLabel;
    private TMP_Text rulesGotItLabel;

    // Selection & Match State
    private RoatanGameMode selectedMode = RoatanGameMode.BlockedCerrado;
    private MatchFormat selectedFormat = MatchFormat.SIX_LOV;
    private bool selectedOneOnePlayoff = false;
    private BotDifficulty selectedDifficulty = BotDifficulty.Novice;
    private int selectedBotCount = 3; // 1 (1v1), 2 (3P), or 3 (4P / 2v2)

    private RoatanMatchEngine engine;
    private RoatanBotBrain botBrain;
    private Coroutine turnRoutine;
    private DominoDto selectedHandDomino;
    private List<RoatanMatchEngine.LegalMove> currentHumanMoves = new List<RoatanMatchEngine.LegalMove>();
    private readonly List<GameObject> spawnedHandTiles = new List<GameObject>();
    private bool waitingForHumanInput;

    private class OpponentSeatUI
    {
        public GameObject Root;
        public Image Background;
        public Image TurnRing;
        public TMP_Text NameText;
        public TMP_Text CountText;
        public TMP_Text ScoreText;
        public RectTransform PassedPipsRow;
        public List<GameObject> PassedPipObjs = new List<GameObject>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoLaunch()
    {
        if (Instance != null) return;
        GameObject appObj = new GameObject("RoatanMobileApp");
        DontDestroyOnLoad(appObj);
        appObj.AddComponent<RoatanMobileApp>();
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

        if (Application.isMobilePlatform)
        {
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
        }

        RoatanLocalization.Initialize();
        LoadSavedMatchPreset();
        RoatanVisualAssets.EnsureInitialized();
        gameObject.AddComponent<RoatanSoundFX>();
        uiFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        EnsureEventSystem();
        BuildRootCanvas();
        BuildHomeScreen();
        BuildMatchSetupSheet();
        BuildUtilityDrawer();
        BuildMatchScreen();
        BuildHowToPlayModal();
        BuildRoundSummaryOverlay();

        onlineController = gameObject.AddComponent<RoatanOnlineController>();
        onlineController.Initialize(safeAreaRoot, uiFont, ShowHomeScreen, OnOnlineMatchRejoined);

        RoatanLocalization.LanguageChanged += OnLanguageChanged;
        SceneManager.activeSceneChanged += OnSceneChanged;
    }

    private void Start()
    {
        ShowHomeScreen();
        onlineController?.CheckPendingRejoinOnStartup();
    }

    private void OnOnlineMatchRejoined()
    {
        StopMatchRoutine();
        homeScreen.SetActive(false);
        if (matchSetupSheet != null) matchSetupSheet.SetActive(false);
        if (utilityDrawerOverlay != null) utilityDrawerOverlay.SetActive(false);
        matchScreen.SetActive(false);
        howToPlayModal.SetActive(false);
        roundSummaryOverlay.SetActive(false);
    }

    private void LoadSavedMatchPreset()
    {
        selectedMode = (RoatanGameMode)PlayerPrefs.GetInt(PrefPresetModeKey, (int)RoatanGameMode.BlockedCerrado);
        selectedFormat = (MatchFormat)PlayerPrefs.GetInt(PrefPresetFormatKey, (int)MatchFormat.SIX_LOV);
        selectedOneOnePlayoff = PlayerPrefs.GetInt(PrefPresetOneOneKey, 0) == 1;
        selectedDifficulty = (BotDifficulty)PlayerPrefs.GetInt(PrefPresetDiffKey, (int)BotDifficulty.Novice);
        selectedBotCount = Mathf.Clamp(PlayerPrefs.GetInt(PrefPresetBotsKey, 3), 1, 3);
    }

    private void SaveMatchPreset()
    {
        PlayerPrefs.SetInt(PrefPresetModeKey, (int)selectedMode);
        PlayerPrefs.SetInt(PrefPresetFormatKey, (int)selectedFormat);
        PlayerPrefs.SetInt(PrefPresetOneOneKey, selectedOneOnePlayoff ? 1 : 0);
        PlayerPrefs.SetInt(PrefPresetDiffKey, (int)selectedDifficulty);
        PlayerPrefs.SetInt(PrefPresetBotsKey, selectedBotCount);
        PlayerPrefs.Save();
    }

    private void CycleLanguage()
    {
        RoatanLanguage[] langs = { RoatanLanguage.English, RoatanLanguage.Spanish, RoatanLanguage.French, RoatanLanguage.German };
        int idx = System.Array.IndexOf(langs, RoatanLocalization.CurrentLanguage);
        RoatanLanguage next = langs[(idx + 1) % langs.Length];
        RoatanSoundFX.Instance?.PlayTap();
        RoatanLocalization.SetLanguage(next);
    }

    private void OnDestroy()
    {
        RoatanLocalization.LanguageChanged -= OnLanguageChanged;
        RoatanOnlineController.ServerEndpointChanged -= SyncDrawerServerEndpointFromPrefs;
        SceneManager.activeSceneChanged -= OnSceneChanged;
    }

    private void OnSceneChanged(Scene oldScene, Scene newScene)
    {
        EnsureEventSystem();
        if (rootCanvas != null)
        {
            rootCanvas.gameObject.SetActive(true);
        }
    }

    private void OnLanguageChanged()
    {
        RefreshHomeSelections();
        RefreshSoundLabels();
        RefreshStaticLabels();
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }
    }

    // ==================== NAVIGATION ====================

    private void ShowHomeScreen()
    {
        StopMatchRoutine();
        ChatManager.Instance?.SetActiveRoundChatLocked(false);
        tableTalkUI?.ClearAllBubbles();
        tableTalkUI?.HideAllOverlays();
        pipRevealOverlay?.HideImmediate();
        rootCanvas.gameObject.SetActive(true);
        onlineController?.HideOnline();
        homeScreen.SetActive(true);
        if (matchSetupSheet != null) matchSetupSheet.SetActive(false);
        if (utilityDrawerOverlay != null) utilityDrawerOverlay.SetActive(false);
        matchScreen.SetActive(false);
        howToPlayModal.SetActive(false);
        roundSummaryOverlay.SetActive(false);
        RefreshStaticLabels();
        RefreshHomeSelections();
        RefreshSoundLabels();
    }

    private void StartNewMatch()
    {
        RoatanSoundFX.Instance?.PlayTap();
        SaveMatchPreset();
        StopMatchRoutine();
        ChatManager.Instance?.SetActiveRoundChatLocked(true);
        tableTalkUI?.ClearAllBubbles();
        tableTalkUI?.HideAllOverlays();
        pipRevealOverlay?.HideImmediate();

        TableStyle tableStyle = selectedMode == RoatanGameMode.BlockedCerrado ? TableStyle.BLOCK : TableStyle.DRAW;
        int bots = selectedFormat == MatchFormat.SIX_LOV ? 3 : selectedBotCount;
        if (selectedFormat == MatchFormat.TWO_BEST && bots == 2) bots = 1; // Two-Best supports 1v1 or 2v2
        bool isTeam2v2 = selectedFormat == MatchFormat.SIX_LOV || (selectedFormat == MatchFormat.TWO_BEST && bots == 3);
        engine = new RoatanMatchEngine(
            tableStyle,
            selectedFormat,
            selectedDifficulty,
            bots,
            isTeam2v2: isTeam2v2,
            oneOnePlayoff: selectedFormat == MatchFormat.SIX_LOV && selectedOneOnePlayoff);
        botBrain = new RoatanBotBrain();

        onlineController?.HideOnline();
        homeScreen.SetActive(false);
        if (matchSetupSheet != null) matchSetupSheet.SetActive(false);
        if (utilityDrawerOverlay != null) utilityDrawerOverlay.SetActive(false);
        howToPlayModal.SetActive(false);
        roundSummaryOverlay.SetActive(false);
        matchScreen.SetActive(true);

        BeginRound();
    }

    private void BeginRound()
    {
        BeginRoundInternal(rematchAfterBreak: false);
    }

    private void BeginRematchAfterBreak()
    {
        BeginRoundInternal(rematchAfterBreak: true);
    }

    private void BeginRoundInternal(bool rematchAfterBreak)
    {
        StopMatchRoutine();
        StopHumanTurnTimer();
        roundSummaryOverlay.SetActive(false);
        tableTalkUI?.ClearAllBubbles();
        tableTalkUI?.HideAllOverlays();
        pipRevealOverlay?.HideImmediate();
        selectedHandDomino = null;
        slamArmed = false;
        if (slamButton != null) slamButton.gameObject.SetActive(false);
        if (actionButton != null) actionButton.gameObject.SetActive(false);
        waitingForHumanInput = false;
        currentHumanMoves.Clear();
        playerPassedPips.Clear();
        botBrain.ResetForRound();

        // Hide player hands and clear board while the 28-tile table shuffle plays
        foreach (GameObject go in spawnedHandTiles) Destroy(go);
        spawnedHandTiles.Clear();
        boardView.ClearBoard();
        RefreshMatchHeader();
        RefreshOpponentSeats();
        RefreshHumanPassedPips();
        RefreshHumanTakeoverIndicators();

        turnRoutine = StartCoroutine(BeginRoundWithShuffleCoroutine(rematchAfterBreak));
    }

    private IEnumerator BeginRoundWithShuffleCoroutine(bool rematchAfterBreak)
    {
        tableTalkUI?.ClearAllBubbles();
        UpdateStatusBanner("Shuffling & Mixing Tiles...");
        RoatanSoundFX.Instance?.PlayShuffleAndMix();
        float shuffleDuration = RoatanSoundFX.Instance != null
            ? RoatanSoundFX.Instance.GetShuffleClipDuration()
            : 5.0f;

        yield return StartCoroutine(boardView.PlayTableShuffleAndDealCoroutine(
            shuffleDuration,
            engine.Players.Count,
            UpdateStatusBanner));

        RoatanMatchEngine.PlacedTile opener = rematchAfterBreak
            ? engine.StartRematchAfterBreak()
            : engine.StartNewRound();
        RoatanMatchEngine.MatchPlayer starter = engine.Players.First(p => p.Id == opener.PlayerId);

        boardView.RebuildFromEngine(engine, animateLastTile: true);
        RoatanSoundFX.Instance?.PlayTilePlace();

        RefreshMatchHeader();
        RefreshOpponentSeats();
        RefreshHumanPassedPips();
        RefreshHumanTakeoverIndicators();
        RebuildHumanHand();
        UpdateStatusBanner(rematchAfterBreak
            ? $"REMATCH! Scores reset to 0 • {starter.Name} opened with [{opener.Domino.Side1}|{opener.Domino.Side2}]"
            : $"{starter.Name} opened with [{opener.Domino.Side1}|{opener.Domino.Side2}]");

        turnRoutine = StartCoroutine(DriveTurnsCoroutine());
    }

    private void OpenOnlineScreen()
    {
        RoatanSoundFX.Instance?.PlayTap();
        StopMatchRoutine();
        tableTalkUI?.ClearAllBubbles();
        tableTalkUI?.HideAllOverlays();
        homeScreen.SetActive(false);
        if (matchSetupSheet != null) matchSetupSheet.SetActive(false);
        if (utilityDrawerOverlay != null) utilityDrawerOverlay.SetActive(false);
        matchScreen.SetActive(false);
        howToPlayModal.SetActive(false);
        roundSummaryOverlay.SetActive(false);
        onlineController.ShowOnline();
    }

    // ==================== GAMEPLAY LOOP ====================

    private IEnumerator DriveTurnsCoroutine()
    {
        yield return new WaitForSeconds(0.55f);

        while (true)
        {
            if (engine.IsRoundOver(out _))
            {
                FinishCurrentRound();
                yield break;
            }

            RoatanMatchEngine.MatchPlayer active = engine.CurrentPlayer;
            RefreshMatchHeader();
            RefreshOpponentSeats();
            RefreshHumanTakeoverIndicators();

            List<RoatanMatchEngine.LegalMove> moves = engine.GetLegalMoves(active);

            if (!active.IsBot)
            {
                if (active.IsBotControlled)
                {
                    // 3-strike AFK Bot Takeover: play automatically at standard bot pacing (~1.2s),
                    // while remaining instantly interruptible if the player taps any tile or Resume Control.
                    waitingForHumanInput = false;
                    currentHumanMoves.Clear();
                    selectedHandDomino = null;
                    boardView.ClearSlots();
                    actionButton.gameObject.SetActive(false);
                    if (slamButton != null) slamButton.gameObject.SetActive(false);
                    StopHumanTurnTimer();
                    RebuildHumanHand();
                    UpdateStatusBanner("Bot Auto-Playing your turn... Tap any tile or Resume Control.");

                    float elapsed = 0f;
                    while (elapsed < RoatanMatchEngine.BotTakeoverPacingSeconds && active.IsBotControlled)
                    {
                        elapsed += Time.deltaTime;
                        yield return null;
                    }

                    if (!active.IsBotControlled)
                    {
                        // Human reclaimed control during pacing window -> hand back manual control immediately
                        continue;
                    }

                    engine.ExecuteBotControlledTurn(
                        botBrain,
                        out RoatanMatchEngine.PlacedTile botPlaced,
                        out List<DominoDto> botDrawn,
                        out bool botPassed);

                    if (botPlaced != null)
                    {
                        boardView.RebuildFromEngine(engine, animateLastTile: true);
                        RoatanSoundFX.Instance?.PlayPlace();
                        string drawNote = botDrawn.Count > 0 ? $" (drew {botDrawn.Count})" : "";
                        UpdateStatusBanner($"Bot Auto-Played [{botPlaced.Domino.Side1}|{botPlaced.Domino.Side2}]{drawNote}");
                    }
                    else if (botPassed)
                    {
                        RecordPlayerPassedOpenEnds(active.Id);
                        RoatanSoundFX.Instance?.PlayPass();
                        UpdateStatusBanner("Bot Auto-Passed (Paso!)");
                    }

                    RebuildHumanHand();
                    RefreshMatchHeader();
                    RefreshOpponentSeats();
                    yield return new WaitForSeconds(0.35f);
                    continue;
                }

                currentHumanMoves = moves;
                selectedHandDomino = null;
                boardView.ClearSlots();
                waitingForHumanInput = true;
                RebuildHumanHand();

                if (moves.Count > 0)
                {
                    actionButton.gameObject.SetActive(false);
                    UpdateStatusBanner($"Your turn! Tap a glowing domino ({moves.Count} playable)");
                    StartHumanTurnTimer(isAutoPass: false);
                }
                else if (engine.CanCurrentPlayerDraw())
                {
                    actionButton.gameObject.SetActive(true);
                    actionButton.GetComponent<Image>().color = RoatanVisualAssets.IslandGold;
                    actionButtonLabel.text = RoatanLocalization.Tf("draw_tile", engine.BoneyardCount);
                    UpdateStatusBanner("No matching tile — tap Draw Tile!");
                    StartHumanTurnTimer(isAutoPass: false);
                }
                else
                {
                    actionButton.gameObject.SetActive(true);
                    actionButton.GetComponent<Image>().color = RoatanVisualAssets.SunsetCoral;
                    actionButtonLabel.text = $"Pass ({(int)RoatanMatchEngine.AutoPassTimeoutSeconds}s)";
                    UpdateStatusBanner("No valid move — tap Pass or wait 5s for Auto-Pass (no penalty)");
                    StartHumanTurnTimer(isAutoPass: true);
                }

                yield break;
            }

            // Bot turn
            waitingForHumanInput = false;
            currentHumanMoves.Clear();
            actionButton.gameObject.SetActive(false);
            RebuildHumanHand();

            UpdateStatusBanner($"{active.Name} ({RoatanLocalization.DifficultyLabel(engine.Difficulty)}) is thinking...");
            float thinkTime = engine.Difficulty == BotDifficulty.Beginner ? 0.7f : (engine.Difficulty == BotDifficulty.Novice ? 0.85f : 0.95f);
            yield return new WaitForSeconds(thinkTime);

            if (moves.Count > 0)
            {
                RoatanMatchEngine.LegalMove chosen = botBrain.ChooseMove(engine, active, moves, engine.Difficulty);
                RoatanMatchEngine.PlacedTile placed = engine.PlayMove(chosen);
                if (placed != null)
                {
                    bool botDominoWin = active.Hand.Count == 0;
                    bool forcesNextPass = !botDominoWin && engine.GetLegalMoves(engine.CurrentPlayer).Count == 0 && !engine.CanCurrentPlayerDraw();
                    bool botSlam = botDominoWin || (forcesNextPass && engine.Difficulty == BotDifficulty.Pro);

                    boardView.RebuildFromEngine(engine, animateLastTile: true, slammedLastTile: botSlam);
                    if (botSlam)
                    {
                        RoatanSoundFX.Instance?.PlaySlam(heavy: true);
                        boardView.PlaySlamImpact(boardView.LastPlacedTilePos, highImpact: true);
                        ShowBotEmoteBubble(active, botDominoWin ? "Game Bruk!" : "Slam it!");
                        UpdateStatusBanner($"{active.Name} SLAMMED [{placed.Domino.Side1}|{placed.Domino.Side2}]!");
                    }
                    else
                    {
                        RoatanSoundFX.Instance?.PlayPlace();
                        UpdateStatusBanner($"{active.Name} played [{placed.Domino.Side1}|{placed.Domino.Side2}]");
                    }
                }
            }
            else if (engine.CanCurrentPlayerDraw())
            {
                botBrain.RecordBlocked(active.Id, engine.LeftEndPip, engine.RightEndPip);
                DominoDto drawn = engine.DrawFromBoneyard();
                if (drawn != null)
                {
                    botBrain.ClearVoidsAfterDraw(active.Id);
                    RoatanSoundFX.Instance?.PlayDraw();
                    UpdateStatusBanner($"{active.Name} drew from the boneyard");
                }
                List<RoatanMatchEngine.LegalMove> afterDraw = engine.GetLegalMoves(active);
                if (afterDraw.Count == 0 && !engine.CanCurrentPlayerDraw())
                {
                    RecordPlayerPassedOpenEnds(active.Id);
                    engine.PassTurn();
                    RoatanSoundFX.Instance?.PlayPass();
                    ShowBotEmoteBubble(active, "Pass!");
                    UpdateStatusBanner($"{active.Name} passed (Paso!)");
                }
            }
            else
            {
                botBrain.RecordBlocked(active.Id, engine.LeftEndPip, engine.RightEndPip);
                RecordPlayerPassedOpenEnds(active.Id);
                engine.PassTurn();
                RoatanSoundFX.Instance?.PlayPass();
                ShowBotEmoteBubble(active, "Pass!");
                UpdateStatusBanner($"{active.Name} passed (Paso!)");
            }

            RefreshMatchHeader();
            RefreshOpponentSeats();
            yield return new WaitForSeconds(0.35f);
        }
    }

    private void OnHumanHandTileClicked(DominoDto domino)
    {
        if (engine == null) return;

        // Instant Manual Reclaim: tapping ANY tile in hand while Bot Auto-Playing immediately reclaims control
        if (engine.Players.Count > 0 && engine.Players[0].IsBotControlled)
        {
            ReclaimHumanControl();
            if (!waitingForHumanInput) return;
        }

        if (!waitingForHumanInput) return;

        List<RoatanMatchEngine.LegalMove> tileMoves = currentHumanMoves
            .Where(m => (m.Domino.Side1 == domino.Side1 && m.Domino.Side2 == domino.Side2) ||
                        (m.Domino.Side1 == domino.Side2 && m.Domino.Side2 == domino.Side1))
            .ToList();

        if (tileMoves.Count == 0)
        {
            RoatanSoundFX.Instance?.PlayPass();
            UpdateStatusBanner($"[{domino.Side1}|{domino.Side2}] doesn't match ends {engine.LeftEndPip} or {engine.RightEndPip}");
            return;
        }

        RoatanSoundFX.Instance?.PlayTap();

        bool alreadySelected = selectedHandDomino != null &&
                               selectedHandDomino.Side1 == domino.Side1 &&
                               selectedHandDomino.Side2 == domino.Side2;

        if (tileMoves.Count == 1 && alreadySelected)
        {
            ExecuteHumanMove(tileMoves[0], isSlam: slamArmed);
            return;
        }

        selectedHandDomino = domino;
        slamArmed = false;
        if (slamButton != null) slamButton.gameObject.SetActive(true);
        RebuildHumanHand();

        bool canLeft = tileMoves.Any(m => m.End == BoardEnd.Left);
        bool canRight = tileMoves.Any(m => m.End == BoardEnd.Right);
        boardView.ShowEndSlots(canLeft, canRight);

        UpdateStatusBanner(tileMoves.Count == 1
            ? "Tap glowing slot, tap again to place — or flick down / tap SLAM!"
            : "Choose a glowing end (or tap SLAM! then pick an end)");
    }

    private void OnHumanTileSlamFlicked(DominoDto domino)
    {
        if (!waitingForHumanInput || engine == null || domino == null) return;

        List<RoatanMatchEngine.LegalMove> tileMoves = currentHumanMoves
            .Where(m => (m.Domino.Side1 == domino.Side1 && m.Domino.Side2 == domino.Side2) ||
                        (m.Domino.Side1 == domino.Side2 && m.Domino.Side2 == domino.Side1))
            .ToList();

        if (tileMoves.Count == 0) return;

        if (tileMoves.Count == 1)
        {
            ExecuteHumanMove(tileMoves[0], isSlam: true);
            return;
        }

        // Matches both ends: arm slam and highlight slots so user picks which end to slam onto
        selectedHandDomino = domino;
        slamArmed = true;
        if (slamButton != null) slamButton.gameObject.SetActive(true);
        RebuildHumanHand();
        boardView.ShowEndSlots(true, true);
        UpdateStatusBanner("SLAM ARMED! Tap either glowing end to SLAM!");
    }

    private void OnSlamButtonClicked()
    {
        if (!waitingForHumanInput || selectedHandDomino == null || engine == null) return;

        List<RoatanMatchEngine.LegalMove> tileMoves = currentHumanMoves
            .Where(m => (m.Domino.Side1 == selectedHandDomino.Side1 && m.Domino.Side2 == selectedHandDomino.Side2) ||
                        (m.Domino.Side1 == selectedHandDomino.Side2 && m.Domino.Side2 == selectedHandDomino.Side1))
            .ToList();

        if (tileMoves.Count == 0) return;

        if (tileMoves.Count == 1 || slamArmed)
        {
            ExecuteHumanMove(tileMoves[0], isSlam: true);
        }
        else
        {
            slamArmed = true;
            RoatanSoundFX.Instance?.PlayTap();
            UpdateStatusBanner("SLAM ARMED! Tap a glowing end (or tap SLAM again)!");
        }
    }

    private void OnBoardEndSlotClicked(BoardEnd end)
    {
        if (!waitingForHumanInput || selectedHandDomino == null) return;

        RoatanMatchEngine.LegalMove chosen = currentHumanMoves.FirstOrDefault(m =>
            m.End == end &&
            ((m.Domino.Side1 == selectedHandDomino.Side1 && m.Domino.Side2 == selectedHandDomino.Side2) ||
             (m.Domino.Side1 == selectedHandDomino.Side2 && m.Domino.Side2 == selectedHandDomino.Side1)));

        if (chosen != null)
        {
            ExecuteHumanMove(chosen, isSlam: slamArmed);
        }
    }

    private void ExecuteHumanMove(RoatanMatchEngine.LegalMove move, bool isSlam = false)
    {
        StopHumanTurnTimer();
        waitingForHumanInput = false;
        selectedHandDomino = null;
        slamArmed = false;
        currentHumanMoves.Clear();
        boardView.ClearSlots();
        actionButton.gameObject.SetActive(false);
        if (slamButton != null) slamButton.gameObject.SetActive(false);

        // Manual move resets consecutiveAutoPlays = 0
        engine.RecordManualAction(0);
        RefreshHumanTakeoverIndicators();

        RoatanMatchEngine.PlacedTile placed = engine.PlayMove(move);
        if (placed != null)
        {
            bool isDominoWin = engine.Players[0].Hand.Count == 0;
            bool forcesNextPass = !isDominoWin && engine.GetLegalMoves(engine.CurrentPlayer).Count == 0 && !engine.CanCurrentPlayerDraw();
            bool highImpact = isDominoWin || forcesNextPass;

            boardView.RebuildFromEngine(engine, animateLastTile: true, slammedLastTile: isSlam);
            if (isSlam)
            {
                RoatanSoundFX.Instance?.PlaySlam(heavy: highImpact);
                boardView.PlaySlamImpact(boardView.LastPlacedTilePos, highImpact);
                UpdateStatusBanner(isDominoWin
                    ? $"DOMINO SLAM!! [{placed.Domino.Side1}|{placed.Domino.Side2}]"
                    : (forcesNextPass
                        ? $"HEAVY SLAM [{placed.Domino.Side1}|{placed.Domino.Side2}] — {engine.CurrentPlayer.Name} forced to PASS!"
                        : $"SLAMMED [{placed.Domino.Side1}|{placed.Domino.Side2}]!"));
            }
            else
            {
                RoatanSoundFX.Instance?.PlayPlace();
                UpdateStatusBanner($"You played [{placed.Domino.Side1}|{placed.Domino.Side2}]");
            }
        }

        RebuildHumanHand();
        RefreshMatchHeader();
        RefreshOpponentSeats();

        turnRoutine = StartCoroutine(DriveTurnsCoroutine());
    }

    private void OnActionButtonClicked()
    {
        if (!waitingForHumanInput || engine == null) return;

        RoatanMatchEngine.MatchPlayer human = engine.CurrentPlayer;
        if (human.IsBot) return;

        if (engine.CanCurrentPlayerDraw())
        {
            engine.RecordManualAction(human.Id);
            RefreshHumanTakeoverIndicators();
            botBrain.RecordBlocked(human.Id, engine.LeftEndPip, engine.RightEndPip);
            DominoDto drawn = engine.DrawFromBoneyard();
            if (drawn != null)
            {
                botBrain.ClearVoidsAfterDraw(human.Id);
                RoatanSoundFX.Instance?.PlayDraw();
                UpdateStatusBanner($"Drew [{drawn.Side1}|{drawn.Side2}] from boneyard");
            }

            currentHumanMoves = engine.GetLegalMoves(human);
            RebuildHumanHand();
            RefreshMatchHeader();

            if (currentHumanMoves.Count > 0)
            {
                actionButton.gameObject.SetActive(false);
                UpdateStatusBanner($"Nice draw! Tap [{drawn.Side1}|{drawn.Side2}] to play!");
                StartHumanTurnTimer(isAutoPass: false);
            }
            else if (engine.CanCurrentPlayerDraw())
            {
                actionButtonLabel.text = RoatanLocalization.Tf("draw_tile", engine.BoneyardCount);
                StartHumanTurnTimer(isAutoPass: false);
            }
            else
            {
                actionButton.GetComponent<Image>().color = RoatanVisualAssets.SunsetCoral;
                actionButtonLabel.text = $"Pass ({(int)RoatanMatchEngine.AutoPassTimeoutSeconds}s)";
                UpdateStatusBanner("Boneyard empty — tap Pass or wait 5s for Auto-Pass");
                StartHumanTurnTimer(isAutoPass: true);
            }
        }
        else
        {
            StopHumanTurnTimer();
            botBrain.RecordBlocked(human.Id, engine.LeftEndPip, engine.RightEndPip);
            RecordPlayerPassedOpenEnds(human.Id);
            waitingForHumanInput = false;
            actionButton.gameObject.SetActive(false);
            engine.PassTurn();
            RoatanSoundFX.Instance?.PlayPass();
            UpdateStatusBanner("You passed (Paso!)");
            turnRoutine = StartCoroutine(DriveTurnsCoroutine());
        }
    }

    private void FinishCurrentRound()
    {
        StopHumanTurnTimer();
        waitingForHumanInput = false;
        slamArmed = false;
        actionButton.gameObject.SetActive(false);
        if (slamButton != null) slamButton.gameObject.SetActive(false);
        boardView.ClearSlots();

        turnRoutine = StartCoroutine(FinishCurrentRoundCoroutine());
    }

    private IEnumerator FinishCurrentRoundCoroutine()
    {
        RoatanMatchEngine.RoundResult result = engine.CompleteRound();

        // 1. Blocked-Board Pip Reveal Overlay (flips all remaining hands, counts pips, holds 3.5s)
        if (result.WasBlocked && pipRevealOverlay != null)
        {
            yield return StartCoroutine(pipRevealOverlay.ShowRevealCoroutine(engine, result));
        }

        // 2. Major state event celebratory banners + audio stings
        if (result.BreakTriggered)
        {
            RoatanSoundFX.Instance?.PlayBreakSting();
            boardView?.TriggerBoardShake(18f, 0.35f);
            string keyBreakNote = result.IsKeyWin ? " (by Key / Capicú!)" : "";
            tableTalkUI?.ShowCelebrationBanner(
                "GAME BRUK! (BREAK)",
                $"{result.Winner?.Name} broke the streak{keyBreakNote}! Scores reset to 0 — [6|6] poses next",
                new Color(0.56f, 0.16f, 0.14f, 0.96f),
                2.2f);
            yield return new WaitForSeconds(1.8f);
        }
        else if (result.MatchOver && engine.MatchFormat == MatchFormat.SIX_LOV && result.IsKeyWin)
        {
            RoatanSoundFX.Instance?.PlayKeySting();
            boardView?.TriggerBoardShake(22f, 0.42f);
            tableTalkUI?.ShowCelebrationBanner(
                "SIX-LOV BY KEY! (CAPICÚ!)",
                $"{result.Winner?.Name} sealed the 6–0 sweep with a Key (+2 wins)!",
                new Color(0.58f, 0.38f, 0.06f, 0.96f),
                2.8f);
            yield return new WaitForSeconds(2.0f);
        }
        else if (result.IsKeyWin)
        {
            RoatanSoundFX.Instance?.PlayKeySting();
            boardView?.TriggerBoardShake(18f, 0.36f);
            tableTalkUI?.ShowCelebrationBanner(
                "KEY! / CAPICÚ! (+2 WINS)",
                $"{result.Winner?.Name} exhausted both open suits on the final tile!",
                new Color(0.58f, 0.38f, 0.06f, 0.96f),
                2.6f);
            yield return new WaitForSeconds(1.9f);
        }
        else if (result.MatchOver && engine.MatchFormat == MatchFormat.SIX_LOV)
        {
            RoatanSoundFX.Instance?.PlaySixLovSting();
            tableTalkUI?.ShowCelebrationBanner(
                "SIX-LOV! (6–0)",
                "Clean sweep — Six consecutive hand wins!",
                new Color(0.08f, 0.44f, 0.30f, 0.96f),
                2.6f);
            yield return new WaitForSeconds(1.8f);
        }
        else if (result.MatchOver && engine.MatchFormat == MatchFormat.CUT_THROAT && result.CutThroatedPlayers.Count > 0)
        {
            RoatanSoundFX.Instance?.PlayCutThroatSting();
            string victims = string.Join(", ", result.CutThroatedPlayers.Select(p => p.Name));
            tableTalkUI?.ShowCelebrationBanner(
                "CUT-THROAT!",
                $"{result.MatchWinner?.Name} hit 6 wins • Cut-Throated at 0: {victims}",
                new Color(0.54f, 0.12f, 0.18f, 0.96f),
                2.6f);
            yield return new WaitForSeconds(1.8f);
        }

        // 3. "Call Your Padna" (CHOOSE_POSER) state handoff before summary/next hand
        if (result.RequiresChoosePoser && tableTalkUI != null)
        {
            if (result.PoserChooserIndex == 0 && result.PartnerCandidateIndex >= 0 && result.PartnerCandidateIndex < engine.Players.Count)
            {
                bool choiceDone = false;
                string partnerName = engine.Players[result.PartnerCandidateIndex].Name;
                tableTalkUI.ShowChoosePoserPrompt(partnerName, RoatanMatchEngine.ChoosePoserTimeoutSeconds, choice =>
                {
                    engine.ConfirmPoserChoice(0, choice);
                    choiceDone = true;
                });
                while (!choiceDone)
                {
                    yield return null;
                }
            }
            else if (result.PoserChooserIndex >= 0 && result.PoserChooserIndex < engine.Players.Count)
            {
                engine.ConfirmPoserChoice(engine.Players[result.PoserChooserIndex].Id, PoserChoice.POSE_MYSELF);
            }
        }

        ShowRoundSummaryCard(result);
    }

    private void ShowRoundSummaryCard(RoatanMatchEngine.RoundResult result)
    {
        RoatanMatchEngine.MatchPlayer champion = null;
        bool hasChampion = !result.IsDraw && engine.HasMatchChampion(out champion);

        if (!result.IsDraw && result.Winner != null && result.Winner.Id == 0)
        {
            RoatanSoundFX.Instance?.PlayWin();
        }
        else if (!result.BreakTriggered)
        {
            RoatanSoundFX.Instance?.PlayPass();
        }

        if (hasChampion)
        {
            int games = PlayerPrefs.GetInt(PrefGamesKey, 0) + 1;
            int wins = PlayerPrefs.GetInt(PrefWinsKey, 0) + (champion.Id == 0 ? 1 : 0);
            PlayerPrefs.SetInt(PrefGamesKey, games);
            PlayerPrefs.SetInt(PrefWinsKey, wins);
            PlayerPrefs.Save();
        }

        RefreshOpponentSeats();

        string reason = result.WasBlocked ? RoatanLocalization.T("blocked_reason") : RoatanLocalization.T("domino_reason");
        if (result.IsDraw)
        {
            summaryTitleLabel.text = $"ROUND {engine.RoundNumber} — DRAW (TIE)!";
        }
        else if (result.BreakTriggered)
        {
            summaryTitleLabel.text = "GAME BRUK! (BREAK)";
        }
        else if (hasChampion && result.IsKeyWin && engine.MatchFormat == MatchFormat.SIX_LOV)
        {
            summaryTitleLabel.text = "SIX-LOV BY KEY! (CAPICÚ!)";
        }
        else if (result.IsKeyWin && !hasChampion)
        {
            summaryTitleLabel.text = "KEY! / CAPICÚ! (+2 WINS)";
        }
        else
        {
            summaryTitleLabel.text = hasChampion
                ? (champion.Id == 0 ? "MATCH CHAMPION!" : $"{champion.Name} WINS MATCH")
                : (result.Winner.Id == 0 ? $"YOU WON ROUND {engine.RoundNumber}!" : $"{result.Winner.Name} WON ROUND {engine.RoundNumber}");
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        if (result.IsDraw)
        {
            string samePoserName = (result.NextPoserIndex >= 0 && result.NextPoserIndex < engine.Players.Count)
                ? engine.Players[result.NextPoserIndex].Name
                : "Poser";
            sb.AppendLine($"{reason}   •   0 pts awarded (Tie)   •   {samePoserName} poses next");
        }
        else if (result.BreakTriggered)
        {
            sb.AppendLine($"{reason}   •   {result.Winner.Name} broke the streak! Scores reset to 0 ([6|6] poses next)");
        }
        else
        {
            if (result.IsKeyWin)
            {
                sb.AppendLine($"Won by KEY / CAPICÚ (+{result.PointsAwarded}) — {result.Winner.Name}");
            }
            else
            {
                sb.AppendLine($"{reason}   •   +{result.PointsAwarded} hand win to {result.Winner.Name}");
            }
            if (result.CutThroatedPlayers != null && result.CutThroatedPlayers.Count > 0)
            {
                string victims = string.Join(", ", result.CutThroatedPlayers.Select(p => p.Name));
                sb.AppendLine($"Cut-Throated at 0: {victims}");
            }
        }
        sb.AppendLine();
        foreach (RoatanMatchEngine.MatchPlayer p in result.Standings)
        {
            string tag = (!result.IsDraw && result.Winner != null && p.Id == result.Winner.Id) ? "* " : "  ";
            sb.AppendLine($"{tag}{p.Name,-10}   Hand: {p.HandPipSum(),2} pips ({p.Hand.Count} tiles)   |   Wins: {p.MatchScore}/{engine.TargetMatchScore}");
        }

        summaryBodyLabel.text = sb.ToString();

        // Post-Hand UI Options:
        // - Break (Game Bruk): NO "Next Round" -> show Rematch, New Match, Leave
        // - Match Champion: show Rematch, New Match, Leave
        // - Normal non-Break hand end: show Next Round and Leave ONLY
        bool showRematchLayout = result.BreakTriggered || hasChampion;

        summaryNextBtn.onClick.RemoveAllListeners();
        if (showRematchLayout)
        {
            summaryNextBtnLabel.text = "Rematch";
            summaryNextBtn.GetComponent<Image>().color = RoatanVisualAssets.PalmGreen;
            summaryNextBtn.onClick.AddListener(() =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                BeginRematchAfterBreak();
            });
            if (summaryNewMatchBtn != null)
            {
                summaryNewMatchBtn.gameObject.SetActive(true);
            }
        }
        else
        {
            summaryNextBtnLabel.text = RoatanLocalization.Tf("next_round", engine.RoundNumber + 1);
            summaryNextBtn.GetComponent<Image>().color = RoatanVisualAssets.PalmGreen;
            summaryNextBtn.onClick.AddListener(() =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                BeginRound();
            });
            if (summaryNewMatchBtn != null)
            {
                summaryNewMatchBtn.gameObject.SetActive(false);
            }
        }

        if (summaryMenuBtnLabel != null)
        {
            summaryMenuBtnLabel.text = "Leave";
        }

        roundSummaryOverlay.SetActive(true);
    }

    // ==================== RESILIENCE TIMERS & BOT TAKEOVER ====================

    private void StartHumanTurnTimer(bool isAutoPass)
    {
        StopHumanTurnTimer();
        humanTimerRoutine = StartCoroutine(HumanTurnTimerCoroutine(isAutoPass));
    }

    private void StopHumanTurnTimer()
    {
        if (humanTimerRoutine != null)
        {
            StopCoroutine(humanTimerRoutine);
            humanTimerRoutine = null;
        }
        if (humanTimerBadgeObj != null)
        {
            humanTimerBadgeObj.SetActive(false);
        }
    }

    private IEnumerator HumanTurnTimerCoroutine(bool isAutoPass)
    {
        float remaining = isAutoPass
            ? RoatanMatchEngine.AutoPassTimeoutSeconds
            : RoatanMatchEngine.TurnTimeoutSeconds;

        if (humanTimerBadgeObj != null) humanTimerBadgeObj.SetActive(true);

        while (remaining > 0f && waitingForHumanInput && engine != null && !engine.CurrentPlayer.IsBot && !engine.CurrentPlayer.IsBotControlled)
        {
            int displaySec = Mathf.CeilToInt(remaining);
            if (isAutoPass)
            {
                if (actionButtonLabel != null) actionButtonLabel.text = $"Pass ({displaySec}s)";
                if (humanTimerLabel != null) humanTimerLabel.text = $"AUTO-PASS {displaySec}s";
                if (humanTimerBadgeBg != null) humanTimerBadgeBg.color = RoatanVisualAssets.SunsetCoral;
            }
            else
            {
                if (humanTimerLabel != null) humanTimerLabel.text = $"TURN TIMER: {displaySec}s";
                if (humanTimerBadgeBg != null)
                {
                    humanTimerBadgeBg.color = remaining <= 3f
                        ? RoatanVisualAssets.SunsetCoral
                        : (remaining <= 5f ? RoatanVisualAssets.AmberHighlight : new Color(0.08f, 0.34f, 0.38f, 0.96f));
                }
            }

            remaining -= Time.deltaTime;
            yield return null;
        }

        if (!waitingForHumanInput || engine == null || engine.CurrentPlayer.IsBot || engine.CurrentPlayer.IsBotControlled)
        {
            if (humanTimerBadgeObj != null) humanTimerBadgeObj.SetActive(false);
            humanTimerRoutine = null;
            yield break;
        }

        // Timer expired (0s)
        waitingForHumanInput = false;
        selectedHandDomino = null;
        slamArmed = false;
        currentHumanMoves.Clear();
        boardView.ClearSlots();
        actionButton.gameObject.SetActive(false);
        if (slamButton != null) slamButton.gameObject.SetActive(false);
        if (humanTimerBadgeObj != null) humanTimerBadgeObj.SetActive(false);
        humanTimerRoutine = null;

        if (isAutoPass)
        {
            // 5s Auto-Pass: MUST NOT increment AFK counter
            botBrain.RecordBlocked(0, engine.LeftEndPip, engine.RightEndPip);
            RecordPlayerPassedOpenEnds(0);
            engine.ExecuteAutoPassNoPenalty();
            RoatanSoundFX.Instance?.PlayPass();
            UpdateStatusBanner("Auto-passed (0s) — no AFK strike");
        }
        else
        {
            // 10s Turn Timeout: Auto-draw (if needed) + play via RoatanBotBrain + increment AFK counter (+1)
            RoatanMatchEngine.MatchPlayer human = engine.Players[0];
            engine.ExecuteTimeoutAutoPlay(
                botBrain,
                out RoatanMatchEngine.PlacedTile autoPlaced,
                out List<DominoDto> autoDrawn,
                out bool autoPassed,
                out bool triggeredBotTakeover);

            if (autoPlaced != null)
            {
                boardView.RebuildFromEngine(engine, animateLastTile: true);
                RoatanSoundFX.Instance?.PlayPlace();
                string drawNote = autoDrawn.Count > 0 ? $" (drew {autoDrawn.Count})" : "";
                UpdateStatusBanner(triggeredBotTakeover
                    ? $"3 AFK Timeouts — Bot Auto-Playing! Tap any tile or Resume Control."
                    : $"Time's up! Auto-played [{autoPlaced.Domino.Side1}|{autoPlaced.Domino.Side2}]{drawNote} (AFK {human.ConsecutiveAutoPlays}/{RoatanMatchEngine.BotTakeoverStrikeLimit})");
            }
            else if (autoPassed)
            {
                RecordPlayerPassedOpenEnds(0);
                RoatanSoundFX.Instance?.PlayPass();
                UpdateStatusBanner(triggeredBotTakeover
                    ? "3 AFK Timeouts — Bot Auto-Playing! Tap any tile or Resume Control."
                    : $"Time's up! Auto-passed after drawing (AFK {human.ConsecutiveAutoPlays}/{RoatanMatchEngine.BotTakeoverStrikeLimit})");
            }
        }

        RefreshHumanTakeoverIndicators();
        RebuildHumanHand();
        RefreshMatchHeader();
        RefreshOpponentSeats();
        turnRoutine = StartCoroutine(DriveTurnsCoroutine());
    }

    private void ReclaimHumanControl()
    {
        if (engine == null || engine.Players.Count == 0) return;
        if (!engine.Players[0].IsBotControlled) return;

        engine.ReclaimManualControl(0);
        RoatanSoundFX.Instance?.PlayTap();
        RefreshHumanTakeoverIndicators();
        RebuildHumanHand();
        UpdateStatusBanner("Control resumed! You're back in full manual control.");
    }

    private void RefreshHumanTakeoverIndicators()
    {
        bool botActive = engine != null && engine.Players.Count > 0 && engine.Players[0].IsBotControlled;
        if (humanBotIndicatorObj != null)
        {
            humanBotIndicatorObj.SetActive(botActive);
        }
        if (resumeControlBtn != null)
        {
            resumeControlBtn.gameObject.SetActive(botActive);
        }
    }

    private void ShowBotEmoteBubble(RoatanMatchEngine.MatchPlayer bot, string phrase)
    {
        if (tableTalkUI == null || engine == null || bot == null) return;
        List<RoatanMatchEngine.MatchPlayer> bots = engine.Players.Where(p => p.IsBot).ToList();
        int idx = bots.FindIndex(b => b.Id == bot.Id);
        if (idx < 0) return;

        int[] seatMap = bots.Count == 1 ? new[] { 0 }
                      : bots.Count == 2 ? new[] { 1, 2 }
                      : new[] { 1, 0, 2 };
        int seatSlot = idx < seatMap.Length ? seatMap[idx] : 0;

        Vector2 bubblePos = seatSlot == 0 ? new Vector2(0f, 560f)
                          : seatSlot == 1 ? new Vector2(-280f, 445f)
                          : new Vector2(280f, 445f);

        tableTalkUI.ShowSpeechBubble(seatSlot, bot.Name, phrase, bubblePos);
    }

    private void RecordPlayerPassedOpenEnds(int playerId)
    {
        if (engine == null || !engine.HasBoardStarted) return;

        if (!playerPassedPips.TryGetValue(playerId, out HashSet<int> set))
        {
            set = new HashSet<int>();
            playerPassedPips[playerId] = set;
        }

        if (engine.LeftEndPip >= 0 && engine.LeftEndPip <= 6) set.Add(engine.LeftEndPip);
        if (engine.RightEndPip >= 0 && engine.RightEndPip <= 6) set.Add(engine.RightEndPip);

        if (playerId == 0)
        {
            RefreshHumanPassedPips();
        }
        else
        {
            RefreshOpponentSeats();
        }
    }

    private void RefreshHumanPassedPips()
    {
        foreach (GameObject go in humanPassedPipObjs) Destroy(go);
        humanPassedPipObjs.Clear();

        if (humanPassedPipsRow == null) return;
        if (!playerPassedPips.TryGetValue(0, out HashSet<int> passed) || passed.Count == 0)
        {
            humanPassedPipsRow.gameObject.SetActive(false);
            return;
        }

        humanPassedPipsRow.gameObject.SetActive(true);
        foreach (int pip in passed.OrderBy(p => p))
        {
            humanPassedPipObjs.Add(CreateMutedPipIcon(humanPassedPipsRow, pip));
        }
    }

    private GameObject CreateMutedPipIcon(Transform parent, int pip)
    {
        GameObject chip = new GameObject($"PassedPip_{pip}");
        chip.transform.SetParent(parent, false);
        RectTransform rect = chip.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(26f, 26f);
        LayoutElement le = chip.AddComponent<LayoutElement>();
        le.preferredWidth = 26f;
        le.preferredHeight = 26f;

        Image bg = chip.AddComponent<Image>();
        bg.sprite = RoatanVisualAssets.RoundedCardSprite;
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.11f, 0.21f, 0.26f, 0.86f);
        bg.raycastTarget = false;

        // Draw authentic muted mini pip dots (0 = hollow subtle dash, 1..6 = classic domino dot layout)
        Vector2[] dots = GetMiniPipDotOffsets(pip);
        if (dots.Length == 0)
        {
            TMP_Text zeroLbl = CreateText(chip.transform, "Zero", "0", 14, FontStyles.Bold, new Color(0.60f, 0.72f, 0.76f, 0.80f), Vector2.zero, new Vector2(22f, 22f));
            zeroLbl.raycastTarget = false;
        }
        else
        {
            Color dotColor = new Color(0.68f, 0.79f, 0.83f, 0.85f);
            for (int i = 0; i < dots.Length; i++)
            {
                GameObject dot = new GameObject("Dot");
                dot.transform.SetParent(chip.transform, false);
                RectTransform dr = dot.AddComponent<RectTransform>();
                dr.sizeDelta = new Vector2(5f, 5f);
                dr.anchoredPosition = dots[i];
                Image di = dot.AddComponent<Image>();
                di.sprite = RoatanVisualAssets.CircleSprite;
                di.color = dotColor;
                di.raycastTarget = false;
            }
        }

        return chip;
    }

    private static Vector2[] GetMiniPipDotOffsets(int pip)
    {
        const float o = 6.2f;
        switch (pip)
        {
            case 1: return new[] { Vector2.zero };
            case 2: return new[] { new Vector2(-o, o), new Vector2(o, -o) };
            case 3: return new[] { new Vector2(-o, o), Vector2.zero, new Vector2(o, -o) };
            case 4: return new[] { new Vector2(-o, o), new Vector2(o, o), new Vector2(-o, -o), new Vector2(o, -o) };
            case 5: return new[] { new Vector2(-o, o), new Vector2(o, o), Vector2.zero, new Vector2(-o, -o), new Vector2(o, -o) };
            case 6: return new[] { new Vector2(-o, o), new Vector2(o, o), new Vector2(-o, 0f), new Vector2(o, 0f), new Vector2(-o, -o), new Vector2(o, -o) };
            default: return new Vector2[0];
        }
    }

    private void StopMatchRoutine()
    {
        StopHumanTurnTimer();
        boardView?.CancelShuffle();
        RoatanSoundFX.Instance?.StopShuffleAndMix();
        if (turnRoutine != null)
        {
            StopCoroutine(turnRoutine);
            turnRoutine = null;
        }
    }

    // ==================== UI REFRESH HELPERS ====================

    private void RefreshStaticLabels()
    {
        if (subtitleLabel != null) subtitleLabel.text = RoatanLocalization.T("subtitle");
        if (subtitleShadowLabel != null) subtitleShadowLabel.text = RoatanLocalization.T("subtitle");
        if (topLangPillLabel != null) topLangPillLabel.text = $"{RoatanLocalization.CodeFor(RoatanLocalization.CurrentLanguage)} v";
        if (modeHeadingLabel != null) modeHeadingLabel.text = "TABLE STYLE";
        if (cerradoTitleLabel != null) cerradoTitleLabel.text = "Roatan Blocked";
        if (cerradoDescLabel != null) cerradoDescLabel.text = "No drawing • Pure strategy";
        if (drawTitleLabel != null) drawTitleLabel.text = "Classic Draw";
        if (drawDescLabel != null) drawDescLabel.text = "Draw from boneyard";
        if (diffHeadingLabel != null) diffHeadingLabel.text = RoatanLocalization.T("bot_difficulty");
        if (playNowBtnLabel != null) playNowBtnLabel.text = "PLAY OFFLINE";
        if (howToPlayBtnLabel != null) howToPlayBtnLabel.text = RoatanLocalization.T("how_to_play");
        if (onlineModeBtnLabel != null) onlineModeBtnLabel.text = "PLAY ONLINE";
        if (matchBackBtnLabel != null) matchBackBtnLabel.text = RoatanLocalization.T("menu_back");
        if (matchFitBtnLabel != null) matchFitBtnLabel.text = RoatanLocalization.T("fit");
        if (summaryMenuBtnLabel != null) summaryMenuBtnLabel.text = "Leave";
        if (rulesGotItLabel != null) rulesGotItLabel.text = RoatanLocalization.T("got_it");

        foreach (KeyValuePair<BotDifficulty, TMP_Text> kv in diffBtnLabels)
        {
            kv.Value.text = RoatanLocalization.DifficultyLabel(kv.Key);
        }
    }

    private void RefreshHomeSelections()
    {
        if (topLangPillLabel != null)
        {
            topLangPillLabel.text = $"{RoatanLocalization.CodeFor(RoatanLocalization.CurrentLanguage)} v";
        }

        foreach (KeyValuePair<RoatanGameMode, Image> kv in modeCardImages)
        {
            bool active = kv.Key == selectedMode;
            kv.Value.color = active ? new Color(0.11f, 0.56f, 0.58f, 1f) : new Color(0.08f, 0.21f, 0.27f, 0.92f);
        }

        foreach (KeyValuePair<MatchFormat, Image> kv in formatBtnImages)
        {
            bool active = kv.Key == selectedFormat;
            kv.Value.color = active ? RoatanVisualAssets.IslandGold : new Color(0.14f, 0.22f, 0.28f, 1f);
            TMP_Text lbl = kv.Value.GetComponentInChildren<TMP_Text>();
            if (lbl != null)
            {
                lbl.color = active ? new Color(0.08f, 0.10f, 0.12f, 1f) : Color.white;
            }
        }

        string tooltipRule;
        switch (selectedFormat)
        {
            case MatchFormat.SIX_LOV:
                tooltipRule = selectedOneOnePlayoff
                    ? "Six-Lov (2v2): First pair to 6-0 wins! 1-1 Playoff ON: trailing win at 1-0 sets 1-1; winner of 1-1 jumps to 2-0 (loss from 2-0+ triggers Game Bruk)."
                    : "Six-Lov (2v2): First pair to 6 consecutive hand wins (6-0) wins! Any trailing win triggers Game Bruk (scores reset to 0-0, [6|6] poses next).";
                break;
            case MatchFormat.TWO_BEST:
                tooltipRule = "Two-Best (Best of 3): First player or 2v2 pair to win 2 hands wins the match. No Break rule (1-1 plays a deciding 3rd hand).";
                break;
            default:
                tooltipRule = "Cut-Throat (FFA): Individual race to 6 wins! Wins needn't be consecutive while someone sits at 0. Once every player has >= 1 win, Game Bruk resets all to 0.";
                break;
        }
        if (formatDescLabel != null) formatDescLabel.text = tooltipRule;
        if (setupRuleTooltipText != null) setupRuleTooltipText.text = tooltipRule;

        if (oneOnePlayoffBtn != null)
        {
            oneOnePlayoffBtn.gameObject.SetActive(selectedFormat == MatchFormat.SIX_LOV);
            if (oneOnePlayoffBtnImg != null)
            {
                oneOnePlayoffBtnImg.color = selectedOneOnePlayoff ? RoatanVisualAssets.PalmGreen : new Color(0.14f, 0.22f, 0.28f, 1f);
            }
            if (oneOnePlayoffBtnLabel != null)
            {
                oneOnePlayoffBtnLabel.text = selectedOneOnePlayoff ? "1-1 Playoff Rule: ON" : "1-1 Playoff Rule: OFF";
            }
        }

        foreach (KeyValuePair<BotDifficulty, Image> kv in diffBtnImages)
        {
            bool active = kv.Key == selectedDifficulty;
            if (!active)
            {
                kv.Value.color = new Color(0.14f, 0.22f, 0.28f, 1f);
            }
            else
            {
                kv.Value.color = kv.Key == BotDifficulty.Beginner
                    ? RoatanVisualAssets.PalmGreen
                    : (kv.Key == BotDifficulty.Novice ? RoatanVisualAssets.BrightLagoon : RoatanVisualAssets.IslandGold);
            }
        }

        // Enforce valid player counts per Match Format & update Padna lineup preview
        if (selectedFormat == MatchFormat.SIX_LOV)
        {
            selectedBotCount = 3;
            if (opponentsHeaderLabel != null) opponentsHeaderLabel.text = "PADNA TABLE LINEUP (2v2)";
            if (opponentsRowObj != null) opponentsRowObj.SetActive(false);
            if (fixed2v2NoteLabel != null)
            {
                fixed2v2NoteLabel.gameObject.SetActive(true);
                fixed2v2NoteLabel.text = "You & Patrik (Padna)  vs  Anthony & Astor";
            }
        }
        else if (selectedFormat == MatchFormat.TWO_BEST)
        {
            if (selectedBotCount == 2) selectedBotCount = 1;
            if (opponentsHeaderLabel != null) opponentsHeaderLabel.text = "MATCH SIZE (1v1 OR 2v2)";
            if (opponentsRowObj != null) opponentsRowObj.SetActive(true);
            if (fixed2v2NoteLabel != null)
            {
                fixed2v2NoteLabel.gameObject.SetActive(true);
                fixed2v2NoteLabel.text = selectedBotCount == 3
                    ? "Padna 2v2: You & Patrik  vs  Anthony & Astor"
                    : "Head-to-Head 1v1: You  vs  Anthony";
            }
            if (botCountBtnObjs.ContainsKey(1)) botCountBtnObjs[1].SetActive(true);
            if (botCountBtnObjs.ContainsKey(2)) botCountBtnObjs[2].SetActive(false);
            if (botCountBtnObjs.ContainsKey(3)) botCountBtnObjs[3].SetActive(true);
        }
        else
        {
            if (opponentsHeaderLabel != null) opponentsHeaderLabel.text = "PLAYERS (FREE-FOR-ALL)";
            if (opponentsRowObj != null) opponentsRowObj.SetActive(true);
            if (fixed2v2NoteLabel != null)
            {
                fixed2v2NoteLabel.gameObject.SetActive(true);
                fixed2v2NoteLabel.text = selectedBotCount == 1
                    ? "1v1 FFA: You vs Anthony"
                    : (selectedBotCount == 2 ? "3P FFA: You vs Anthony & Patrik" : "4P FFA: You vs Anthony, Patrik & Astor");
            }
            if (botCountBtnObjs.ContainsKey(1)) botCountBtnObjs[1].SetActive(true);
            if (botCountBtnObjs.ContainsKey(2)) botCountBtnObjs[2].SetActive(true);
            if (botCountBtnObjs.ContainsKey(3)) botCountBtnObjs[3].SetActive(true);
        }

        foreach (KeyValuePair<int, Image> kv in botCountBtnImages)
        {
            kv.Value.color = kv.Key == selectedBotCount ? RoatanVisualAssets.BrightLagoon : new Color(0.14f, 0.22f, 0.28f, 1f);
        }

        if (diffDescLabel != null)
        {
            switch (selectedDifficulty)
            {
                case BotDifficulty.Beginner:
                    diffDescLabel.text = RoatanLocalization.T("diff_beginner_desc");
                    break;
                case BotDifficulty.Novice:
                    diffDescLabel.text = RoatanLocalization.T("diff_novice_desc");
                    break;
                case BotDifficulty.Pro:
                    diffDescLabel.text = RoatanLocalization.T("diff_pro_desc");
                    break;
            }
        }

        if (activePresetPillLabel != null)
        {
            string fmtName = selectedFormat == MatchFormat.SIX_LOV ? "Six-Lov 2v2"
                           : (selectedFormat == MatchFormat.TWO_BEST ? "Two-Best" : "Cut-Throat");
            string tblName = selectedMode == RoatanGameMode.BlockedCerrado ? "Roatan Blocked" : "Classic Draw";
            string diffName = RoatanLocalization.DifficultyLabel(selectedDifficulty);
            activePresetPillLabel.text = $"Saved Preset:  {fmtName}  •  {tblName}  •  {diffName}";
        }

        int wins = PlayerPrefs.GetInt(PrefWinsKey, 0);
        int games = PlayerPrefs.GetInt(PrefGamesKey, 0);
        int winPct = games > 0 ? Mathf.RoundToInt((wins * 100f) / games) : 0;
        string statsSummary = $"Offline Record: {wins} Wins  •  {games} Matches  ({winPct}%)";
        if (statsLabel != null) statsLabel.text = statsSummary;
        if (drawerStatsLabel != null) drawerStatsLabel.text = $"Wins: {wins}\nMatches Played: {games}\nWin Rate: {winPct}%";
    }

    private void RefreshMatchHeader()
    {
        if (engine == null) return;
        string fmtShort = engine.MatchFormat == MatchFormat.SIX_LOV ? "Six-Lov"
                        : (engine.MatchFormat == MatchFormat.TWO_BEST ? "Two-Best" : "Cut-Throat");
        string styleShort = engine.TableStyle == TableStyle.BLOCK ? "Block" : "Draw";
        string pilePart = engine.TableStyle == TableStyle.DRAW
            ? $" • Bone:{engine.BoneyardCount}"
            : (engine.LockedPileCount > 0 ? $" • Lock:{engine.LockedPileCount}" : "");

        string scorePart = engine.IsTeam2v2
            ? $"Us {engine.TeamScores[0]}-{engine.TeamScores[1]} Them"
            : $"Wins {engine.Players[0].MatchScore}/{engine.TargetMatchScore}";

        matchHeaderPill.text = $"{fmtShort} ({styleShort}) • Rd {engine.RoundNumber} • {scorePart}{pilePart}";
    }

    private void RefreshOpponentSeats()
    {
        if (engine == null) return;
        List<RoatanMatchEngine.MatchPlayer> bots = engine.Players.Where(p => p.IsBot).ToList();

        int[] seatMap = bots.Count == 1 ? new[] { 0 }
                      : bots.Count == 2 ? new[] { 1, 2 }
                      : new[] { 1, 0, 2 };

        foreach (OpponentSeatUI seat in opponentSeats)
        {
            seat.Root.SetActive(false);
        }

        for (int i = 0; i < bots.Count && i < seatMap.Length; i++)
        {
            RoatanMatchEngine.MatchPlayer bot = bots[i];
            OpponentSeatUI seat = opponentSeats[seatMap[i]];
            seat.Root.SetActive(true);
            bool isPartner = engine.IsTeam2v2 && bot.TeamId == engine.Players[0].TeamId;
            seat.NameText.text = isPartner ? $"{bot.Name} (Padna)" : bot.Name;
            seat.CountText.text = $"{bot.Hand.Count} tiles";
            seat.ScoreText.text = $"{bot.MatchScore}/{engine.TargetMatchScore} W";

            bool isTurn = engine.CurrentPlayer.Id == bot.Id;
            seat.TurnRing.color = isTurn ? RoatanVisualAssets.IslandGold : (isPartner ? RoatanVisualAssets.PalmGreen : new Color(1f, 1f, 1f, 0.14f));
            seat.Background.color = isTurn ? new Color(0.12f, 0.36f, 0.38f, 0.96f) : new Color(0.06f, 0.17f, 0.22f, 0.88f);

            // Render small muted pip icons beneath nameplate for suits passed on in the current hand
            foreach (GameObject oldPip in seat.PassedPipObjs) Destroy(oldPip);
            seat.PassedPipObjs.Clear();

            if (seat.PassedPipsRow != null)
            {
                if (playerPassedPips.TryGetValue(bot.Id, out HashSet<int> passed) && passed.Count > 0)
                {
                    seat.PassedPipsRow.gameObject.SetActive(true);
                    foreach (int pip in passed.OrderBy(p => p))
                    {
                        seat.PassedPipObjs.Add(CreateMutedPipIcon(seat.PassedPipsRow, pip));
                    }
                }
                else
                {
                    seat.PassedPipsRow.gameObject.SetActive(false);
                }
            }
        }
    }

    private void RebuildHumanHand()
    {
        foreach (GameObject go in spawnedHandTiles) Destroy(go);
        spawnedHandTiles.Clear();

        if (engine == null) return;
        RoatanMatchEngine.MatchPlayer human = engine.Players[0];

        int count = human.Hand.Count;
        float tileW = count <= 7 ? 112f : Mathf.Max(78f, 840f / count);
        float tileH = tileW * 2f;

        foreach (DominoDto tile in human.Hand)
        {
            DominoDto captured = tile;
            bool isPlayable = waitingForHumanInput && currentHumanMoves.Any(m =>
                (m.Domino.Side1 == tile.Side1 && m.Domino.Side2 == tile.Side2) ||
                (m.Domino.Side1 == tile.Side2 && m.Domino.Side2 == tile.Side1));

            bool isSelected = selectedHandDomino != null &&
                              selectedHandDomino.Side1 == tile.Side1 &&
                              selectedHandDomino.Side2 == tile.Side2;

            GameObject slot = new GameObject($"HandTile_{tile.Side1}_{tile.Side2}");
            slot.transform.SetParent(handContainer, false);
            LayoutElement le = slot.AddComponent<LayoutElement>();
            le.preferredWidth = tileW;
            le.preferredHeight = tileH + 26f;

            GameObject tileBtnObj = new GameObject("TileVisual");
            tileBtnObj.transform.SetParent(slot.transform, false);
            RectTransform rect = tileBtnObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(tileW, tileH);
            rect.anchoredPosition = new Vector2(0f, isSelected ? 22f : (isPlayable ? 10f : -6f));

            if (isPlayable || isSelected)
            {
                GameObject ringObj = new GameObject("GlowRing");
                ringObj.transform.SetParent(tileBtnObj.transform, false);
                RectTransform ringRect = ringObj.AddComponent<RectTransform>();
                ringRect.anchorMin = Vector2.zero;
                ringRect.anchorMax = Vector2.one;
                ringRect.offsetMin = new Vector2(-6f, -6f);
                ringRect.offsetMax = new Vector2(6f, 6f);
                Image ringImg = ringObj.AddComponent<Image>();
                ringImg.sprite = RoatanVisualAssets.RoundedOutlineSprite;
                ringImg.type = Image.Type.Sliced;
                ringImg.color = isSelected ? new Color(1f, 0.92f, 0.35f, 1f) : new Color(0.95f, 0.74f, 0.24f, 0.85f);
                ringImg.raycastTarget = false;
            }

            Image faceImg = tileBtnObj.AddComponent<Image>();
            faceImg.sprite = RoatanVisualAssets.GetDominoSprite(tile.Side1, tile.Side2);
            faceImg.color = (!waitingForHumanInput || isPlayable) ? Color.white : new Color(0.72f, 0.76f, 0.80f, 0.85f);

            RoatanTileSlamGesture slamGesture = tileBtnObj.AddComponent<RoatanTileSlamGesture>();
            slamGesture.Configure(captured, isPlayable, OnHumanTileSlamFlicked);

            Button btn = tileBtnObj.AddComponent<Button>();
            btn.targetGraphic = faceImg;
            btn.onClick.AddListener(() =>
            {
                if (slamGesture.SuppressNextClick)
                {
                    slamGesture.ConsumeSuppressedClick();
                    return;
                }
                OnHumanHandTileClicked(captured);
            });

            spawnedHandTiles.Add(slot);
        }
    }

    private void UpdateStatusBanner(string message)
    {
        if (matchStatusBanner != null)
        {
            matchStatusBanner.text = message;
        }
    }

    private void RefreshSoundLabels()
    {
        bool muted = RoatanSoundFX.Instance != null && RoatanSoundFX.Instance.IsMuted;
        if (homeSoundBtnLabel != null) homeSoundBtnLabel.text = muted ? RoatanLocalization.T("sound_off") : RoatanLocalization.T("sound_on");
        if (matchSoundBtnLabel != null) matchSoundBtnLabel.text = muted ? "Muted" : "SFX";
    }

    // ==================== UI CONSTRUCTION (PORTRAIT 1080x1920) ====================

    private void BuildRootCanvas()
    {
        GameObject canvasObj = new GameObject("RoatanPortraitCanvas");
        canvasObj.transform.SetParent(transform, false);

        rootCanvas = canvasObj.AddComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = 200;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        Image bg = canvasObj.AddComponent<Image>();
        bg.sprite = RoatanVisualAssets.TableSurfaceSprite;
        bg.type = Image.Type.Simple;
        bg.preserveAspect = false;

        GameObject safeObj = new GameObject("SafeAreaRoot");
        safeObj.transform.SetParent(canvasObj.transform, false);
        safeAreaRoot = safeObj.AddComponent<RectTransform>();
        safeAreaRoot.anchorMin = Vector2.zero;
        safeAreaRoot.anchorMax = Vector2.one;
        safeAreaRoot.offsetMin = new Vector2(20f, 20f);
        safeAreaRoot.offsetMax = new Vector2(-20f, -20f);
    }

    private void BuildHomeScreen()
    {
        homeScreen = CreateStretchPanel(safeAreaRoot, "HomeScreen", Color.clear);

        // Compact Top Header Bar: Top-Left Language Pill ("EN v") + Top-Right Utility Drawer Button (3-bar icon + "MENU")
        GameObject topBar = new GameObject("HomeTopBar");
        topBar.transform.SetParent(homeScreen.transform, false);
        RectTransform topBarRect = topBar.AddComponent<RectTransform>();
        topBarRect.anchorMin = new Vector2(0f, 1f);
        topBarRect.anchorMax = new Vector2(1f, 1f);
        topBarRect.pivot = new Vector2(0.5f, 1f);
        topBarRect.sizeDelta = new Vector2(0f, 92f);
        topBarRect.anchoredPosition = new Vector2(0f, -12f);

        Button langPillBtn = CreatePillButton(topBar.transform, "BtnTopLang", "EN v", new Color(0.09f, 0.24f, 0.30f, 0.95f), new Vector2(-392f, 0f), new Vector2(168f, 68f), 26, CycleLanguage);
        topLangPillLabel = langPillBtn.GetComponentInChildren<TMP_Text>();
        topLangPillLabel.color = RoatanVisualAssets.IslandGold;

        Button menuPillBtn = CreatePillButton(topBar.transform, "BtnUtilityMenu", "", new Color(0.09f, 0.24f, 0.30f, 0.95f), new Vector2(376f, 0f), new Vector2(196f, 68f), 24, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            RefreshHomeSelections();
            RefreshSoundLabels();
            if (utilityDrawerOverlay != null)
            {
                utilityDrawerOverlay.transform.SetAsLastSibling();
                utilityDrawerOverlay.SetActive(true);
            }
        });
        // Crisp 3-bar hamburger icon drawn with UI strips (never renders as missing glyph box on Android)
        float[] barYs = { 10f, 0f, -10f };
        for (int i = 0; i < barYs.Length; i++)
        {
            GameObject bar = new GameObject($"MenuBar_{i}");
            bar.transform.SetParent(menuPillBtn.transform, false);
            RectTransform br = bar.AddComponent<RectTransform>();
            br.sizeDelta = new Vector2(28f, 4f);
            br.anchoredPosition = new Vector2(-54f, barYs[i]);
            Image bi = bar.AddComponent<Image>();
            bi.color = RoatanVisualAssets.IslandGold;
            bi.raycastTarget = false;
        }
        TMP_Text menuLabel = CreateText(menuPillBtn.transform, "MenuText", "MENU", 24, FontStyles.Bold, Color.white, new Vector2(20f, 0f), new Vector2(124f, 52f));
        menuLabel.raycastTarget = false;

        // Roatan Island Caribbean Centerpiece Header Banner
        GameObject headerCard = CreateCard(homeScreen.transform, "HeaderBanner", new Color(0.03f, 0.14f, 0.19f, 0.96f), new Vector2(0f, 470f), new Vector2(940f, 296f));
        headerCard.AddComponent<RectMask2D>();

        // Subtle gold border frame around the banner
        GameObject hdrOutline = new GameObject("BannerOutline");
        hdrOutline.transform.SetParent(headerCard.transform, false);
        RectTransform hoRect = hdrOutline.AddComponent<RectTransform>();
        hoRect.anchorMin = Vector2.zero;
        hoRect.anchorMax = Vector2.one;
        hoRect.offsetMin = Vector2.zero;
        hoRect.offsetMax = Vector2.zero;
        Image hoImg = hdrOutline.AddComponent<Image>();
        hoImg.sprite = RoatanVisualAssets.RoundedOutlineSprite;
        hoImg.type = Image.Type.Sliced;
        hoImg.color = new Color(0.95f, 0.74f, 0.24f, 0.55f);
        hoImg.raycastTarget = false;

        // Layer 1: Tropical Sunset & Caribbean Reef Radial Glow Backdrop
        GameObject glowObj = new GameObject("TropicalSunsetReefGlow");
        glowObj.transform.SetParent(headerCard.transform, false);
        RectTransform glowRect = glowObj.AddComponent<RectTransform>();
        glowRect.anchoredPosition = new Vector2(0f, 6f);
        glowRect.sizeDelta = new Vector2(930f, 292f);
        Image glowImg = glowObj.AddComponent<Image>();
        glowImg.sprite = RoatanVisualAssets.SunsetReefGlowSprite;
        glowImg.raycastTarget = false;

        // Layer 2: Stylized Curved Coconut Palm Silhouettes rising behind western/central ridge
        SpawnBannerPalm(headerCard.transform, "PalmWest", new Vector2(-218f, -14f), new Vector2(168f, 198f), 7f, flipX: false, new Color(0.05f, 0.26f, 0.25f, 0.78f));
        SpawnBannerPalm(headerCard.transform, "PalmRidgeCenter", new Vector2(-86f, 8f), new Vector2(182f, 212f), -8f, flipX: true, new Color(0.06f, 0.30f, 0.27f, 0.72f));
        SpawnBannerPalm(headerCard.transform, "PalmEastCay", new Vector2(138f, -20f), new Vector2(142f, 168f), -13f, flipX: false, new Color(0.05f, 0.24f, 0.25f, 0.68f));

        // Layer 3: Roatan Island Map Silhouette (Reef-Turquoise Glow Shadow + Rich Tropical Emerald-Gold Landmass)
        GameObject mapShadowObj = new GameObject("RoatanIslandMapReefGlow");
        mapShadowObj.transform.SetParent(headerCard.transform, false);
        RectTransform msRect = mapShadowObj.AddComponent<RectTransform>();
        msRect.anchoredPosition = new Vector2(2f, 2f);
        msRect.sizeDelta = new Vector2(768f, 226f);
        msRect.localScale = new Vector3(1.025f, 1.045f, 1f);
        Image msImg = mapShadowObj.AddComponent<Image>();
        msImg.sprite = RoatanVisualAssets.RoatanIslandMapSprite;
        msImg.preserveAspect = true;
        msImg.color = new Color(0.11f, 0.80f, 0.78f, 0.44f);
        msImg.raycastTarget = false;

        GameObject mapObj = new GameObject("RoatanIslandMap");
        mapObj.transform.SetParent(headerCard.transform, false);
        RectTransform mRect = mapObj.AddComponent<RectTransform>();
        mRect.anchoredPosition = new Vector2(0f, 6f);
        mRect.sizeDelta = new Vector2(752f, 218f);
        Image mImg = mapObj.AddComponent<Image>();
        mImg.sprite = RoatanVisualAssets.RoatanIslandMapSprite;
        mImg.preserveAspect = true;
        mImg.color = new Color(0.24f, 0.68f, 0.43f, 0.86f);
        mImg.raycastTarget = false;

        // Layer 4: Flanking [6|6] & [5|3] Domino Tiles + High-Readability Foreground Typography
        GameObject leftDom = new GameObject("LogoDomLeft");
        leftDom.transform.SetParent(headerCard.transform, false);
        RectTransform lRect = leftDom.AddComponent<RectTransform>();
        lRect.sizeDelta = new Vector2(82f, 164f);
        lRect.anchoredPosition = new Vector2(-372f, 4f);
        lRect.localRotation = Quaternion.Euler(0f, 0f, 16f);
        Image lImg = leftDom.AddComponent<Image>();
        lImg.sprite = RoatanVisualAssets.GetDominoSprite(6, 6);
        lImg.raycastTarget = false;

        GameObject rightDom = new GameObject("LogoDomRight");
        rightDom.transform.SetParent(headerCard.transform, false);
        RectTransform rRect = rightDom.AddComponent<RectTransform>();
        rRect.sizeDelta = new Vector2(82f, 164f);
        rRect.anchoredPosition = new Vector2(372f, 4f);
        rRect.localRotation = Quaternion.Euler(0f, 0f, -16f);
        Image rImg = rightDom.AddComponent<Image>();
        rImg.sprite = RoatanVisualAssets.GetDominoSprite(5, 3);
        rImg.raycastTarget = false;

        // Crisp dark drop-shadow behind "ROATAN DOMINO" for high contrast over the island map
        TMP_Text titleShadow = CreateText(headerCard.transform, "TitleShadow", "ROATAN DOMINO", 62, FontStyles.Bold, new Color(0.02f, 0.06f, 0.08f, 0.92f), new Vector2(3f, 52f), new Vector2(650f, 80f));
        titleShadow.raycastTarget = false;
        TMP_Text titleMain = CreateText(headerCard.transform, "Title", "ROATAN DOMINO", 62, FontStyles.Bold, new Color(0.969f, 0.788f, 0.282f, 1f), new Vector2(0f, 56f), new Vector2(650f, 80f));
        titleMain.outlineWidth = 0.22f;
        titleMain.outlineColor = new Color32(8, 22, 28, 255);
        titleMain.raycastTarget = false;

        subtitleShadowLabel = CreateText(headerCard.transform, "SubtitleShadow", RoatanLocalization.T("subtitle"), 25, FontStyles.Bold, new Color(0.02f, 0.06f, 0.08f, 0.85f), new Vector2(2f, -15f), new Vector2(670f, 44f));
        subtitleShadowLabel.raycastTarget = false;
        subtitleLabel = CreateText(headerCard.transform, "Subtitle", RoatanLocalization.T("subtitle"), 25, FontStyles.Bold, new Color(0.92f, 0.98f, 0.98f, 1f), new Vector2(0f, -13f), new Vector2(670f, 44f));
        subtitleLabel.raycastTarget = false;

        // Subtle dark pill behind Offline Record stat so it always reads crisply over the island ridge
        GameObject statPill = CreateCard(headerCard.transform, "StatReadabilityPill", new Color(0.02f, 0.10f, 0.14f, 0.72f), new Vector2(0f, -84f), new Vector2(610f, 46f));
        statPill.GetComponent<Image>().raycastTarget = false;
        statsLabel = CreateText(statPill.transform, "Stats", "Offline Record: 0 Wins  •  0 Matches (0%)", 23, FontStyles.Bold, Color.white, Vector2.zero, new Vector2(590f, 38f));
        statsLabel.raycastTarget = false;

        // Active Preset Summary Chip
        GameObject presetChip = CreateCard(homeScreen.transform, "ActivePresetChip", new Color(0.06f, 0.22f, 0.28f, 0.88f), new Vector2(0f, 240f), new Vector2(860f, 68f));
        activePresetPillLabel = CreateText(presetChip.transform, "PresetText", "Saved Preset: Six-Lov 2v2 • Roatan Blocked • Novice", 24, FontStyles.Bold, new Color(0.95f, 0.88f, 0.65f), Vector2.zero, new Vector2(820f, 54f));

        // 3 Focused Primary Buttons: PLAY OFFLINE | CUSTOM MATCH | PLAY ONLINE
        Button playOfflineBtn = CreatePillButton(homeScreen.transform, "BtnPlayOffline", "PLAY OFFLINE", RoatanVisualAssets.PalmGreen, new Vector2(0f, 65f), new Vector2(780f, 136f), 44, StartNewMatch);
        playNowBtnLabel = playOfflineBtn.GetComponentInChildren<TMP_Text>();

        Button customMatchBtn = CreatePillButton(homeScreen.transform, "BtnCustomMatch", "CUSTOM MATCH", new Color(0.11f, 0.44f, 0.50f, 1f), new Vector2(0f, -110f), new Vector2(780f, 124f), 38, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            RefreshHomeSelections();
            if (setupRuleTooltipCard != null) setupRuleTooltipCard.SetActive(false);
            if (matchSetupSheet != null)
            {
                matchSetupSheet.transform.SetAsLastSibling();
                matchSetupSheet.SetActive(true);
            }
        });
        GameObject cmOutline = new GameObject("Outline");
        cmOutline.transform.SetParent(customMatchBtn.transform, false);
        RectTransform cmoRect = cmOutline.AddComponent<RectTransform>();
        cmoRect.anchorMin = Vector2.zero;
        cmoRect.anchorMax = Vector2.one;
        cmoRect.offsetMin = Vector2.zero;
        cmoRect.offsetMax = Vector2.zero;
        Image cmoImg = cmOutline.AddComponent<Image>();
        cmoImg.sprite = RoatanVisualAssets.RoundedOutlineSprite;
        cmoImg.type = Image.Type.Sliced;
        cmoImg.color = new Color(0.95f, 0.74f, 0.24f, 0.75f);
        cmoImg.raycastTarget = false;

        Button playOnlineBtn = CreatePillButton(homeScreen.transform, "BtnPlayOnline", "PLAY ONLINE", RoatanVisualAssets.IslandGold, new Vector2(0f, -280f), new Vector2(780f, 124f), 38, OpenOnlineScreen);
        onlineModeBtnLabel = playOnlineBtn.GetComponentInChildren<TMP_Text>();
        onlineModeBtnLabel.color = new Color(0.08f, 0.11f, 0.14f, 1f);

        CreateText(homeScreen.transform, "HomeFooterTip", "Tap CUSTOM MATCH to switch Six-Lov, Two-Best, Cut-Throat, or 1-1 Playoff", 22, FontStyles.Italic, new Color(0.80f, 0.92f, 0.94f, 0.88f), new Vector2(0f, -430f), new Vector2(860f, 52f));
    }

    private static void SpawnBannerPalm(Transform parent, string name, Vector2 pos, Vector2 size, float rotZ, bool flipX, Color tint)
    {
        GameObject palm = new GameObject(name);
        palm.transform.SetParent(parent, false);
        RectTransform pr = palm.AddComponent<RectTransform>();
        pr.anchoredPosition = pos;
        pr.sizeDelta = size;
        pr.localRotation = Quaternion.Euler(0f, 0f, rotZ);
        pr.localScale = new Vector3(flipX ? -1f : 1f, 1f, 1f);
        Image pi = palm.AddComponent<Image>();
        pi.sprite = RoatanVisualAssets.CoconutPalmSprite;
        pi.preserveAspect = true;
        pi.color = tint;
        pi.raycastTarget = false;
    }

    private void BuildMatchSetupSheet()
    {
        matchSetupSheet = CreateStretchPanel(safeAreaRoot, "MatchSetupSheet", new Color(0f, 0f, 0f, 0.76f));

        GameObject sheet = CreateCard(matchSetupSheet.transform, "SetupCard", RoatanVisualAssets.DarkSlate, new Vector2(0f, -18f), new Vector2(960f, 1460f));

        CreateText(sheet.transform, "SheetTitle", "CUSTOM MATCH SETUP", 38, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(-45f, 655f), new Vector2(680f, 58f));

        // Compact (?) Rule Tooltip Button + Close Button
        CreatePillButton(sheet.transform, "BtnRuleTooltip", "?", RoatanVisualAssets.BrightLagoon, new Vector2(315f, 655f), new Vector2(68f, 64f), 32, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            if (setupRuleTooltipCard != null)
            {
                setupRuleTooltipCard.SetActive(!setupRuleTooltipCard.activeSelf);
                if (setupRuleTooltipCard.activeSelf) setupRuleTooltipCard.transform.SetAsLastSibling();
            }
        });

        CreatePillButton(sheet.transform, "BtnCloseSetup", "X", new Color(0.24f, 0.22f, 0.26f, 1f), new Vector2(400f, 655f), new Vector2(68f, 64f), 30, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            SaveMatchPreset();
            matchSetupSheet.SetActive(false);
        });

        // 1. Table Style (Roatan Blocked | Classic Draw)
        modeHeadingLabel = CreateText(sheet.transform, "ModeHeading", "TABLE STYLE", 25, FontStyles.Bold, new Color(0.95f, 0.88f, 0.65f), new Vector2(0f, 565f), new Vector2(880f, 36f));

        Button cerrBtn = CreateCompactModeCard(
            sheet.transform,
            "CardCerrado",
            "Roatan Blocked",
            "No drawing • Pure strategy",
            new Vector2(-225f, 480f),
            new Vector2(430f, 114f),
            () => { RoatanSoundFX.Instance?.PlayTap(); selectedMode = RoatanGameMode.BlockedCerrado; SaveMatchPreset(); RefreshHomeSelections(); },
            out cerradoTitleLabel,
            out cerradoDescLabel);
        modeCardImages[RoatanGameMode.BlockedCerrado] = cerrBtn.GetComponent<Image>();

        Button drawBtn = CreateCompactModeCard(
            sheet.transform,
            "CardClassicDraw",
            "Classic Draw",
            "Draw from boneyard",
            new Vector2(225f, 480f),
            new Vector2(430f, 114f),
            () => { RoatanSoundFX.Instance?.PlayTap(); selectedMode = RoatanGameMode.ClassicDraw; SaveMatchPreset(); RefreshHomeSelections(); },
            out drawTitleLabel,
            out drawDescLabel);
        modeCardImages[RoatanGameMode.ClassicDraw] = drawBtn.GetComponent<Image>();

        // 2. Match Format (Six-Lov | Two-Best | Cut-Throat)
        CreateText(sheet.transform, "FormatHeading", "MATCH FORMAT", 25, FontStyles.Bold, new Color(0.95f, 0.88f, 0.65f), new Vector2(0f, 378f), new Vector2(880f, 36f));

        MatchFormat[] formats = { MatchFormat.SIX_LOV, MatchFormat.TWO_BEST, MatchFormat.CUT_THROAT };
        string[] fmtLabels = { "Six-Lov\n(2v2 • 6-0)", "Two-Best\n(Best of 3)", "Cut-Throat\n(FFA • 6 W)" };
        float[] triXs = { -295f, 0f, 295f };
        for (int i = 0; i < formats.Length; i++)
        {
            MatchFormat fmt = formats[i];
            Button fb = CreatePillButton(sheet.transform, $"BtnFmt_{fmt}", fmtLabels[i], new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(triXs[i], 292f), new Vector2(276f, 96f), 25, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                selectedFormat = fmt;
                SaveMatchPreset();
                RefreshHomeSelections();
            });
            formatBtnImages[fmt] = fb.GetComponent<Image>();
        }

        formatDescLabel = CreateText(sheet.transform, "FormatDesc", "", 21, FontStyles.Italic, new Color(0.86f, 0.95f, 0.97f), new Vector2(0f, 194f), new Vector2(860f, 66f));

        // 1-1 Playoff Toggle (Six-Lov only)
        oneOnePlayoffBtn = CreatePillButton(sheet.transform, "BtnOneOnePlayoff", "1-1 Playoff Rule: OFF", new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(0f, 114f), new Vector2(460f, 62f), 24, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            selectedOneOnePlayoff = !selectedOneOnePlayoff;
            SaveMatchPreset();
            RefreshHomeSelections();
        });
        oneOnePlayoffBtnImg = oneOnePlayoffBtn.GetComponent<Image>();
        oneOnePlayoffBtnLabel = oneOnePlayoffBtn.GetComponentInChildren<TMP_Text>();

        // 3. Bot Difficulty
        diffHeadingLabel = CreateText(sheet.transform, "DiffHeading", RoatanLocalization.T("bot_difficulty"), 25, FontStyles.Bold, new Color(0.95f, 0.88f, 0.65f), new Vector2(0f, 22f), new Vector2(880f, 36f));

        BotDifficulty[] diffs = { BotDifficulty.Beginner, BotDifficulty.Novice, BotDifficulty.Pro };
        for (int i = 0; i < diffs.Length; i++)
        {
            BotDifficulty d = diffs[i];
            Button b = CreatePillButton(sheet.transform, $"BtnDiff_{d}", RoatanLocalization.DifficultyLabel(d), new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(triXs[i], -56f), new Vector2(274f, 82f), 29, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                selectedDifficulty = d;
                SaveMatchPreset();
                RefreshHomeSelections();
            });
            diffBtnImages[d] = b.GetComponent<Image>();
            diffBtnLabels[d] = b.GetComponentInChildren<TMP_Text>();
        }

        diffDescLabel = CreateText(sheet.transform, "DiffDesc", "", 21, FontStyles.Italic, new Color(0.86f, 0.95f, 0.97f), new Vector2(0f, -132f), new Vector2(860f, 52f));

        // 4. Padna Table Lineup Preview & Match Size Selector
        opponentsHeaderLabel = CreateText(sheet.transform, "OppHeading", "PADNA TABLE LINEUP (2v2)", 25, FontStyles.Bold, new Color(0.95f, 0.88f, 0.65f), new Vector2(0f, -214f), new Vector2(880f, 36f));

        GameObject lineupCard = CreateCard(sheet.transform, "LineupPreviewCard", new Color(0.06f, 0.19f, 0.25f, 0.94f), new Vector2(0f, -286f), new Vector2(860f, 76f));
        fixed2v2NoteLabel = CreateText(lineupCard.transform, "Fixed2v2Note", "You & Patrik (Padna)  vs  Anthony & Astor", 25, FontStyles.Bold, RoatanVisualAssets.IslandGold, Vector2.zero, new Vector2(820f, 60f));

        opponentsRowObj = new GameObject("OpponentsRow");
        opponentsRowObj.transform.SetParent(sheet.transform, false);
        RectTransform oppRowRect = opponentsRowObj.AddComponent<RectTransform>();
        oppRowRect.anchoredPosition = new Vector2(0f, -384f);
        oppRowRect.sizeDelta = new Vector2(880f, 84f);

        int[] botCounts = { 1, 2, 3 };
        string[] botLabels = { "1v1 (1 Bot)", "3P (2 Bots)", "4P (3 Bots)" };
        for (int i = 0; i < botCounts.Length; i++)
        {
            int c = botCounts[i];
            Button b = CreatePillButton(opponentsRowObj.transform, $"BtnBots_{c}", botLabels[i], new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(triXs[i], 0f), new Vector2(274f, 76f), 25, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                selectedBotCount = c;
                SaveMatchPreset();
                RefreshHomeSelections();
            });
            botCountBtnImages[c] = b.GetComponent<Image>();
            botCountBtnObjs[c] = b.gameObject;
        }

        // Compact Rule Tooltip Popover (toggled by (?) button)
        setupRuleTooltipCard = CreateCard(sheet.transform, "RuleTooltipPopover", new Color(0.05f, 0.14f, 0.20f, 0.98f), new Vector2(0f, 445f), new Vector2(820f, 195f));
        setupRuleTooltipText = CreateText(setupRuleTooltipCard.transform, "TipText", "", 22, FontStyles.Normal, Color.white, new Vector2(0f, 16f), new Vector2(760f, 120f));
        CreatePillButton(setupRuleTooltipCard.transform, "BtnCloseTip", "Got it", RoatanVisualAssets.PalmGreen, new Vector2(0f, -64f), new Vector2(180f, 48f), 22, () =>
        {
            setupRuleTooltipCard.SetActive(false);
        });
        setupRuleTooltipCard.SetActive(false);

        // Primary START MATCH CTA
        CreatePillButton(sheet.transform, "BtnStartCustomMatch", "START MATCH", RoatanVisualAssets.PalmGreen, new Vector2(0f, -565f), new Vector2(740f, 122f), 42, StartNewMatch);

        matchSetupSheet.SetActive(false);
    }

    private void BuildUtilityDrawer()
    {
        utilityDrawerOverlay = CreateStretchPanel(safeAreaRoot, "UtilityDrawerOverlay", new Color(0f, 0f, 0f, 0.65f));

        // Backdrop click closes drawer
        Button scrimBtn = utilityDrawerOverlay.AddComponent<Button>();
        scrimBtn.transition = Selectable.Transition.None;
        scrimBtn.onClick.AddListener(() => utilityDrawerOverlay.SetActive(false));

        // Right-aligned Slide-Out Drawer Panel
        GameObject drawer = CreateCard(utilityDrawerOverlay.transform, "DrawerPanel", RoatanVisualAssets.DarkSlate, Vector2.zero, new Vector2(640f, 0f));
        RectTransform dRect = drawer.GetComponent<RectTransform>();
        dRect.anchorMin = new Vector2(1f, 0f);
        dRect.anchorMax = new Vector2(1f, 1f);
        dRect.pivot = new Vector2(1f, 0.5f);
        dRect.offsetMin = new Vector2(-640f, 20f);
        dRect.offsetMax = new Vector2(-8f, -20f);

        CreateText(drawer.transform, "DrawerTitle", "ROATAN MENU", 38, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(-45f, 760f), new Vector2(440f, 64f));
        CreatePillButton(drawer.transform, "BtnCloseDrawer", "X", new Color(0.22f, 0.22f, 0.28f, 1f), new Vector2(240f, 760f), new Vector2(72f, 66f), 30, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            utilityDrawerOverlay.SetActive(false);
        });

        // 1. Offline Record / Stats Card
        GameObject statsCard = CreateCard(drawer.transform, "DrawerStatsCard", new Color(0.06f, 0.20f, 0.26f, 0.96f), new Vector2(0f, 565f), new Vector2(540f, 224f));
        CreateText(statsCard.transform, "StatsHdr", "OFFLINE RECORD & STATS", 23, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, 72f), new Vector2(500f, 36f));
        drawerStatsLabel = CreateText(statsCard.transform, "StatsBody", "Wins: 0\nMatches Played: 0\nWin Rate: 0%", 26, FontStyles.Normal, Color.white, new Vector2(0f, -20f), new Vector2(500f, 140f));

        // 2. Sound ON/OFF Toggle
        Button soundBtn = CreatePillButton(drawer.transform, "BtnDrawerSound", RoatanLocalization.T("sound_on"), new Color(0.10f, 0.28f, 0.34f, 1f), new Vector2(0f, 385f), new Vector2(540f, 84f), 28, () =>
        {
            RoatanSoundFX.Instance?.ToggleMute();
            RoatanSoundFX.Instance?.PlayTap();
            RefreshSoundLabels();
        });
        homeSoundBtnLabel = soundBtn.GetComponentInChildren<TMP_Text>();

        // 3. How to Play (opens Tabbed Rule Guide)
        Button howBtn = CreatePillButton(drawer.transform, "BtnDrawerHowToPlay", RoatanLocalization.T("how_to_play"), RoatanVisualAssets.BrightLagoon, new Vector2(0f, 282f), new Vector2(540f, 84f), 28, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            utilityDrawerOverlay.SetActive(false);
            SelectRulesTab("Six-Lov");
            howToPlayModal.transform.SetAsLastSibling();
            howToPlayModal.SetActive(true);
        });
        howToPlayBtnLabel = howBtn.GetComponentInChildren<TMP_Text>();

        // 4. Language quick cycle inside drawer too
        CreatePillButton(drawer.transform, "BtnDrawerLang", "Switch Language (EN/ES/FR/DE)", new Color(0.10f, 0.24f, 0.30f, 1f), new Vector2(0f, 180f), new Vector2(540f, 80f), 24, CycleLanguage);

        // 5. Online Server Endpoint Configuration (Production / Custom Tunnel URL / Localhost)
        GameObject serverCard = CreateCard(drawer.transform, "DrawerServerCard", new Color(0.06f, 0.20f, 0.26f, 0.96f), new Vector2(0f, -110f), new Vector2(540f, 444f));
        CreateText(serverCard.transform, "ServerHdr", "ONLINE SERVER URL", 23, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, 182f), new Vector2(500f, 36f));

        RoatanServerPreset[] presets = { RoatanServerPreset.Production, RoatanServerPreset.CustomTunnel, RoatanServerPreset.Localhost };
        string[] presetLabels = { "Production", "Tunnel", "Localhost" };
        float[] presetXs = { -168f, 0f, 168f };
        for (int i = 0; i < presets.Length; i++)
        {
            RoatanServerPreset capturedPreset = presets[i];
            Button presetBtn = CreatePillButton(serverCard.transform, $"DrawerPreset_{capturedPreset}", presetLabels[i], new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(presetXs[i], 122f), new Vector2(158f, 54f), 21, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                SelectDrawerServerPreset(capturedPreset);
            });
            drawerServerPresetBtns[capturedPreset] = presetBtn.GetComponent<Image>();
        }

        drawerServerUrlInput = CreateInputField(serverCard.transform, "DrawerServerUrlInput", "https://...trycloudflare.com", RoatanOnlineController.GetSavedServerUrl(), new Vector2(0f, 44f), new Vector2(496f, 68f));
        drawerServerUrlInput.onValueChanged.AddListener(OnDrawerServerUrlChanged);

        drawerServerPreviewLabel = CreateText(serverCard.transform, "DrawerServerPreview", "", 18, FontStyles.Italic, new Color(0.75f, 0.92f, 0.96f), new Vector2(0f, -20f), new Vector2(496f, 44f));

        CreatePillButton(serverCard.transform, "BtnDrawerSaveServer", "Save Server URL", RoatanVisualAssets.PalmGreen, new Vector2(0f, -94f), new Vector2(496f, 68f), 25, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            SaveDrawerServerUrl();
        });

        CreatePillButton(serverCard.transform, "BtnDrawerConnectOnline", "Save & Open Online", RoatanVisualAssets.BrightLagoon, new Vector2(0f, -172f), new Vector2(496f, 64f), 24, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            if (SaveDrawerServerUrl())
            {
                OpenOnlineScreen();
                onlineController?.ConnectConfiguredServer();
            }
        });

        RoatanOnlineController.ServerEndpointChanged -= SyncDrawerServerEndpointFromPrefs;
        RoatanOnlineController.ServerEndpointChanged += SyncDrawerServerEndpointFromPrefs;
        SyncDrawerServerEndpointFromPrefs();

        utilityDrawerOverlay.SetActive(false);
    }

    private void SelectDrawerServerPreset(RoatanServerPreset preset)
    {
        drawerServerPreset = preset;
        string presetUrl = RoatanOnlineController.GetPresetUrl(preset);

        if (drawerServerUrlInput != null)
        {
            suppressDrawerServerEvents = true;
            drawerServerUrlInput.text = presetUrl;
            suppressDrawerServerEvents = false;
        }

        if (!string.IsNullOrWhiteSpace(presetUrl))
        {
            RoatanOnlineController.SaveServerEndpoint(preset, presetUrl);
        }
        else
        {
            RefreshDrawerServerPresetVisuals();
            UpdateDrawerServerPreview(presetUrl);
        }
    }

    private void OnDrawerServerUrlChanged(string newText)
    {
        if (suppressDrawerServerEvents) return;
        drawerServerPreset = RoatanOnlineController.DetectPresetForUrl(newText, RoatanServerPreset.CustomTunnel);
        RefreshDrawerServerPresetVisuals();
        UpdateDrawerServerPreview(newText);
    }

    private bool SaveDrawerServerUrl()
    {
        string raw = drawerServerUrlInput != null ? drawerServerUrlInput.text.Trim() : string.Empty;
        if (!RoatanOnlineController.TryResolveServerEndpoint(raw, out Uri wsUri, out string displayUrl, out bool isTcp, out string tcpHost, out int tcpPort, out bool tcpSsl, out string err))
        {
            if (drawerServerPreviewLabel != null)
            {
                drawerServerPreviewLabel.text = err;
                drawerServerPreviewLabel.color = RoatanVisualAssets.SunsetCoral;
            }
            return false;
        }

        RoatanServerPreset resolvedPreset = RoatanOnlineController.DetectPresetForUrl(displayUrl, drawerServerPreset);
        RoatanOnlineController.SaveServerEndpoint(resolvedPreset, displayUrl);
        if (drawerServerPreviewLabel != null)
        {
            drawerServerPreviewLabel.text = isTcp
                ? $"Saved: {(tcpSsl ? "tcps" : "tcp")}://{tcpHost}:{tcpPort}"
                : $"Saved: {wsUri}";
            drawerServerPreviewLabel.color = new Color(0.68f, 0.95f, 0.80f);
        }
        return true;
    }

    private void SyncDrawerServerEndpointFromPrefs()
    {
        drawerServerPreset = RoatanOnlineController.GetSavedPreset();
        string saved = RoatanOnlineController.GetSavedServerUrl();
        if (drawerServerUrlInput != null)
        {
            suppressDrawerServerEvents = true;
            drawerServerUrlInput.text = saved;
            suppressDrawerServerEvents = false;
        }
        RefreshDrawerServerPresetVisuals();
        UpdateDrawerServerPreview(saved);
    }

    private void RefreshDrawerServerPresetVisuals()
    {
        foreach (KeyValuePair<RoatanServerPreset, Image> kv in drawerServerPresetBtns)
        {
            if (kv.Value == null) continue;
            bool selected = kv.Key == drawerServerPreset;
            kv.Value.color = selected ? RoatanVisualAssets.IslandGold : new Color(0.14f, 0.22f, 0.28f, 1f);
            TMP_Text lbl = kv.Value.GetComponentInChildren<TMP_Text>();
            if (lbl != null)
            {
                lbl.color = selected ? new Color(0.08f, 0.10f, 0.12f, 1f) : Color.white;
            }
        }
    }

    private void UpdateDrawerServerPreview(string rawUrl)
    {
        if (drawerServerPreviewLabel == null) return;
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            drawerServerPreviewLabel.text = "Paste your Cloudflare Tunnel URL above";
            drawerServerPreviewLabel.color = new Color(0.75f, 0.90f, 0.94f);
            return;
        }

        if (RoatanOnlineController.TryResolveServerEndpoint(rawUrl, out Uri wsUri, out _, out bool isTcp, out string tcpHost, out int tcpPort, out bool tcpSsl, out string err))
        {
            drawerServerPreviewLabel.text = isTcp
                ? $"Endpoint: {(tcpSsl ? "tcps" : "tcp")}://{tcpHost}:{tcpPort}"
                : $"WSS/WS: {wsUri}";
            drawerServerPreviewLabel.color = new Color(0.68f, 0.95f, 0.80f);
        }
        else
        {
            drawerServerPreviewLabel.text = err;
            drawerServerPreviewLabel.color = RoatanVisualAssets.SunsetCoral;
        }
    }

    private Button CreateCompactModeCard(Transform parent, string name, string title, string subtitle, Vector2 pos, Vector2 size, System.Action onClick, out TMP_Text titleOut, out TMP_Text descOut)
    {
        GameObject card = CreateCard(parent, name, new Color(0.08f, 0.21f, 0.27f, 0.92f), pos, size);
        GameObject borderObj = new GameObject("Outline");
        borderObj.transform.SetParent(card.transform, false);
        RectTransform bRect = borderObj.AddComponent<RectTransform>();
        bRect.anchorMin = Vector2.zero;
        bRect.anchorMax = Vector2.one;
        bRect.offsetMin = Vector2.zero;
        bRect.offsetMax = Vector2.zero;
        Image bImg = borderObj.AddComponent<Image>();
        bImg.sprite = RoatanVisualAssets.RoundedOutlineSprite;
        bImg.type = Image.Type.Sliced;
        bImg.color = new Color(0.95f, 0.74f, 0.24f, 0.70f);
        bImg.raycastTarget = false;

        titleOut = CreateText(card.transform, "Title", title, 28, FontStyles.Bold, Color.white, new Vector2(0f, 18f), new Vector2(size.x - 24f, 40f));
        descOut = CreateText(card.transform, "Desc", subtitle, 19, FontStyles.Normal, new Color(0.84f, 0.94f, 0.94f), new Vector2(0f, -22f), new Vector2(size.x - 24f, 34f));

        Button btn = card.AddComponent<Button>();
        btn.targetGraphic = card.GetComponent<Image>();
        btn.onClick.AddListener(() => onClick?.Invoke());
        return btn;
    }

    private void BuildMatchScreen()
    {
        matchScreen = CreateStretchPanel(safeAreaRoot, "MatchScreen", Color.clear);

        GameObject topBar = new GameObject("TopHudBar");
        topBar.transform.SetParent(matchScreen.transform, false);
        RectTransform topRect = topBar.AddComponent<RectTransform>();
        topRect.anchorMin = new Vector2(0f, 1f);
        topRect.anchorMax = new Vector2(1f, 1f);
        topRect.pivot = new Vector2(0.5f, 1f);
        topRect.sizeDelta = new Vector2(0f, 108f);
        topRect.anchoredPosition = new Vector2(0f, -6f);

        Button backBtn = CreatePillButton(topBar.transform, "BtnBack", RoatanLocalization.T("menu_back"), new Color(0.06f, 0.17f, 0.22f, 0.92f), new Vector2(-410f, 0f), new Vector2(168f, 74f), 26, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            ShowHomeScreen();
        });
        matchBackBtnLabel = backBtn.GetComponentInChildren<TMP_Text>();

        GameObject pillCard = CreateCard(topBar.transform, "MatchHeaderCard", new Color(0.06f, 0.17f, 0.22f, 0.92f), new Vector2(-35f, 0f), new Vector2(540f, 74f));
        matchHeaderPill = CreateText(pillCard.transform, "HeaderText", "", 22, FontStyles.Bold, RoatanVisualAssets.IslandGold, Vector2.zero, new Vector2(516f, 64f));

        Button fitBtn = CreatePillButton(topBar.transform, "BtnFit", RoatanLocalization.T("fit"), new Color(0.06f, 0.17f, 0.22f, 0.92f), new Vector2(310f, 0f), new Vector2(115f, 74f), 26, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            boardView?.RecenterBoard();
        });
        matchFitBtnLabel = fitBtn.GetComponentInChildren<TMP_Text>();

        Button sfxBtn = CreatePillButton(topBar.transform, "BtnMatchSfx", "SFX", new Color(0.06f, 0.17f, 0.22f, 0.92f), new Vector2(440f, 0f), new Vector2(120f, 74f), 25, () =>
        {
            RoatanSoundFX.Instance?.ToggleMute();
            RoatanSoundFX.Instance?.PlayTap();
            RefreshSoundLabels();
        });
        matchSoundBtnLabel = sfxBtn.GetComponentInChildren<TMP_Text>();

        // Board Viewport
        GameObject boardViewportObj = new GameObject("BoardViewport");
        boardViewportObj.transform.SetParent(matchScreen.transform, false);
        RectTransform vpRect = boardViewportObj.AddComponent<RectTransform>();
        vpRect.anchorMin = new Vector2(0f, 0f);
        vpRect.anchorMax = new Vector2(1f, 1f);
        vpRect.offsetMin = new Vector2(10f, 355f);
        vpRect.offsetMax = new Vector2(-10f, -125f);
        boardViewportObj.AddComponent<RectMask2D>();

        boardView = boardViewportObj.AddComponent<RoatanBoardView>();
        boardView.Initialize(vpRect);
        boardView.SetSafeMargins(left: 44f, right: 44f, top: 220f, bottom: 118f);
        boardView.EndSlotClicked += OnBoardEndSlotClicked;

        // Compact Upper-Flank Opponent Nameplates (clear of central domino snake, raycastTarget = false on labels)
        opponentSeats.Add(CreateOpponentSeat(matchScreen.transform, "SeatTop", new Vector2(0.5f, 1f), new Vector2(0f, -154f), new Vector2(292f, 82f)));
        opponentSeats.Add(CreateOpponentSeat(matchScreen.transform, "SeatLeft", new Vector2(0f, 1f), new Vector2(114f, -216f), new Vector2(208f, 82f)));
        opponentSeats.Add(CreateOpponentSeat(matchScreen.transform, "SeatRight", new Vector2(1f, 1f), new Vector2(-114f, -216f), new Vector2(208f, 82f)));

        // Compact Human Passed-Suit Pip Strip (just above bottom hand dock on right edge)
        GameObject hPassObj = new GameObject("HumanPassedPipsRow");
        hPassObj.transform.SetParent(matchScreen.transform, false);
        humanPassedPipsRow = hPassObj.AddComponent<RectTransform>();
        humanPassedPipsRow.anchorMin = new Vector2(1f, 0f);
        humanPassedPipsRow.anchorMax = new Vector2(1f, 0f);
        humanPassedPipsRow.pivot = new Vector2(1f, 0f);
        humanPassedPipsRow.anchoredPosition = new Vector2(-24f, 502f);
        humanPassedPipsRow.sizeDelta = new Vector2(220f, 30f);
        HorizontalLayoutGroup hpHlg = hPassObj.AddComponent<HorizontalLayoutGroup>();
        hpHlg.childAlignment = TextAnchor.MiddleRight;
        hpHlg.spacing = 4f;
        hpHlg.childControlWidth = false;
        hpHlg.childControlHeight = false;
        hPassObj.SetActive(false);

        // Floating Status Banner
        GameObject statusCard = CreateCard(matchScreen.transform, "StatusBanner", new Color(0.05f, 0.16f, 0.22f, 0.90f), Vector2.zero, new Vector2(960f, 64f));
        RectTransform statusRect = statusCard.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.5f, 0f);
        statusRect.anchorMax = new Vector2(0.5f, 0f);
        statusRect.pivot = new Vector2(0.5f, 0f);
        statusRect.anchoredPosition = new Vector2(0f, 344f);
        matchStatusBanner = CreateText(statusCard.transform, "StatusText", "", 25, FontStyles.Bold, Color.white, Vector2.zero, new Vector2(920f, 54f));

        // Floating Draw / Pass Action Button
        actionButton = CreatePillButton(matchScreen.transform, "BtnDrawOrPass", "Draw Tile", RoatanVisualAssets.IslandGold, Vector2.zero, new Vector2(440f, 92f), 34, OnActionButtonClicked);
        RectTransform actRect = actionButton.GetComponent<RectTransform>();
        actRect.anchorMin = new Vector2(0.5f, 0f);
        actRect.anchorMax = new Vector2(0.5f, 0f);
        actRect.pivot = new Vector2(0.5f, 0f);
        actRect.anchoredPosition = new Vector2(0f, 424f);
        actionButtonLabel = actionButton.GetComponentInChildren<TMP_Text>();
        actionButtonLabel.color = new Color(0.10f, 0.10f, 0.12f, 1f);
        actionButton.gameObject.SetActive(false);

        // Dedicated SLAM Button (appears when a valid playable tile is selected)
        slamButton = CreatePillButton(matchScreen.transform, "BtnSlamTile", "SLAM TILE!", RoatanVisualAssets.SunsetCoral, Vector2.zero, new Vector2(310f, 84f), 30, OnSlamButtonClicked);
        RectTransform slamRect = slamButton.GetComponent<RectTransform>();
        slamRect.anchorMin = new Vector2(0.5f, 0f);
        slamRect.anchorMax = new Vector2(0.5f, 0f);
        slamRect.pivot = new Vector2(0.5f, 0f);
        slamRect.anchoredPosition = new Vector2(-120f, 424f);
        slamButton.gameObject.SetActive(false);

        // Fast-Access Anti-Cheating Table Talk / Quick-Emote Wheel Button
        emoteWheelBtn = CreatePillButton(matchScreen.transform, "BtnTableTalk", "Table Talk", new Color(0.08f, 0.28f, 0.34f, 0.96f), Vector2.zero, new Vector2(215f, 72f), 24, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            tableTalkUI?.ToggleEmoteWheel();
        });
        RectTransform emoteRect = emoteWheelBtn.GetComponent<RectTransform>();
        emoteRect.anchorMin = new Vector2(1f, 0f);
        emoteRect.anchorMax = new Vector2(1f, 0f);
        emoteRect.pivot = new Vector2(1f, 0f);
        emoteRect.anchoredPosition = new Vector2(-24f, 424f);

        // Visible 10s Turn Timer / 5s Auto-Pass Pill (left side with clean breathing room above status banner)
        humanTimerBadgeObj = CreateCard(matchScreen.transform, "HumanTurnTimerBadge", new Color(0.08f, 0.34f, 0.38f, 0.96f), Vector2.zero, new Vector2(275f, 64f));
        RectTransform timerRect = humanTimerBadgeObj.GetComponent<RectTransform>();
        timerRect.anchorMin = new Vector2(0f, 0f);
        timerRect.anchorMax = new Vector2(0f, 0f);
        timerRect.pivot = new Vector2(0f, 0f);
        timerRect.anchoredPosition = new Vector2(24f, 446f);
        humanTimerBadgeBg = humanTimerBadgeObj.GetComponent<Image>();
        humanTimerLabel = CreateText(humanTimerBadgeObj.transform, "TimerText", "TURN TIMER: 10s", 22, FontStyles.Bold, Color.white, Vector2.zero, new Vector2(255f, 52f));
        humanTimerBadgeObj.SetActive(false);

        // "Bot Auto-Playing" Avatar/HUD Indicator + Instant "Resume Control" Banner
        humanBotIndicatorObj = CreateCard(matchScreen.transform, "BotAutoPlayingBadge", new Color(0.58f, 0.22f, 0.14f, 0.96f), Vector2.zero, new Vector2(290f, 58f));
        RectTransform botIndRect = humanBotIndicatorObj.GetComponent<RectTransform>();
        botIndRect.anchorMin = new Vector2(0f, 0f);
        botIndRect.anchorMax = new Vector2(0f, 0f);
        botIndRect.pivot = new Vector2(0f, 0f);
        botIndRect.anchoredPosition = new Vector2(24f, 518f);
        humanBotIndicatorLabel = CreateText(humanBotIndicatorObj.transform, "BotIndText", "Bot Auto-Playing", 22, FontStyles.Bold, RoatanVisualAssets.IslandGold, Vector2.zero, new Vector2(270f, 46f));
        humanBotIndicatorObj.SetActive(false);

        resumeControlBtn = CreatePillButton(matchScreen.transform, "BtnResumeControl", "Resume Control (Tap Here or Any Tile)", RoatanVisualAssets.PalmGreen, Vector2.zero, new Vector2(610f, 76f), 25, ReclaimHumanControl);
        RectTransform resumeRect = resumeControlBtn.GetComponent<RectTransform>();
        resumeRect.anchorMin = new Vector2(0.5f, 0f);
        resumeRect.anchorMax = new Vector2(0.5f, 0f);
        resumeRect.pivot = new Vector2(0.5f, 0f);
        resumeRect.anchoredPosition = new Vector2(0f, 420f);
        resumeControlBtn.gameObject.SetActive(false);

        // Attach Table Talk UI (Emote Wheel, Speech Bubbles, Celebratory Banners, Call Your Padna Modal)
        tableTalkUI = matchScreen.AddComponent<RoatanTableTalkUI>();
        tableTalkUI.Initialize(matchScreen.transform, uiFont, phrase =>
        {
            UpdateStatusBanner($"You called: \"{phrase}\"");
        });

        // Attach Blocked-Board Pip Reveal Overlay
        pipRevealOverlay = matchScreen.AddComponent<RoatanBlockedPipRevealOverlay>();
        pipRevealOverlay.Initialize(matchScreen.transform, uiFont);

        // Bottom Wood-Rimmed Player Hand Tray
        GameObject handDock = new GameObject("HandDock");
        handDock.transform.SetParent(matchScreen.transform, false);
        RectTransform dockRect = handDock.AddComponent<RectTransform>();
        dockRect.anchorMin = new Vector2(0f, 0f);
        dockRect.anchorMax = new Vector2(1f, 0f);
        dockRect.pivot = new Vector2(0.5f, 0f);
        dockRect.sizeDelta = new Vector2(0f, 330f);
        dockRect.anchoredPosition = Vector2.zero;

        Image dockImg = handDock.AddComponent<Image>();
        dockImg.sprite = RoatanVisualAssets.WoodFrameSprite;

        GameObject scrollObj = new GameObject("HandScroll");
        scrollObj.transform.SetParent(handDock.transform, false);
        RectTransform scrollRectTransform = scrollObj.AddComponent<RectTransform>();
        scrollRectTransform.anchorMin = Vector2.zero;
        scrollRectTransform.anchorMax = Vector2.one;
        scrollRectTransform.offsetMin = new Vector2(18f, 16f);
        scrollRectTransform.offsetMax = new Vector2(-18f, -16f);
        scrollObj.AddComponent<RectMask2D>();

        ScrollRect scroll = scrollObj.AddComponent<ScrollRect>();
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Elastic;

        GameObject contentObj = new GameObject("HandContent");
        contentObj.transform.SetParent(scrollObj.transform, false);
        handContainer = contentObj.AddComponent<RectTransform>();
        handContainer.anchorMin = new Vector2(0.5f, 0.5f);
        handContainer.anchorMax = new Vector2(0.5f, 0.5f);
        handContainer.pivot = new Vector2(0.5f, 0.5f);
        handContainer.sizeDelta = new Vector2(960f, 290f);

        HorizontalLayoutGroup hlg = contentObj.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 14f;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.padding = new RectOffset(20, 20, 10, 10);

        ContentSizeFitter fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        scroll.content = handContainer;
        scroll.viewport = scrollRectTransform;

        matchScreen.SetActive(false);
    }

    private void BuildHowToPlayModal()
    {
        howToPlayModal = CreateStretchPanel(safeAreaRoot, "HowToPlayModal", new Color(0f, 0f, 0f, 0.78f));

        GameObject card = CreateCard(howToPlayModal.transform, "RulesCard", RoatanVisualAssets.DarkSlate, Vector2.zero, new Vector2(940f, 1140f));
        CreateText(card.transform, "RulesTitle", "HOW TO PLAY — ROATAN RULES", 38, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, 488f), new Vector2(860f, 60f));

        // 4 Rule Tabs: Six-Lov | Two-Best | Cut-Throat | Game Bruk
        string[] tabs = { "Six-Lov", "Two-Best", "Cut-Throat", "Game Bruk" };
        float[] tabXs = { -324f, -108f, 108f, 324f };
        for (int i = 0; i < tabs.Length; i++)
        {
            string tabName = tabs[i];
            Button tb = CreatePillButton(card.transform, $"RulesTab_{tabName}", tabName, new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(tabXs[i], 392f), new Vector2(206f, 68f), 23, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                SelectRulesTab(tabName);
            });
            rulesTabImages[tabName] = tb.GetComponent<Image>();
        }

        GameObject bodyCard = CreateCard(card.transform, "RulesBodyCard", new Color(0.06f, 0.16f, 0.22f, 0.92f), new Vector2(0f, -15f), new Vector2(860f, 690f));
        rulesBodyLabel = CreateText(bodyCard.transform, "RulesBody", "", 27, FontStyles.Normal, Color.white, Vector2.zero, new Vector2(800f, 630f));
        rulesBodyLabel.alignment = TextAlignmentOptions.TopLeft;

        Button gotItBtn = CreatePillButton(card.transform, "BtnCloseRules", RoatanLocalization.T("got_it"), RoatanVisualAssets.PalmGreen, new Vector2(0f, -468f), new Vector2(380f, 92f), 34, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            howToPlayModal.SetActive(false);
        });
        rulesGotItLabel = gotItBtn.GetComponentInChildren<TMP_Text>();

        SelectRulesTab("Six-Lov");
        howToPlayModal.SetActive(false);
    }

    private void SelectRulesTab(string tabName)
    {
        activeRulesTab = tabName;
        foreach (KeyValuePair<string, Image> kv in rulesTabImages)
        {
            bool active = kv.Key == tabName;
            kv.Value.color = active ? RoatanVisualAssets.IslandGold : new Color(0.14f, 0.22f, 0.28f, 1f);
            TMP_Text lbl = kv.Value.GetComponentInChildren<TMP_Text>();
            if (lbl != null)
            {
                lbl.color = active ? new Color(0.08f, 0.10f, 0.12f, 1f) : Color.white;
            }
        }

        if (rulesBodyLabel == null) return;
        switch (tabName)
        {
            case "Six-Lov":
                rulesBodyLabel.text =
                    "<b>SIX-LOV (2v2 PARTNERS)</b>\n\n" +
                    "• <b>Goal:</b> First partnership to win <b>6 consecutive hands (6–0)</b> wins the match.\n\n" +
                    "• <b>Call Your Padna:</b> After winning a hand, choose <b>Pose Myself</b> or <b>Pass Pose to Padna</b> within 8 seconds. The chosen poser opens the next hand with any tile.\n\n" +
                    "• <b>1-1 Playoff Toggle:</b> Optional lobby rule where a trailing win at 1–0 moves to 1–1; the winner of 1–1 jumps straight to 2–0.\n\n" +
                    "• <b>Controls:</b> Tap a glowing tile to place, flick down or tap <b>SLAM</b> for a heavy table impact, and drag/pinch to pan or zoom.";
                break;
            case "Two-Best":
                rulesBodyLabel.text =
                    "<b>TWO-BEST (QUICK BEST OF 3)</b>\n\n" +
                    "• <b>Players:</b> Play <b>1v1</b> head-to-head or <b>2v2</b> partners.\n\n" +
                    "• <b>Goal:</b> First player or pair to reach <b>2 hand wins</b> wins the match.\n\n" +
                    "• <b>No Break Rule:</b> Wins do not have to be consecutive — a 1–1 tie goes straight to a deciding 3rd hand!\n\n" +
                    "• <b>1v1 Dealing:</b> Each player gets 7 tiles (never 14). In Blocked mode the remaining 14 stay face-down; in Draw mode they form the boneyard.";
                break;
            case "Cut-Throat":
                rulesBodyLabel.text =
                    "<b>CUT-THROAT (FREE-FOR-ALL)</b>\n\n" +
                    "• <b>Players:</b> 2, 3, or 4 players — every player for themselves (no partners).\n\n" +
                    "• <b>Goal:</b> First individual player to reach <b>6 hand wins</b> wins and \"Cut-Throats\" anyone still at 0 wins!\n\n" +
                    "• <b>3-Player Blocked Deck:</b> The [0|0] double blank is removed (27 tiles), dealing 9 tiles each.\n\n" +
                    "• <b>Cut-Throat Break:</b> Wins keep stacking while at least one player sits at 0. Once every player scores 1+, all scores reset to 0!";
                break;
            default:
                rulesBodyLabel.text =
                    "<b>GAME BRUK (THE BREAK) & TABLE RULES</b>\n\n" +
                    "• <b>Six-Lov Break:</b> If the trailing team wins while opponents hold 1–5 points, <b>Game Bruk!</b> triggers — scores reset to 0–0 and [6|6] must pose next.\n\n" +
                    "• <b>Blocked Board (Trancado):</b> All hands flip face-up! Lowest individual pip count wins. Opposing ties are a Draw (0 pts, same poser repeats).\n\n" +
                    "• <b>Pass-Suit Icons:</b> Small muted pip badges under each player's nameplate track suits they have already passed on this hand.\n\n" +
                    "• <b>Timers:</b> 10s turn timer (3 strikes = Bot Auto-Play, tap any tile to reclaim) and 5s no-penalty Auto-Pass.";
                break;
        }
    }

    private void BuildRoundSummaryOverlay()
    {
        roundSummaryOverlay = CreateStretchPanel(safeAreaRoot, "RoundSummaryOverlay", new Color(0f, 0f, 0f, 0.76f));

        GameObject card = CreateCard(roundSummaryOverlay.transform, "SummaryCard", RoatanVisualAssets.DarkSlate, Vector2.zero, new Vector2(920f, 920f));
        summaryTitleLabel = CreateText(card.transform, "SummaryTitle", "ROUND COMPLETE", 44, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, 365f), new Vector2(840f, 74f));
        summaryBodyLabel = CreateText(card.transform, "SummaryBody", "", 26, FontStyles.Normal, Color.white, new Vector2(0f, 95f), new Vector2(820f, 410f));
        summaryBodyLabel.alignment = TextAlignmentOptions.MidlineLeft;

        // Primary CTA: "Next Round" (normal hand) or "Rematch" (Break / Match Over)
        summaryNextBtn = CreatePillButton(card.transform, "BtnNextRound", "Next Round", RoatanVisualAssets.PalmGreen, new Vector2(0f, -180f), new Vector2(560f, 96f), 34, BeginRound);
        summaryNextBtnLabel = summaryNextBtn.GetComponentInChildren<TMP_Text>();

        // Secondary CTA (shown ONLY on Break / Match Over): "New Match" -> returns to match/lobby config
        summaryNewMatchBtn = CreatePillButton(card.transform, "BtnSummaryNewMatch", "New Match", RoatanVisualAssets.BrightLagoon, new Vector2(0f, -292f), new Vector2(500f, 84f), 30, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            ShowHomeScreen();
        });
        summaryNewMatchBtnLabel = summaryNewMatchBtn.GetComponentInChildren<TMP_Text>();
        summaryNewMatchBtn.gameObject.SetActive(false);

        // Leave button (always visible)
        Button menuBtn = CreatePillButton(card.transform, "BtnSummaryMenu", "Leave", new Color(0.24f, 0.20f, 0.24f, 1f), new Vector2(0f, -396f), new Vector2(400f, 78f), 28, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            ShowHomeScreen();
        });
        summaryMenuBtnLabel = menuBtn.GetComponentInChildren<TMP_Text>();

        roundSummaryOverlay.SetActive(false);
    }

    private OpponentSeatUI CreateOpponentSeat(Transform parent, string name, Vector2 anchor, Vector2 anchoredPos, Vector2 size)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPos;

        Image bg = obj.AddComponent<Image>();
        bg.sprite = RoatanVisualAssets.RoundedCardSprite;
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.06f, 0.17f, 0.22f, 0.88f);
        bg.raycastTarget = false;

        GameObject ringObj = new GameObject("TurnRing");
        ringObj.transform.SetParent(obj.transform, false);
        RectTransform ringRect = ringObj.AddComponent<RectTransform>();
        ringRect.anchorMin = Vector2.zero;
        ringRect.anchorMax = Vector2.one;
        ringRect.offsetMin = Vector2.zero;
        ringRect.offsetMax = Vector2.zero;
        Image ringImg = ringObj.AddComponent<Image>();
        ringImg.sprite = RoatanVisualAssets.RoundedOutlineSprite;
        ringImg.type = Image.Type.Sliced;
        ringImg.color = new Color(1f, 1f, 1f, 0.15f);
        ringImg.raycastTarget = false;

        TMP_Text nameTxt = CreateText(obj.transform, "Name", "Bot", 22, FontStyles.Bold, Color.white, new Vector2(0f, size.y * 0.24f), new Vector2(size.x - 14f, 28f));
        nameTxt.raycastTarget = false;
        TMP_Text countTxt = CreateText(obj.transform, "Count", "7 tiles", 20, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(-size.x * 0.22f, -size.y * 0.20f), new Vector2(size.x * 0.52f, 26f));
        countTxt.raycastTarget = false;
        TMP_Text scoreTxt = CreateText(obj.transform, "Score", "0/6 W", 19, FontStyles.Normal, new Color(0.78f, 0.90f, 0.94f), new Vector2(size.x * 0.24f, -size.y * 0.20f), new Vector2(size.x * 0.44f, 26f));
        scoreTxt.raycastTarget = false;

        // Compact muted Pass-Suit Pip Row directly beneath each nameplate
        GameObject pipsRowObj = new GameObject("PassedPipsRow");
        pipsRowObj.transform.SetParent(obj.transform, false);
        RectTransform pipsRect = pipsRowObj.AddComponent<RectTransform>();
        pipsRect.anchorMin = new Vector2(0.5f, 0f);
        pipsRect.anchorMax = new Vector2(0.5f, 0f);
        pipsRect.pivot = new Vector2(0.5f, 1f);
        pipsRect.anchoredPosition = new Vector2(0f, -5f);
        pipsRect.sizeDelta = new Vector2(size.x + 12f, 28f);

        HorizontalLayoutGroup pipsHlg = pipsRowObj.AddComponent<HorizontalLayoutGroup>();
        pipsHlg.childAlignment = TextAnchor.MiddleCenter;
        pipsHlg.spacing = 4f;
        pipsHlg.childControlWidth = false;
        pipsHlg.childControlHeight = false;
        pipsRowObj.SetActive(false);

        return new OpponentSeatUI
        {
            Root = obj,
            Background = bg,
            TurnRing = ringImg,
            NameText = nameTxt,
            CountText = countTxt,
            ScoreText = scoreTxt,
            PassedPipsRow = pipsRect
        };
    }

    private Button CreateModeCard(
        Transform parent,
        string name,
        string title,
        string desc,
        Vector2 pos,
        UnityEngine.Events.UnityAction onClick,
        out TMP_Text titleText,
        out TMP_Text descText)
    {
        GameObject card = CreateCard(parent, name, new Color(0.08f, 0.21f, 0.27f, 0.92f), pos, new Vector2(920f, 152f));
        Button btn = card.AddComponent<Button>();
        btn.targetGraphic = card.GetComponent<Image>();
        btn.onClick.AddListener(onClick);

        GameObject border = new GameObject("Border");
        border.transform.SetParent(card.transform, false);
        RectTransform bRect = border.AddComponent<RectTransform>();
        bRect.anchorMin = Vector2.zero;
        bRect.anchorMax = Vector2.one;
        bRect.offsetMin = Vector2.zero;
        bRect.offsetMax = Vector2.zero;
        Image bImg = border.AddComponent<Image>();
        bImg.sprite = RoatanVisualAssets.RoundedOutlineSprite;
        bImg.type = Image.Type.Sliced;
        bImg.color = new Color(0.95f, 0.74f, 0.24f, 0.55f);
        bImg.raycastTarget = false;

        titleText = CreateText(card.transform, "Title", title, 34, FontStyles.Bold, Color.white, new Vector2(0f, 28f), new Vector2(850f, 48f));
        titleText.raycastTarget = false;
        descText = CreateText(card.transform, "Desc", desc, 23, FontStyles.Normal, new Color(0.84f, 0.94f, 0.96f), new Vector2(0f, -26f), new Vector2(850f, 48f));
        descText.raycastTarget = false;
        return btn;
    }

    private GameObject CreateStretchPanel(Transform parent, string name, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        if (color.a > 0.001f)
        {
            Image img = obj.AddComponent<Image>();
            img.color = color;
        }
        return obj;
    }

    private GameObject CreateCard(Transform parent, string name, Color color, Vector2 pos, Vector2 size)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        Image img = obj.AddComponent<Image>();
        img.sprite = RoatanVisualAssets.RoundedCardSprite;
        img.type = Image.Type.Sliced;
        img.color = color;
        return obj;
    }

    private Button CreatePillButton(Transform parent, string name, string label, Color color, Vector2 pos, Vector2 size, int fontSize, UnityEngine.Events.UnityAction onClick)
    {
        GameObject obj = CreateCard(parent, name, color, pos, size);
        Button btn = obj.AddComponent<Button>();
        btn.targetGraphic = obj.GetComponent<Image>();
        btn.onClick.AddListener(onClick);

        TMP_Text txt = CreateText(obj.transform, "Label", label, fontSize, FontStyles.Bold, Color.white, Vector2.zero, size - new Vector2(20f, 10f));
        txt.raycastTarget = false;
        return btn;
    }

    private TMP_InputField CreateInputField(Transform parent, string name, string placeholder, string initialValue, Vector2 pos, Vector2 size, bool isPassword = false)
    {
        GameObject obj = CreateCard(parent, name, new Color(0.04f, 0.11f, 0.16f, 1f), pos, size);

        GameObject textArea = new GameObject("TextArea");
        textArea.transform.SetParent(obj.transform, false);
        RectTransform taRect = textArea.AddComponent<RectTransform>();
        taRect.anchorMin = Vector2.zero;
        taRect.anchorMax = Vector2.one;
        taRect.offsetMin = new Vector2(18f, 6f);
        taRect.offsetMax = new Vector2(-18f, -6f);
        textArea.AddComponent<RectMask2D>();

        TMP_Text ph = CreateText(textArea.transform, "Placeholder", placeholder, 22, FontStyles.Italic, new Color(0.55f, 0.68f, 0.74f, 0.85f), Vector2.zero, Vector2.zero);
        RectTransform phRect = ph.GetComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = Vector2.zero;
        phRect.offsetMax = Vector2.zero;
        ph.alignment = TextAlignmentOptions.MidlineLeft;
        ph.enableWordWrapping = false;

        TMP_Text txt = CreateText(textArea.transform, "Text", "", 22, FontStyles.Normal, Color.white, Vector2.zero, Vector2.zero);
        RectTransform tRect = txt.GetComponent<RectTransform>();
        tRect.anchorMin = Vector2.zero;
        tRect.anchorMax = Vector2.one;
        tRect.offsetMin = Vector2.zero;
        tRect.offsetMax = Vector2.zero;
        txt.alignment = TextAlignmentOptions.MidlineLeft;
        txt.enableWordWrapping = false;

        TMP_InputField input = obj.AddComponent<TMP_InputField>();
        input.textViewport = taRect;
        input.textComponent = txt;
        input.placeholder = ph;
        input.contentType = isPassword ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
        input.text = initialValue ?? string.Empty;

        return input;
    }

    private TMP_Text CreateText(Transform parent, string name, string content, int fontSize, FontStyles style, Color color, Vector2 pos, Vector2 size)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        if (uiFont != null)
        {
            tmp.font = uiFont;
        }
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        return tmp;
    }
}
