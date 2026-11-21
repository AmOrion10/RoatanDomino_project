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

    public static RoatanMobileApp Instance { get; private set; }

    private TMP_FontAsset uiFont;
    private Canvas rootCanvas;
    private RectTransform safeAreaRoot;

    // Screens
    private GameObject homeScreen;
    private GameObject matchScreen;
    private GameObject howToPlayModal;
    private GameObject roundSummaryOverlay;
    private RoatanOnlineController onlineController;

    // Home UI refs
    private TMP_Text subtitleLabel;
    private TMP_Text statsLabel;
    private TMP_Text modeHeadingLabel;
    private TMP_Text cerradoTitleLabel;
    private TMP_Text cerradoDescLabel;
    private TMP_Text drawTitleLabel;
    private TMP_Text drawDescLabel;
    private TMP_Text diffHeadingLabel;
    private TMP_Text diffDescLabel;
    private TMP_Text formatDescLabel;
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

    private readonly Dictionary<RoatanGameMode, Image> modeCardImages = new Dictionary<RoatanGameMode, Image>();
    private readonly Dictionary<MatchFormat, Image> formatBtnImages = new Dictionary<MatchFormat, Image>();
    private readonly Dictionary<BotDifficulty, Image> diffBtnImages = new Dictionary<BotDifficulty, Image>();
    private readonly Dictionary<BotDifficulty, TMP_Text> diffBtnLabels = new Dictionary<BotDifficulty, TMP_Text>();
    private readonly Dictionary<int, Image> botCountBtnImages = new Dictionary<int, Image>();
    private readonly Dictionary<int, GameObject> botCountBtnObjs = new Dictionary<int, GameObject>();
    private readonly Dictionary<RoatanLanguage, Image> langBtnImages = new Dictionary<RoatanLanguage, Image>();

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

    // Opponent seat badges (Top = index 0, Left = index 1, Right = index 2)
    private readonly List<OpponentSeatUI> opponentSeats = new List<OpponentSeatUI>();

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
        RoatanVisualAssets.EnsureInitialized();
        gameObject.AddComponent<RoatanSoundFX>();
        uiFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        EnsureEventSystem();
        BuildRootCanvas();
        BuildHomeScreen();
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
        matchScreen.SetActive(false);
        howToPlayModal.SetActive(false);
        roundSummaryOverlay.SetActive(false);
    }

    private void OnDestroy()
    {
        RoatanLocalization.LanguageChanged -= OnLanguageChanged;
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
        tableTalkUI?.HideAllOverlays();
        pipRevealOverlay?.HideImmediate();
        rootCanvas.gameObject.SetActive(true);
        onlineController?.HideOnline();
        homeScreen.SetActive(true);
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
        StopMatchRoutine();
        ChatManager.Instance?.SetActiveRoundChatLocked(true);
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
        tableTalkUI?.HideAllOverlays();
        pipRevealOverlay?.HideImmediate();
        selectedHandDomino = null;
        slamArmed = false;
        if (slamButton != null) slamButton.gameObject.SetActive(false);
        waitingForHumanInput = false;
        currentHumanMoves.Clear();
        botBrain.ResetForRound();

        boardView.ClearBoard();
        RoatanMatchEngine.PlacedTile opener = rematchAfterBreak
            ? engine.StartRematchAfterBreak()
            : engine.StartNewRound();
        RoatanMatchEngine.MatchPlayer starter = engine.Players.First(p => p.Id == opener.PlayerId);

        boardView.RebuildFromEngine(engine, animateLastTile: true);
        RoatanSoundFX.Instance?.PlayPlace();

        RefreshMatchHeader();
        RefreshOpponentSeats();
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
        homeScreen.SetActive(false);
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
                    engine.PassTurn();
                    RoatanSoundFX.Instance?.PlayPass();
                    ShowBotEmoteBubble(active, "Pass!");
                    UpdateStatusBanner($"{active.Name} passed (Paso!)");
                }
            }
            else
            {
                botBrain.RecordBlocked(active.Id, engine.LeftEndPip, engine.RightEndPip);
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
            tableTalkUI?.ShowCelebrationBanner(
                "GAME BRUK! (BREAK)",
                $"{result.Winner?.Name} broke the streak! Scores reset to 0 — [6|6] poses next",
                new Color(0.56f, 0.16f, 0.14f, 0.96f),
                2.2f);
            yield return new WaitForSeconds(1.8f);
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
            sb.AppendLine($"{reason}   •   +{result.PointsAwarded} hand win to {result.Winner.Name}");
            if (result.CutThroatedPlayers != null && result.CutThroatedPlayers.Count > 0)
            {
                string victims = string.Join(", ", result.CutThroatedPlayers.Select(p => p.Name));
                sb.AppendLine($"Cut-Throated at 0: {victims}");
            }
        }
        sb.AppendLine();
        foreach (RoatanMatchEngine.MatchPlayer p in result.Standings)
        {
            string tag = (!result.IsDraw && result.Winner != null && p.Id == result.Winner.Id) ? "★ " : "   ";
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
                          : seatSlot == 1 ? new Vector2(-240f, 190f)
                          : new Vector2(240f, 190f);

        tableTalkUI.ShowSpeechBubble(bot.Name, phrase, bubblePos);
    }

    private void StopMatchRoutine()
    {
        StopHumanTurnTimer();
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
        if (modeHeadingLabel != null) modeHeadingLabel.text = RoatanLocalization.T("choose_mode");
        if (cerradoTitleLabel != null) cerradoTitleLabel.text = RoatanLocalization.T("mode_cerrado_title");
        if (cerradoDescLabel != null) cerradoDescLabel.text = RoatanLocalization.T("mode_cerrado_desc");
        if (drawTitleLabel != null) drawTitleLabel.text = RoatanLocalization.T("mode_draw_title");
        if (drawDescLabel != null) drawDescLabel.text = RoatanLocalization.T("mode_draw_desc");
        if (diffHeadingLabel != null) diffHeadingLabel.text = RoatanLocalization.T("bot_difficulty");
        if (playNowBtnLabel != null) playNowBtnLabel.text = RoatanLocalization.T("play_now");
        if (howToPlayBtnLabel != null) howToPlayBtnLabel.text = RoatanLocalization.T("how_to_play");
        if (onlineModeBtnLabel != null) onlineModeBtnLabel.text = RoatanLocalization.T("online_mode");
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

        if (formatDescLabel != null)
        {
            switch (selectedFormat)
            {
                case MatchFormat.SIX_LOV:
                    formatDescLabel.text = selectedOneOnePlayoff
                        ? "Six-Lov (2v2): First pair to 6-0 wins! 1-1 Playoff ON (1-1 -> 2-0, Break from 2-0+)."
                        : "Six-Lov (2v2): First pair to 6 consecutive wins (6-0)! Trailing win triggers Game Bruk.";
                    break;
                case MatchFormat.TWO_BEST:
                    formatDescLabel.text = "Two-Best (Best of 3): First player or pair to 2 hand wins! No Break rule.";
                    break;
                default:
                    formatDescLabel.text = "Cut-Throat (FFA): Individual race to 6 wins! Break triggers if everyone scores >= 1.";
                    break;
            }
        }

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

        foreach (KeyValuePair<RoatanLanguage, Image> kv in langBtnImages)
        {
            kv.Value.color = kv.Key == RoatanLocalization.CurrentLanguage
                ? RoatanVisualAssets.IslandGold
                : new Color(0.10f, 0.24f, 0.30f, 0.95f);
            TMP_Text lbl = kv.Value.GetComponentInChildren<TMP_Text>();
            if (lbl != null)
            {
                lbl.color = kv.Key == RoatanLocalization.CurrentLanguage
                    ? new Color(0.08f, 0.10f, 0.12f, 1f)
                    : Color.white;
            }
        }

        // Enforce valid player counts per Match Format
        if (selectedFormat == MatchFormat.SIX_LOV)
        {
            selectedBotCount = 3;
            opponentsHeaderLabel.text = "PLAYERS  •  2v2 PARTNERS";
            opponentsRowObj.SetActive(false);
            if (fixed2v2NoteLabel != null)
            {
                fixed2v2NoteLabel.gameObject.SetActive(true);
                fixed2v2NoteLabel.text = "You & Patrik (Padna)  vs  Anthony & Astor";
            }
        }
        else if (selectedFormat == MatchFormat.TWO_BEST)
        {
            if (selectedBotCount == 2) selectedBotCount = 1;
            opponentsHeaderLabel.text = "MATCH SIZE (1v1 OR 2v2)";
            opponentsRowObj.SetActive(true);
            if (fixed2v2NoteLabel != null) fixed2v2NoteLabel.gameObject.SetActive(false);
            if (botCountBtnObjs.ContainsKey(1)) botCountBtnObjs[1].SetActive(true);
            if (botCountBtnObjs.ContainsKey(2)) botCountBtnObjs[2].SetActive(false);
            if (botCountBtnObjs.ContainsKey(3)) botCountBtnObjs[3].SetActive(true);
        }
        else
        {
            opponentsHeaderLabel.text = RoatanLocalization.T("opponents");
            opponentsRowObj.SetActive(true);
            if (fixed2v2NoteLabel != null) fixed2v2NoteLabel.gameObject.SetActive(false);
            if (botCountBtnObjs.ContainsKey(1)) botCountBtnObjs[1].SetActive(true);
            if (botCountBtnObjs.ContainsKey(2)) botCountBtnObjs[2].SetActive(true);
            if (botCountBtnObjs.ContainsKey(3)) botCountBtnObjs[3].SetActive(true);
        }

        foreach (KeyValuePair<int, Image> kv in botCountBtnImages)
        {
            kv.Value.color = kv.Key == selectedBotCount ? RoatanVisualAssets.BrightLagoon : new Color(0.14f, 0.22f, 0.28f, 1f);
        }

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

        int wins = PlayerPrefs.GetInt(PrefWinsKey, 0);
        int games = PlayerPrefs.GetInt(PrefGamesKey, 0);
        statsLabel.text = $"Offline Record: {wins} Wins  •  {games} Matches";
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
            seat.Background.color = isTurn ? new Color(0.12f, 0.36f, 0.38f, 0.96f) : new Color(0.06f, 0.17f, 0.22f, 0.90f);
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

        // Compact Language Switcher Row at very top (EN default, ES, FR, DE)
        RoatanLanguage[] langs = { RoatanLanguage.English, RoatanLanguage.Spanish, RoatanLanguage.French, RoatanLanguage.German };
        float[] langXs = { -210f, -70f, 70f, 210f };
        for (int i = 0; i < langs.Length; i++)
        {
            RoatanLanguage lang = langs[i];
            Button lb = CreatePillButton(homeScreen.transform, $"Lang_{lang}", RoatanLocalization.CodeFor(lang), new Color(0.10f, 0.24f, 0.30f, 0.95f), new Vector2(langXs[i], 865f), new Vector2(124f, 52f), 24, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                RoatanLocalization.SetLanguage(lang);
            });
            langBtnImages[lang] = lb.GetComponent<Image>();
        }

        // Top Logo Banner (compact & crisp)
        GameObject headerCard = CreateCard(homeScreen.transform, "HeaderBanner", new Color(0.05f, 0.19f, 0.25f, 0.92f), new Vector2(0f, 715f), new Vector2(960f, 215f));

        GameObject leftDom = new GameObject("LogoDomLeft");
        leftDom.transform.SetParent(headerCard.transform, false);
        RectTransform lRect = leftDom.AddComponent<RectTransform>();
        lRect.sizeDelta = new Vector2(72f, 144f);
        lRect.anchoredPosition = new Vector2(-375f, 4f);
        lRect.localRotation = Quaternion.Euler(0f, 0f, 16f);
        leftDom.AddComponent<Image>().sprite = RoatanVisualAssets.GetDominoSprite(6, 6);

        GameObject rightDom = new GameObject("LogoDomRight");
        rightDom.transform.SetParent(headerCard.transform, false);
        RectTransform rRect = rightDom.AddComponent<RectTransform>();
        rRect.sizeDelta = new Vector2(72f, 144f);
        rRect.anchoredPosition = new Vector2(375f, 4f);
        rRect.localRotation = Quaternion.Euler(0f, 0f, -16f);
        rightDom.AddComponent<Image>().sprite = RoatanVisualAssets.GetDominoSprite(5, 3);

        CreateText(headerCard.transform, "Title", "ROATAN DOMINO", 58, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, 46f), new Vector2(640f, 74f));
        subtitleLabel = CreateText(headerCard.transform, "Subtitle", RoatanLocalization.T("subtitle"), 25, FontStyles.Normal, new Color(0.84f, 0.95f, 0.96f), new Vector2(0f, -12f), new Vector2(660f, 40f));
        statsLabel = CreateText(headerCard.transform, "Stats", "Offline Record: 0 Wins  •  0 Matches", 23, FontStyles.Bold, Color.white, new Vector2(0f, -62f), new Vector2(620f, 36f));

        // 1. Table Style (Side-by-Side: Block Cerrado | Classic Draw)
        modeHeadingLabel = CreateText(homeScreen.transform, "ModeHeading", "TABLE STYLE", 26, FontStyles.Bold, new Color(0.95f, 0.88f, 0.65f), new Vector2(0f, 572f), new Vector2(920f, 38f));

        Button cerrBtn = CreateCompactModeCard(
            homeScreen.transform,
            "CardCerrado",
            "Block (Cerrado)",
            "No drawing • Pure strategy",
            new Vector2(-240f, 492f),
            new Vector2(460f, 110f),
            () => { RoatanSoundFX.Instance?.PlayTap(); selectedMode = RoatanGameMode.BlockedCerrado; RefreshHomeSelections(); },
            out cerradoTitleLabel,
            out cerradoDescLabel);
        modeCardImages[RoatanGameMode.BlockedCerrado] = cerrBtn.GetComponent<Image>();

        Button drawBtn = CreateCompactModeCard(
            homeScreen.transform,
            "CardClassicDraw",
            "Classic Draw",
            "Draw from boneyard",
            new Vector2(240f, 492f),
            new Vector2(460f, 110f),
            () => { RoatanSoundFX.Instance?.PlayTap(); selectedMode = RoatanGameMode.ClassicDraw; RefreshHomeSelections(); },
            out drawTitleLabel,
            out drawDescLabel);
        modeCardImages[RoatanGameMode.ClassicDraw] = drawBtn.GetComponent<Image>();

        // 2. Match Format Selector (Six-Lov 2v2 | Two-Best | Cut-Throat)
        CreateText(homeScreen.transform, "FormatHeading", "MATCH FORMAT", 26, FontStyles.Bold, new Color(0.95f, 0.88f, 0.65f), new Vector2(0f, 396f), new Vector2(920f, 38f));

        MatchFormat[] formats = { MatchFormat.SIX_LOV, MatchFormat.TWO_BEST, MatchFormat.CUT_THROAT };
        string[] fmtLabels = { "Six-Lov\n(2v2 • 6-0)", "Two-Best\n(Best of 3)", "Cut-Throat\n(FFA • 6 W)" };
        float[] triXs = { -310f, 0f, 310f };
        for (int i = 0; i < formats.Length; i++)
        {
            MatchFormat fmt = formats[i];
            Button fb = CreatePillButton(homeScreen.transform, $"BtnFmt_{fmt}", fmtLabels[i], new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(triXs[i], 314f), new Vector2(292f, 96f), 25, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                selectedFormat = fmt;
                RefreshHomeSelections();
            });
            formatBtnImages[fmt] = fb.GetComponent<Image>();
        }

        formatDescLabel = CreateText(homeScreen.transform, "FormatDesc", "", 22, FontStyles.Italic, new Color(0.88f, 0.96f, 0.98f), new Vector2(0f, 224f), new Vector2(920f, 54f));

        oneOnePlayoffBtn = CreatePillButton(homeScreen.transform, "BtnOneOnePlayoff", "1-1 Playoff Rule: OFF", new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(0f, 152f), new Vector2(460f, 58f), 24, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            selectedOneOnePlayoff = !selectedOneOnePlayoff;
            RefreshHomeSelections();
        });
        oneOnePlayoffBtnImg = oneOnePlayoffBtn.GetComponent<Image>();
        oneOnePlayoffBtnLabel = oneOnePlayoffBtn.GetComponentInChildren<TMP_Text>();

        // 3. Bot Difficulty Section
        diffHeadingLabel = CreateText(homeScreen.transform, "DiffHeading", RoatanLocalization.T("bot_difficulty"), 26, FontStyles.Bold, new Color(0.95f, 0.88f, 0.65f), new Vector2(0f, 66f), new Vector2(920f, 38f));

        BotDifficulty[] diffs = { BotDifficulty.Beginner, BotDifficulty.Novice, BotDifficulty.Pro };
        for (int i = 0; i < diffs.Length; i++)
        {
            BotDifficulty d = diffs[i];
            Button b = CreatePillButton(homeScreen.transform, $"BtnDiff_{d}", RoatanLocalization.DifficultyLabel(d), new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(triXs[i], -12f), new Vector2(285f, 82f), 30, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                selectedDifficulty = d;
                RefreshHomeSelections();
            });
            diffBtnImages[d] = b.GetComponent<Image>();
            diffBtnLabels[d] = b.GetComponentInChildren<TMP_Text>();
        }

        diffDescLabel = CreateText(homeScreen.transform, "DiffDesc", "", 22, FontStyles.Italic, new Color(0.88f, 0.96f, 0.98f), new Vector2(0f, -86f), new Vector2(920f, 52f));

        // 4. Opponents / Match Size Section
        opponentsHeaderLabel = CreateText(homeScreen.transform, "OppHeading", RoatanLocalization.T("opponents"), 25, FontStyles.Bold, new Color(0.95f, 0.88f, 0.65f), new Vector2(0f, -162f), new Vector2(940f, 38f));

        fixed2v2NoteLabel = CreateText(homeScreen.transform, "Fixed2v2Note", "You & Patrik (Padna)  vs  Anthony & Astor", 26, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, -236f), new Vector2(920f, 64f));

        opponentsRowObj = new GameObject("OpponentsRow");
        opponentsRowObj.transform.SetParent(homeScreen.transform, false);
        RectTransform oppRowRect = opponentsRowObj.AddComponent<RectTransform>();
        oppRowRect.anchoredPosition = new Vector2(0f, -238f);
        oppRowRect.sizeDelta = new Vector2(940f, 86f);

        int[] botCounts = { 1, 2, 3 };
        string[] botLabels = { "1v1 (1 Bot)", "3P (2 Bots)", "4P (3 Bots)" };
        for (int i = 0; i < botCounts.Length; i++)
        {
            int c = botCounts[i];
            Button b = CreatePillButton(opponentsRowObj.transform, $"BtnBots_{c}", botLabels[i], new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(triXs[i], 0f), new Vector2(285f, 78f), 26, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                selectedBotCount = c;
                RefreshHomeSelections();
            });
            botCountBtnImages[c] = b.GetComponent<Image>();
            botCountBtnObjs[c] = b.gameObject;
        }

        // Big Play CTA
        Button playBtn = CreatePillButton(homeScreen.transform, "BtnPlayNow", RoatanLocalization.T("play_now"), RoatanVisualAssets.PalmGreen, new Vector2(0f, -430f), new Vector2(720f, 122f), 46, StartNewMatch);
        playNowBtnLabel = playBtn.GetComponentInChildren<TMP_Text>();

        // Bottom Utility Row
        Button soundBtn = CreatePillButton(homeScreen.transform, "BtnHomeSound", RoatanLocalization.T("sound_on"), new Color(0.08f, 0.20f, 0.26f, 0.95f), new Vector2(-315f, -640f), new Vector2(280f, 78f), 26, () =>
        {
            RoatanSoundFX.Instance?.ToggleMute();
            RoatanSoundFX.Instance?.PlayTap();
            RefreshSoundLabels();
        });
        homeSoundBtnLabel = soundBtn.GetComponentInChildren<TMP_Text>();

        Button howBtn = CreatePillButton(homeScreen.transform, "BtnHowToPlay", RoatanLocalization.T("how_to_play"), new Color(0.08f, 0.20f, 0.26f, 0.95f), new Vector2(0f, -640f), new Vector2(280f, 78f), 26, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            howToPlayModal.SetActive(true);
        });
        howToPlayBtnLabel = howBtn.GetComponentInChildren<TMP_Text>();

        Button onlineBtn = CreatePillButton(homeScreen.transform, "BtnOnlineLogin", RoatanLocalization.T("online_mode"), new Color(0.08f, 0.20f, 0.26f, 0.95f), new Vector2(315f, -640f), new Vector2(280f, 78f), 26, OpenOnlineScreen);
        onlineModeBtnLabel = onlineBtn.GetComponentInChildren<TMP_Text>();
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
        boardView.EndSlotClicked += OnBoardEndSlotClicked;

        // Opponent Seats
        opponentSeats.Add(CreateOpponentSeat(matchScreen.transform, "SeatTop", new Vector2(0.5f, 1f), new Vector2(0f, -172f), new Vector2(310f, 92f)));
        opponentSeats.Add(CreateOpponentSeat(matchScreen.transform, "SeatLeft", new Vector2(0f, 0.56f), new Vector2(122f, 0f), new Vector2(215f, 118f)));
        opponentSeats.Add(CreateOpponentSeat(matchScreen.transform, "SeatRight", new Vector2(1f, 0.56f), new Vector2(-122f, 0f), new Vector2(215f, 118f)));

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

        // Visible 10s Turn Timer / 5s Auto-Pass Pill (left side above status banner)
        humanTimerBadgeObj = CreateCard(matchScreen.transform, "HumanTurnTimerBadge", new Color(0.08f, 0.34f, 0.38f, 0.96f), Vector2.zero, new Vector2(275f, 64f));
        RectTransform timerRect = humanTimerBadgeObj.GetComponent<RectTransform>();
        timerRect.anchorMin = new Vector2(0f, 0f);
        timerRect.anchorMax = new Vector2(0f, 0f);
        timerRect.pivot = new Vector2(0f, 0f);
        timerRect.anchoredPosition = new Vector2(24f, 424f);
        humanTimerBadgeBg = humanTimerBadgeObj.GetComponent<Image>();
        humanTimerLabel = CreateText(humanTimerBadgeObj.transform, "TimerText", "TURN TIMER: 10s", 22, FontStyles.Bold, Color.white, Vector2.zero, new Vector2(255f, 52f));
        humanTimerBadgeObj.SetActive(false);

        // "Bot Auto-Playing" Avatar/HUD Indicator + Instant "Resume Control" Banner
        humanBotIndicatorObj = CreateCard(matchScreen.transform, "BotAutoPlayingBadge", new Color(0.58f, 0.22f, 0.14f, 0.96f), Vector2.zero, new Vector2(290f, 58f));
        RectTransform botIndRect = humanBotIndicatorObj.GetComponent<RectTransform>();
        botIndRect.anchorMin = new Vector2(0f, 0f);
        botIndRect.anchorMax = new Vector2(0f, 0f);
        botIndRect.pivot = new Vector2(0f, 0f);
        botIndRect.anchoredPosition = new Vector2(24f, 496f);
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

        GameObject card = CreateCard(howToPlayModal.transform, "RulesCard", RoatanVisualAssets.DarkSlate, Vector2.zero, new Vector2(920f, 1060f));
        CreateText(card.transform, "RulesTitle", "How to Play Roatan Domino", 42, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, 430f), new Vector2(840f, 64f));

        string rules =
            "<b>1. Match Open Ends</b>\n" +
            "Play a domino whose pip count matches either open end of the table chain. Playable tiles lift up with a golden glow.\n\n" +
            "<b>2. Tap to Place</b>\n" +
            "Tap a glowing domino in your hand, then tap the glowing gold slot on the table (or tap the same domino twice if only one end fits).\n\n" +
            "<b>3. Two Game Modes</b>\n" +
            "• <b>Roatan Cerrado (Blocked):</b> 4 players, all 28 tiles dealt (no boneyard). Pass (Paso!) when blocked. Lowest remaining pips wins a locked (Trancado!) board!\n" +
            "• <b>Classic Draw:</b> Draw from the boneyard when you have no match.\n\n" +
            "<b>4. Touch Controls</b>\n" +
            "Drag with 1 finger to pan the table, pinch with 2 fingers to zoom, or tap <b>Fit</b> to re-center. First to 100 points wins the match!";

        TMP_Text body = CreateText(card.transform, "RulesBody", rules, 28, FontStyles.Normal, Color.white, new Vector2(0f, 20f), new Vector2(820f, 700f));
        body.alignment = TextAlignmentOptions.TopLeft;

        Button gotItBtn = CreatePillButton(card.transform, "BtnCloseRules", RoatanLocalization.T("got_it"), RoatanVisualAssets.PalmGreen, new Vector2(0f, -420f), new Vector2(360f, 92f), 34, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            howToPlayModal.SetActive(false);
        });
        rulesGotItLabel = gotItBtn.GetComponentInChildren<TMP_Text>();

        howToPlayModal.SetActive(false);
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
        bg.color = new Color(0.06f, 0.17f, 0.22f, 0.90f);

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

        TMP_Text nameTxt = CreateText(obj.transform, "Name", "Bot", 26, FontStyles.Bold, Color.white, new Vector2(0f, size.y * 0.22f), new Vector2(size.x - 16f, 34f));
        TMP_Text countTxt = CreateText(obj.transform, "Count", "7 tiles", 23, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, -size.y * 0.08f), new Vector2(size.x - 16f, 30f));
        TMP_Text scoreTxt = CreateText(obj.transform, "Score", "0 pts", 20, FontStyles.Normal, new Color(0.78f, 0.90f, 0.94f), new Vector2(0f, -size.y * 0.34f), new Vector2(size.x - 16f, 26f));

        return new OpponentSeatUI
        {
            Root = obj,
            Background = bg,
            TurnRing = ringImg,
            NameText = nameTxt,
            CountText = countTxt,
            ScoreText = scoreTxt
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
