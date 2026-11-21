using System.Collections;
using System.Collections.Generic;
using System.Linq;

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
    private TMP_Text opponentsHeaderLabel;
    private GameObject opponentsRowObj;
    private TMP_Text playNowBtnLabel;
    private TMP_Text homeSoundBtnLabel;
    private TMP_Text howToPlayBtnLabel;
    private TMP_Text onlineModeBtnLabel;

    private readonly Dictionary<RoatanGameMode, Image> modeCardImages = new Dictionary<RoatanGameMode, Image>();
    private readonly Dictionary<BotDifficulty, Image> diffBtnImages = new Dictionary<BotDifficulty, Image>();
    private readonly Dictionary<BotDifficulty, TMP_Text> diffBtnLabels = new Dictionary<BotDifficulty, TMP_Text>();
    private readonly Dictionary<int, Image> botCountBtnImages = new Dictionary<int, Image>();
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

    // Opponent seat badges (Top = index 0, Left = index 1, Right = index 2)
    private readonly List<OpponentSeatUI> opponentSeats = new List<OpponentSeatUI>();

    // Summary overlay refs
    private TMP_Text summaryTitleLabel;
    private TMP_Text summaryBodyLabel;
    private Button summaryNextBtn;
    private TMP_Text summaryNextBtnLabel;
    private TMP_Text summaryMenuBtnLabel;
    private TMP_Text rulesGotItLabel;

    // Selection & Match State
    private RoatanGameMode selectedMode = RoatanGameMode.BlockedCerrado;
    private BotDifficulty selectedDifficulty = BotDifficulty.Novice;
    private int selectedBotCount = 1; // Used for Classic Draw (1, 2, or 3)

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
        onlineController.Initialize(safeAreaRoot, uiFont, ShowHomeScreen);

        RoatanLocalization.LanguageChanged += OnLanguageChanged;
        SceneManager.activeSceneChanged += OnSceneChanged;
    }

    private void Start()
    {
        ShowHomeScreen();
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

        int bots = selectedBotCount;
        engine = new RoatanMatchEngine(selectedMode, selectedDifficulty, bots);
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
        StopMatchRoutine();
        roundSummaryOverlay.SetActive(false);
        selectedHandDomino = null;
        waitingForHumanInput = false;
        currentHumanMoves.Clear();
        botBrain.ResetForRound();

        boardView.ClearBoard();
        RoatanMatchEngine.PlacedTile opener = engine.StartNewRound();
        RoatanMatchEngine.MatchPlayer starter = engine.Players.First(p => p.Id == opener.PlayerId);

        boardView.RebuildFromEngine(engine, animateLastTile: true);
        RoatanSoundFX.Instance?.PlayPlace();

        RefreshMatchHeader();
        RefreshOpponentSeats();
        RebuildHumanHand();
        UpdateStatusBanner($"{starter.Name} opened with [{opener.Domino.Side1}|{opener.Domino.Side2}]");

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

            List<RoatanMatchEngine.LegalMove> moves = engine.GetLegalMoves(active);

            if (!active.IsBot)
            {
                currentHumanMoves = moves;
                selectedHandDomino = null;
                boardView.ClearSlots();
                waitingForHumanInput = true;
                RebuildHumanHand();

                if (moves.Count > 0)
                {
                    actionButton.gameObject.SetActive(false);
                    UpdateStatusBanner($"Your turn! Tap a glowing domino ({moves.Count} playable)");
                }
                else if (engine.CanCurrentPlayerDraw())
                {
                    actionButton.gameObject.SetActive(true);
                    actionButton.GetComponent<Image>().color = RoatanVisualAssets.IslandGold;
                    actionButtonLabel.text = RoatanLocalization.Tf("draw_tile", engine.BoneyardCount);
                    UpdateStatusBanner("No matching tile — tap Draw Tile!");
                }
                else
                {
                    actionButton.gameObject.SetActive(true);
                    actionButton.GetComponent<Image>().color = RoatanVisualAssets.SunsetCoral;
                    actionButtonLabel.text = RoatanLocalization.T("pass_turn");
                    UpdateStatusBanner("No matching tile — tap Pass Turn (Paso!)");
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
                    boardView.RebuildFromEngine(engine, animateLastTile: true);
                    RoatanSoundFX.Instance?.PlayPlace();
                    UpdateStatusBanner($"{active.Name} played [{placed.Domino.Side1}|{placed.Domino.Side2}]");
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
                    UpdateStatusBanner($"{active.Name} passed (Paso!)");
                }
            }
            else
            {
                botBrain.RecordBlocked(active.Id, engine.LeftEndPip, engine.RightEndPip);
                engine.PassTurn();
                RoatanSoundFX.Instance?.PlayPass();
                UpdateStatusBanner($"{active.Name} passed (Paso!)");
            }

            RefreshMatchHeader();
            RefreshOpponentSeats();
            yield return new WaitForSeconds(0.35f);
        }
    }

    private void OnHumanHandTileClicked(DominoDto domino)
    {
        if (!waitingForHumanInput || engine == null) return;

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
            ExecuteHumanMove(tileMoves[0]);
            return;
        }

        selectedHandDomino = domino;
        RebuildHumanHand();

        bool canLeft = tileMoves.Any(m => m.End == BoardEnd.Left);
        bool canRight = tileMoves.Any(m => m.End == BoardEnd.Right);
        boardView.ShowEndSlots(canLeft, canRight);

        UpdateStatusBanner(tileMoves.Count == 1
            ? "Tap the glowing gold slot (or tap tile again) to place!"
            : "Choose which glowing end on the table to place on!");
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
            ExecuteHumanMove(chosen);
        }
    }

    private void ExecuteHumanMove(RoatanMatchEngine.LegalMove move)
    {
        waitingForHumanInput = false;
        selectedHandDomino = null;
        currentHumanMoves.Clear();
        boardView.ClearSlots();
        actionButton.gameObject.SetActive(false);

        RoatanMatchEngine.PlacedTile placed = engine.PlayMove(move);
        if (placed != null)
        {
            boardView.RebuildFromEngine(engine, animateLastTile: true);
            RoatanSoundFX.Instance?.PlayPlace();
            UpdateStatusBanner($"You played [{placed.Domino.Side1}|{placed.Domino.Side2}]");
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
            }
            else if (engine.CanCurrentPlayerDraw())
            {
                actionButtonLabel.text = RoatanLocalization.Tf("draw_tile", engine.BoneyardCount);
            }
            else
            {
                actionButton.GetComponent<Image>().color = RoatanVisualAssets.SunsetCoral;
                actionButtonLabel.text = RoatanLocalization.T("pass_turn");
                UpdateStatusBanner("Boneyard empty — tap Pass Turn (Paso!)");
            }
        }
        else
        {
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
        waitingForHumanInput = false;
        actionButton.gameObject.SetActive(false);
        boardView.ClearSlots();

        RoatanMatchEngine.RoundResult result = engine.CompleteRound();
        RoatanMatchEngine.MatchPlayer champion = null;
        bool hasChampion = !result.IsDraw && engine.HasMatchChampion(out champion);

        if (!result.IsDraw && result.Winner != null && result.Winner.Id == 0)
        {
            RoatanSoundFX.Instance?.PlayWin();
        }
        else
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
        else
        {
            sb.AppendLine($"{reason}   •   +{result.PointsAwarded} pts to {result.Winner.Name}");
        }
        sb.AppendLine();
        foreach (RoatanMatchEngine.MatchPlayer p in result.Standings)
        {
            string tag = (!result.IsDraw && result.Winner != null && p.Id == result.Winner.Id) ? "★ " : "   ";
            sb.AppendLine($"{tag}{p.Name,-10}   Hand: {p.HandPipSum(),2} pips ({p.Hand.Count} tiles)   |   Score: {p.MatchScore}/{engine.TargetMatchScore}");
        }

        summaryBodyLabel.text = sb.ToString();
        summaryNextBtnLabel.text = hasChampion
            ? RoatanLocalization.T("new_match")
            : RoatanLocalization.Tf("next_round", engine.RoundNumber + 1);
        summaryNextBtn.onClick.RemoveAllListeners();
        summaryNextBtn.onClick.AddListener(() =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            if (hasChampion) StartNewMatch();
            else BeginRound();
        });

        roundSummaryOverlay.SetActive(true);
    }

    private void StopMatchRoutine()
    {
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
        if (summaryMenuBtnLabel != null) summaryMenuBtnLabel.text = RoatanLocalization.T("main_menu");
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

        opponentsHeaderLabel.text = RoatanLocalization.T("opponents");
        opponentsRowObj.SetActive(true);

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
        string modeShort = engine.Mode == RoatanGameMode.BlockedCerrado ? "Cerrado" : "Classic Draw";
        string boneyardPart = engine.Mode == RoatanGameMode.ClassicDraw
            ? $"  •  Boneyard: {engine.BoneyardCount}"
            : (engine.LockedPileCount > 0 ? $"  •  Locked: {engine.LockedPileCount}" : "");
        int humanScore = engine.Players[0].MatchScore;
        matchHeaderPill.text = $"{modeShort} ({RoatanLocalization.DifficultyLabel(engine.Difficulty)})  •  Rd {engine.RoundNumber}  •  You: {humanScore}/{engine.TargetMatchScore}{boneyardPart}";
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
            seat.NameText.text = bot.Name;
            seat.CountText.text = $"{bot.Hand.Count} tiles";
            seat.ScoreText.text = $"{bot.MatchScore} pts";

            bool isTurn = engine.CurrentPlayer.Id == bot.Id;
            seat.TurnRing.color = isTurn ? RoatanVisualAssets.IslandGold : new Color(1f, 1f, 1f, 0.14f);
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

            Button btn = tileBtnObj.AddComponent<Button>();
            btn.targetGraphic = faceImg;
            btn.onClick.AddListener(() => OnHumanHandTileClicked(captured));

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
            Button lb = CreatePillButton(homeScreen.transform, $"Lang_{lang}", RoatanLocalization.CodeFor(lang), new Color(0.10f, 0.24f, 0.30f, 0.95f), new Vector2(langXs[i], 855f), new Vector2(124f, 56f), 24, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                RoatanLocalization.SetLanguage(lang);
            });
            langBtnImages[lang] = lb.GetComponent<Image>();
        }

        // Top Logo Banner
        GameObject headerCard = CreateCard(homeScreen.transform, "HeaderBanner", new Color(0.05f, 0.19f, 0.25f, 0.92f), new Vector2(0f, 675f), new Vector2(960f, 260f));

        GameObject leftDom = new GameObject("LogoDomLeft");
        leftDom.transform.SetParent(headerCard.transform, false);
        RectTransform lRect = leftDom.AddComponent<RectTransform>();
        lRect.sizeDelta = new Vector2(82f, 164f);
        lRect.anchoredPosition = new Vector2(-365f, 8f);
        lRect.localRotation = Quaternion.Euler(0f, 0f, 16f);
        leftDom.AddComponent<Image>().sprite = RoatanVisualAssets.GetDominoSprite(6, 6);

        GameObject rightDom = new GameObject("LogoDomRight");
        rightDom.transform.SetParent(headerCard.transform, false);
        RectTransform rRect = rightDom.AddComponent<RectTransform>();
        rRect.sizeDelta = new Vector2(82f, 164f);
        rRect.anchoredPosition = new Vector2(365f, 8f);
        rRect.localRotation = Quaternion.Euler(0f, 0f, -16f);
        rightDom.AddComponent<Image>().sprite = RoatanVisualAssets.GetDominoSprite(5, 3);

        CreateText(headerCard.transform, "Title", "ROATAN DOMINO", 62, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, 52f), new Vector2(640f, 80f));
        subtitleLabel = CreateText(headerCard.transform, "Subtitle", RoatanLocalization.T("subtitle"), 27, FontStyles.Normal, new Color(0.84f, 0.95f, 0.96f), new Vector2(0f, -14f), new Vector2(660f, 44f));
        statsLabel = CreateText(headerCard.transform, "Stats", "Offline Record: 0 Wins  •  0 Matches", 25, FontStyles.Bold, Color.white, new Vector2(0f, -72f), new Vector2(620f, 40f));

        // Mode Selection Section
        modeHeadingLabel = CreateText(homeScreen.transform, "ModeHeading", RoatanLocalization.T("choose_mode"), 30, FontStyles.Bold, new Color(0.95f, 0.88f, 0.65f), new Vector2(0f, 480f), new Vector2(920f, 44f));

        Button cerrBtn = CreateModeCard(
            homeScreen.transform,
            "CardCerrado",
            RoatanLocalization.T("mode_cerrado_title"),
            RoatanLocalization.T("mode_cerrado_desc"),
            new Vector2(0f, 355f),
            () => { RoatanSoundFX.Instance?.PlayTap(); selectedMode = RoatanGameMode.BlockedCerrado; RefreshHomeSelections(); },
            out cerradoTitleLabel,
            out cerradoDescLabel);
        modeCardImages[RoatanGameMode.BlockedCerrado] = cerrBtn.GetComponent<Image>();

        Button drawBtn = CreateModeCard(
            homeScreen.transform,
            "CardClassicDraw",
            RoatanLocalization.T("mode_draw_title"),
            RoatanLocalization.T("mode_draw_desc"),
            new Vector2(0f, 175f),
            () => { RoatanSoundFX.Instance?.PlayTap(); selectedMode = RoatanGameMode.ClassicDraw; RefreshHomeSelections(); },
            out drawTitleLabel,
            out drawDescLabel);
        modeCardImages[RoatanGameMode.ClassicDraw] = drawBtn.GetComponent<Image>();

        // Bot Difficulty Section
        diffHeadingLabel = CreateText(homeScreen.transform, "DiffHeading", RoatanLocalization.T("bot_difficulty"), 30, FontStyles.Bold, new Color(0.95f, 0.88f, 0.65f), new Vector2(0f, 22f), new Vector2(920f, 44f));

        BotDifficulty[] diffs = { BotDifficulty.Beginner, BotDifficulty.Novice, BotDifficulty.Pro };
        float[] diffXs = { -310f, 0f, 310f };
        for (int i = 0; i < diffs.Length; i++)
        {
            BotDifficulty d = diffs[i];
            Button b = CreatePillButton(homeScreen.transform, $"BtnDiff_{d}", RoatanLocalization.DifficultyLabel(d), new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(diffXs[i], -68f), new Vector2(285f, 92f), 32, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                selectedDifficulty = d;
                RefreshHomeSelections();
            });
            diffBtnImages[d] = b.GetComponent<Image>();
            diffBtnLabels[d] = b.GetComponentInChildren<TMP_Text>();
        }

        diffDescLabel = CreateText(homeScreen.transform, "DiffDesc", "", 25, FontStyles.Italic, new Color(0.88f, 0.96f, 0.98f), new Vector2(0f, -158f), new Vector2(920f, 62f));

        // Opponents Section
        opponentsHeaderLabel = CreateText(homeScreen.transform, "OppHeading", RoatanLocalization.T("opponents"), 26, FontStyles.Bold, new Color(0.95f, 0.88f, 0.65f), new Vector2(0f, -238f), new Vector2(940f, 44f));

        opponentsRowObj = new GameObject("OpponentsRow");
        opponentsRowObj.transform.SetParent(homeScreen.transform, false);
        RectTransform oppRowRect = opponentsRowObj.AddComponent<RectTransform>();
        oppRowRect.anchoredPosition = new Vector2(0f, -318f);
        oppRowRect.sizeDelta = new Vector2(940f, 92f);

        int[] botCounts = { 1, 2, 3 };
        string[] botLabels = { "1v1 (1 Bot)", "3P (2 Bots)", "4P (3 Bots)" };
        for (int i = 0; i < botCounts.Length; i++)
        {
            int c = botCounts[i];
            Button b = CreatePillButton(opponentsRowObj.transform, $"BtnBots_{c}", botLabels[i], new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(diffXs[i], 0f), new Vector2(285f, 82f), 28, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                selectedBotCount = c;
                RefreshHomeSelections();
            });
            botCountBtnImages[c] = b.GetComponent<Image>();
        }

        // Big Play CTA
        Button playBtn = CreatePillButton(homeScreen.transform, "BtnPlayNow", RoatanLocalization.T("play_now"), RoatanVisualAssets.PalmGreen, new Vector2(0f, -505f), new Vector2(720f, 128f), 46, StartNewMatch);
        playNowBtnLabel = playBtn.GetComponentInChildren<TMP_Text>();

        // Bottom Utility Row
        Button soundBtn = CreatePillButton(homeScreen.transform, "BtnHomeSound", RoatanLocalization.T("sound_on"), new Color(0.08f, 0.20f, 0.26f, 0.95f), new Vector2(-315f, -715f), new Vector2(280f, 78f), 26, () =>
        {
            RoatanSoundFX.Instance?.ToggleMute();
            RoatanSoundFX.Instance?.PlayTap();
            RefreshSoundLabels();
        });
        homeSoundBtnLabel = soundBtn.GetComponentInChildren<TMP_Text>();

        Button howBtn = CreatePillButton(homeScreen.transform, "BtnHowToPlay", RoatanLocalization.T("how_to_play"), new Color(0.08f, 0.20f, 0.26f, 0.95f), new Vector2(0f, -715f), new Vector2(280f, 78f), 26, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            howToPlayModal.SetActive(true);
        });
        howToPlayBtnLabel = howBtn.GetComponentInChildren<TMP_Text>();

        Button onlineBtn = CreatePillButton(homeScreen.transform, "BtnOnlineLogin", RoatanLocalization.T("online_mode"), new Color(0.08f, 0.20f, 0.26f, 0.95f), new Vector2(315f, -715f), new Vector2(280f, 78f), 26, OpenOnlineScreen);
        onlineModeBtnLabel = onlineBtn.GetComponentInChildren<TMP_Text>();
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

        GameObject card = CreateCard(roundSummaryOverlay.transform, "SummaryCard", RoatanVisualAssets.DarkSlate, Vector2.zero, new Vector2(920f, 820f));
        summaryTitleLabel = CreateText(card.transform, "SummaryTitle", "ROUND COMPLETE", 44, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, 305f), new Vector2(840f, 74f));
        summaryBodyLabel = CreateText(card.transform, "SummaryBody", "", 27, FontStyles.Normal, Color.white, new Vector2(0f, 30f), new Vector2(820f, 400f));
        summaryBodyLabel.alignment = TextAlignmentOptions.MidlineLeft;

        summaryNextBtn = CreatePillButton(card.transform, "BtnNextRound", "Next Round", RoatanVisualAssets.PalmGreen, new Vector2(0f, -210f), new Vector2(540f, 102f), 36, BeginRound);
        summaryNextBtnLabel = summaryNextBtn.GetComponentInChildren<TMP_Text>();

        Button menuBtn = CreatePillButton(card.transform, "BtnSummaryMenu", RoatanLocalization.T("main_menu"), new Color(0.18f, 0.25f, 0.34f, 1f), new Vector2(0f, -330f), new Vector2(380f, 84f), 30, () =>
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
